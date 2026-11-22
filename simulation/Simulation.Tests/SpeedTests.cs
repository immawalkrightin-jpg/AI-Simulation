using System.Net;
using System.Text.Json;
using Xunit;

namespace Simulation.Tests;

public class SpeedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public SpeedTests(ApiFactory factory)
    {
        this.factory = factory;
    }

    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Access-Key", "gt");
        return client;
    }

    [Fact]
    public async Task SpeedIsReportedInStatusAndCanBeChanged()
    {
        var client = Client();
        var changed = JsonDocument.Parse(await (await client.PostAsync("/api/simulation/speed?value=4", null)).Content.ReadAsStringAsync());
        Assert.Equal(4, changed.RootElement.GetProperty("speed").GetDouble());
        var status = JsonDocument.Parse(await (await client.GetAsync("/api/simulation/status")).Content.ReadAsStringAsync());
        Assert.Equal(4, status.RootElement.GetProperty("speed").GetDouble());
        await client.PostAsync("/api/simulation/speed?value=1", null);
    }

    [Fact]
    public async Task SpeedRejectsOutOfRangeValues()
    {
        var client = Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/simulation/speed?value=0", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/simulation/speed?value=50", null)).StatusCode);
    }

    [Fact]
    public async Task SpeedRequiresTheGroundTruthKey()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/simulation/speed?value=2", null)).StatusCode);
    }
}
