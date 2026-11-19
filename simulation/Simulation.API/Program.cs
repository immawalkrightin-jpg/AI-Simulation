using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Simulation.API.Hubs;
using Simulation.API.Scenarios;
using Simulation.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.AddSingleton(provider =>
    new SimulationSession(DefaultScenario.Build(provider.GetRequiredService<IOptions<SimulationOptions>>().Value.Seed)));
builder.Services.AddSingleton<ObservationBuffer>();
builder.Services.AddSingleton<SimulationControl>();
builder.Services.AddSingleton<SimulationBroadcaster>();
builder.Services.AddHostedService<SimulationHost>();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IOptions<SimulationOptions>>((cors, simulation) =>
    cors.AddDefaultPolicy(policy => policy
        .WithOrigins(simulation.Value.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseMiddleware<AccessKeyMiddleware>();
app.MapControllers();
app.MapHub<GroundTruthHub>("/hubs/ground-truth");
app.MapHub<ObservationHub>("/hubs/observations");
app.Run();

public partial class Program
{
}
