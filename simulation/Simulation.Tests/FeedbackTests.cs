using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Simulation.Tests;

public class FeedbackTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public FeedbackTests(ApiFactory factory)
    {
        this.factory = factory;
    }

    private HttpClient Client(string? key = null)
    {
        var client = factory.CreateClient();
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add("X-Access-Key", key);
        }
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static object Report(
        string operation = "alpha",
        int number = 12,
        string category = "friendly_fire",
        string description = "Hit an own supply convoy.") =>
        new { operation, number, category, description };

    private string[] StoredLines()
    {
        var path = Path.Combine(factory.FeedbackPath, "corrections.jsonl");
        return File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0).ToArray() : Array.Empty<string>();
    }

    [Fact]
    public async Task FeedbackRoutesRequireTheGroundTruthKey()
    {
        foreach (var key in new string?[] { null, "ai" })
        {
            var client = Client(key);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/feedback/categories")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/feedback/corrections")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/feedback/corrections", Report())).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await Client("gt").GetAsync("/api/feedback/categories")).StatusCode);
    }

    [Fact]
    public async Task CategoriesListTheSupportedFailureKinds()
    {
        var categories = (await Json(await Client("gt").GetAsync("/api/feedback/categories")))
            .EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Equal(
            new[]
            {
                "target_missed", "wrong_target_assessment", "friendly_fire", "moral_judgement",
                "tactical_mistake", "supply_allocation", "supply_transport"
            },
            categories);
    }

    [Fact]
    public async Task ValidReportIsAcceptedAndPersisted()
    {
        var before = StoredLines().Length;
        var response = await Client("gt").PostAsJsonAsync("/api/feedback/corrections", Report(description: "  Hit an own supply convoy.  "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await Json(response);
        Assert.False(string.IsNullOrEmpty(created.GetProperty("id").GetString()));
        Assert.Equal("alpha", created.GetProperty("operation").GetString());
        Assert.Equal(12, created.GetProperty("number").GetInt32());
        Assert.Equal("friendly_fire", created.GetProperty("category").GetString());
        Assert.Equal("Hit an own supply convoy.", created.GetProperty("description").GetString());

        var lines = StoredLines();
        Assert.Equal(before + 1, lines.Length);
        var stored = JsonDocument.Parse(lines[^1]).RootElement;
        Assert.Equal(created.GetProperty("id").GetString(), stored.GetProperty("id").GetString());
        Assert.Equal("Hit an own supply convoy.", stored.GetProperty("description").GetString());
    }

    [Fact]
    public async Task ReportCarriesTheSeedAndTickOfTheSimulation()
    {
        var gt = Client("gt");
        await gt.PostAsync("/api/simulation/reset?seed=21", null);
        await gt.PostAsync("/api/simulation/step?count=5", null);
        var created = await Json(await gt.PostAsJsonAsync("/api/feedback/corrections", Report()));
        Assert.Equal(21, created.GetProperty("seed").GetInt32());
        Assert.True(created.GetProperty("tick").GetInt64() >= 5);
    }

    [Fact]
    public async Task InvalidReportsAreRejectedAndNothingIsStored()
    {
        var before = StoredLines().Length;
        var gt = Client("gt");
        var invalid = new object[]
        {
            Report(operation: ""),
            Report(operation: "1alpha"),
            Report(operation: "bad name"),
            Report(operation: "alpha\n"),
            Report(operation: new string('a', 41)),
            Report(number: -1),
            Report(number: 1_000_000_000),
            Report(category: "unknown"),
            Report(category: "Friendly_Fire"),
            Report(description: ""),
            Report(description: "   "),
            Report(description: new string('x', 2001))
        };
        foreach (var report in invalid)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await gt.PostAsJsonAsync("/api/feedback/corrections", report)).StatusCode);
        }
        Assert.Equal(before, StoredLines().Length);
    }

    [Fact]
    public async Task MalformedBodiesAreRejected()
    {
        var gt = Client("gt");
        var broken = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await gt.PostAsync("/api/feedback/corrections", broken)).StatusCode);
        var empty = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await gt.PostAsync("/api/feedback/corrections", empty)).StatusCode);
    }

    [Fact]
    public async Task RecentReturnsTheNewestReportsInOrder()
    {
        var gt = Client("gt");
        for (var i = 1; i <= 3; i++)
        {
            await gt.PostAsJsonAsync("/api/feedback/corrections", Report(operation: "recent", number: i));
        }
        var recent = await Json(await gt.GetAsync("/api/feedback/corrections?count=2"));
        var numbers = recent.EnumerateArray().Select(c => c.GetProperty("number").GetInt32()).ToList();
        Assert.Equal(new[] { 2, 3 }, numbers);
        Assert.All(recent.EnumerateArray(), c => Assert.Equal("recent", c.GetProperty("operation").GetString()));
    }

    [Fact]
    public async Task RecentRejectsOutOfRangeCounts()
    {
        var gt = Client("gt");
        Assert.Equal(HttpStatusCode.BadRequest, (await gt.GetAsync("/api/feedback/corrections?count=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await gt.GetAsync("/api/feedback/corrections?count=201")).StatusCode);
    }
}
