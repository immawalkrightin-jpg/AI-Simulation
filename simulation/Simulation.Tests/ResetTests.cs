using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Simulation.Tests;

public class ResetTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public ResetTests(ApiFactory factory)
    {
        this.factory = factory;
    }

    private HttpClient Client(string key)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Access-Key", key);
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<string> RunSeed(int seed, int ticks)
    {
        var client = Client("gt");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/simulation/reset?seed={seed}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/simulation/step?count={ticks}", null)).StatusCode);
        return await (await client.GetAsync("/api/ground-truth/snapshot")).Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task ResetRequiresTheGroundTruthKey()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/simulation/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("ai").PostAsync("/api/simulation/reset", null)).StatusCode);
    }

    [Fact]
    public async Task ResetRewindsTheClockAndReportsTheSeed()
    {
        var client = Client("gt");
        await client.PostAsync("/api/simulation/step?count=25", null);
        var status = await Json(await client.PostAsync("/api/simulation/reset?seed=9", null));
        Assert.Equal(0, status.GetProperty("tick").GetInt64());
        Assert.Equal(9, status.GetProperty("seed").GetInt32());
        var current = await Json(await client.GetAsync("/api/simulation/status"));
        Assert.Equal(0, current.GetProperty("tick").GetInt64());
        Assert.Equal(9, current.GetProperty("seed").GetInt32());
    }

    [Fact]
    public async Task ResetWithoutASeedKeepsTheCurrentSeed()
    {
        var client = Client("gt");
        await client.PostAsync("/api/simulation/reset?seed=11", null);
        await client.PostAsync("/api/simulation/step?count=10", null);
        var status = await Json(await client.PostAsync("/api/simulation/reset", null));
        Assert.Equal(11, status.GetProperty("seed").GetInt32());
        Assert.Equal(0, status.GetProperty("tick").GetInt64());
    }

    [Fact]
    public async Task NegativeSeedIsRejectedAndChangesNothing()
    {
        var client = Client("gt");
        await client.PostAsync("/api/simulation/reset?seed=5", null);
        await client.PostAsync("/api/simulation/step?count=10", null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/simulation/reset?seed=-1", null)).StatusCode);
        var status = await Json(await client.GetAsync("/api/simulation/status"));
        Assert.Equal(5, status.GetProperty("seed").GetInt32());
        Assert.True(status.GetProperty("tick").GetInt64() >= 10);
    }

    [Fact]
    public async Task SameSeedReproducesTheSameWorld()
    {
        var first = await RunSeed(7, 200);
        var second = await RunSeed(7, 200);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task DifferentSeedsProduceDifferentWorlds()
    {
        var first = await RunSeed(7, 600);
        var second = await RunSeed(8, 600);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ResetClearsObservationsAndOperationalPoints()
    {
        var gt = Client("gt");
        var ai = Client("ai");
        await gt.PostAsync("/api/simulation/step?count=1000", null);
        var put = await ai.PutAsJsonAsync("/api/ai/points/probe-1", new
        {
            kind = "Tracking",
            confidence = "Estimated",
            x = 100f,
            y = 100f,
            radius = 10f,
            label = "probe",
            path = Array.Empty<float>()
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var before = await Json(await ai.GetAsync("/api/ai/points"));
        Assert.Equal(1, before.GetProperty("points").GetArrayLength());

        await gt.PostAsync("/api/simulation/reset?seed=3", null);

        var points = await Json(await ai.GetAsync("/api/ai/points"));
        Assert.Equal(0, points.GetProperty("points").GetArrayLength());
        Assert.True(points.GetProperty("version").GetInt64() > before.GetProperty("version").GetInt64());
        var page = await Json(await ai.GetAsync("/api/observations?after=0"));
        Assert.Equal(0, page.GetProperty("latestSequence").GetInt64());
        Assert.Equal(0, page.GetProperty("observations").GetArrayLength());
    }

    [Fact]
    public async Task AssetsReportTheRestartedClock()
    {
        var gt = Client("gt");
        await gt.PostAsync("/api/simulation/step?count=40", null);
        await gt.PostAsync("/api/simulation/reset?seed=2", null);
        var assets = await Json(await Client("ai").GetAsync("/api/ai/assets"));
        Assert.Equal(0, assets.GetProperty("tick").GetInt64());
    }
}
