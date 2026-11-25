using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Simulation.API.Contracts.Observations;
using Xunit;

namespace Simulation.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string FeedbackPath { get; } = Path.Combine(Path.GetTempPath(), "sim-feedback-" + Guid.NewGuid().ToString("N"));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(FeedbackPath))
        {
            Directory.Delete(FeedbackPath, true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:AutoStart"] = "false",
                ["Simulation:GroundTruthKey"] = "gt",
                ["Simulation:AiKey"] = "ai",
                ["Simulation:FeedbackPath"] = FeedbackPath
            }));
    }
}

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public ApiTests(ApiFactory factory)
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

    [Fact]
    public async Task GroundTruthRequiresGroundTruthKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/ground-truth/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("ai").GetAsync("/api/ground-truth/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client("gt").GetAsync("/api/ground-truth/snapshot")).StatusCode);
    }

    [Fact]
    public async Task ObservationsRequireAiKeyAndRejectGroundTruthKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/observations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("gt").GetAsync("/api/observations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client("ai").GetAsync("/api/observations")).StatusCode);
    }

    [Fact]
    public async Task SimulationControlRequiresGroundTruthKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("ai").PostAsync("/api/simulation/step", null)).StatusCode);
    }

    [Fact]
    public async Task SnapshotContainsCountriesUnitsAndSensors()
    {
        var snapshot = await Json(await Client("gt").GetAsync("/api/ground-truth/snapshot"));
        var countries = snapshot.GetProperty("countries").GetArrayLength();
        Assert.True(countries >= 20);
        Assert.Equal(1, snapshot.GetProperty("countries").EnumerateArray().Count(c => c.GetProperty("isModel").GetBoolean()));
        Assert.True(snapshot.GetProperty("units").GetArrayLength() > 0);
        Assert.True(snapshot.GetProperty("sensors").GetArrayLength() > 0);
        Assert.Equal(countries * (countries - 1) / 2, snapshot.GetProperty("relations").GetArrayLength());
        Assert.True(snapshot.TryGetProperty("interactions", out _));
        Assert.Equal(JsonValueKind.Array, snapshot.GetProperty("zones").ValueKind);
        Assert.Equal(JsonValueKind.Array, snapshot.GetProperty("occupations").ValueKind);
    }

    [Fact]
    public async Task StepAdvancesTheClock()
    {
        var client = Client("gt");
        var before = (await Json(await client.GetAsync("/api/simulation/status"))).GetProperty("tick").GetInt64();
        var after = (await Json(await client.PostAsync("/api/simulation/step?count=5", null))).GetProperty("tick").GetInt64();
        Assert.True(after >= before + 5);
    }

    [Fact]
    public async Task StepRejectsOutOfRangeCounts()
    {
        var client = Client("gt");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/simulation/step?count=0", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/simulation/step?count=5000", null)).StatusCode);
    }

    [Fact]
    public async Task PauseAndResumeToggleStatus()
    {
        var client = Client("gt");
        var resumed = await Json(await client.PostAsync("/api/simulation/resume", null));
        Assert.False(resumed.GetProperty("paused").GetBoolean());
        var paused = await Json(await client.PostAsync("/api/simulation/pause", null));
        Assert.True(paused.GetProperty("paused").GetBoolean());
    }

    [Fact]
    public async Task ObservationsCarryNoGroundTruthIdentity()
    {
        await Client("gt").PostAsync("/api/simulation/step?count=300", null);
        var page = await Json(await Client("ai").GetAsync("/api/observations?after=0"));
        Assert.True(page.GetProperty("latestSequence").GetInt64() >= 0);
        foreach (var item in page.GetProperty("observations").EnumerateArray())
        {
            Assert.False(item.TryGetProperty("unitId", out _));
            Assert.False(item.TryGetProperty("id", out _));
            Assert.False(item.TryGetProperty("owner", out _));
        }
    }

    [Fact]
    public async Task ObservationCursorReturnsOnlyNewerItems()
    {
        await Client("gt").PostAsync("/api/simulation/step?count=300", null);
        var all = await Json(await Client("ai").GetAsync("/api/observations?after=0"));
        var latest = all.GetProperty("latestSequence").GetInt64();
        var newer = await Json(await Client("ai").GetAsync($"/api/observations?after={latest}"));
        Assert.Equal(0, newer.GetProperty("observations").GetArrayLength());
    }

    [Fact]
    public void ObservationContractsNeverReferenceGroundTruthTypes()
    {
        var contractTypes = typeof(ObservationDto).Assembly.GetTypes()
            .Where(t => t.Namespace is "Simulation.API.Contracts.Observations" or "Simulation.API.Contracts.Ai")
            .ToList();
        Assert.NotEmpty(contractTypes);
        foreach (var type in contractTypes)
        {
            foreach (var property in type.GetProperties())
            {
                foreach (var referenced in Flatten(property.PropertyType))
                {
                    Assert.NotEqual("Simulation.API.Contracts.GroundTruth", referenced.Namespace);
                    Assert.False(referenced.Namespace?.StartsWith("WorldEngine.World") ?? false);
                    Assert.False(referenced.Namespace?.StartsWith("WorldEngine.Units") ?? false);
                }
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in Flatten(argument))
                {
                    yield return nested;
                }
            }
        }
    }
}
