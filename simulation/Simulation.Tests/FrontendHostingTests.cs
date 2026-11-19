using System.Net;
using Xunit;

namespace Simulation.Tests;

public class FrontendHostingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public FrontendHostingTests(ApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RootServesTheFrontendPage()
    {
        var response = await factory.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Window A", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticAssetsAreServed()
    {
        var response = await factory.CreateClient().GetAsync("/css/style.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GuardedChannelsStillRequireKeys()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ground-truth/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/observations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ai/points")).StatusCode);
    }
}
