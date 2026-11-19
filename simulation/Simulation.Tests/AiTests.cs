using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Simulation.Tests;

public class AiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public AiTests(ApiFactory factory)
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

    private static object Point(string kind = "SuspectedTarget", float x = 300, float y = 200, float radius = 20) =>
        new { kind, confidence = "Estimated", x, y, radius, label = "battery", path = Array.Empty<float>() };

    [Fact]
    public async Task AiChannelRequiresTheAiKey()
    {
        foreach (var path in new[] { "/api/ai/assets", "/api/ai/points" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client("gt").GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Client("ai").GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task AssetsExposeSensorsWithoutUnitsOrOwners()
    {
        var assets = await Json(await Client("ai").GetAsync("/api/ai/assets"));
        Assert.True(assets.GetProperty("sensors").GetArrayLength() > 0);
        Assert.False(assets.TryGetProperty("units", out _));
        Assert.False(assets.TryGetProperty("countries", out _));
        foreach (var sensor in assets.GetProperty("sensors").EnumerateArray())
        {
            Assert.False(sensor.TryGetProperty("owner", out _));
        }
    }

    [Fact]
    public async Task PointsCanBeCreatedListedAndDeleted()
    {
        var client = Client("ai");
        var created = await client.PutAsJsonAsync("/api/ai/points/crud-1", Point());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var listed = await Json(await client.GetAsync("/api/ai/points"));
        Assert.Contains(
            listed.GetProperty("points").EnumerateArray(),
            p => p.GetProperty("id").GetString() == "crud-1" && p.GetProperty("kind").GetString() == "SuspectedTarget");
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/ai/points/crud-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/ai/points/crud-1")).StatusCode);
    }

    [Fact]
    public async Task InvalidPointsAreRejected()
    {
        var client = Client("ai");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/points/bad-1", Point(kind: "Nuke"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/points/bad-2", Point(kind: "1"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/points/bad-3", Point(x: 99999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/points/bad-4", Point(radius: 9000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/points/bad%20id", Point())).StatusCode);
    }

    [Fact]
    public async Task UpdatingAPointKeepsItsCreationTick()
    {
        var ai = Client("ai");
        var first = await Json(await ai.PutAsJsonAsync("/api/ai/points/keep-1", Point()));
        await Client("gt").PostAsync("/api/simulation/step?count=5", null);
        var second = await Json(await ai.PutAsJsonAsync("/api/ai/points/keep-1", Point(x: 320)));
        var before = first.GetProperty("points").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "keep-1");
        var after = second.GetProperty("points").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "keep-1");
        Assert.Equal(before.GetProperty("createdTick").GetInt64(), after.GetProperty("createdTick").GetInt64());
        Assert.True(after.GetProperty("updatedTick").GetInt64() > before.GetProperty("updatedTick").GetInt64());
        Assert.Equal(320, after.GetProperty("x").GetSingle());
    }
}
