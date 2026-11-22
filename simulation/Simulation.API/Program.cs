using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Simulation.API.Hubs;
using Simulation.API.Scenarios;
using Simulation.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.AddSingleton(provider =>
{
    var seed = provider.GetRequiredService<IOptions<SimulationOptions>>().Value.Seed;
    return new SimulationSession(DefaultScenario.Build(seed), DefaultScenario.Build, seed);
});
builder.Services.AddSingleton<ObservationBuffer>();
builder.Services.AddSingleton<OperationalMap>();
builder.Services.AddSingleton<FeedbackStore>();
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

string? frontendPath = null;
for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null && frontendPath is null; directory = directory.Parent)
{
    var candidate = Path.Combine(directory.FullName, "frontend");
    if (File.Exists(Path.Combine(candidate, "index.html")))
    {
        frontendPath = candidate;
    }
}
if (frontendPath is not null)
{
    var frontendFiles = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendFiles });
}

app.UseCors();
app.UseMiddleware<AccessKeyMiddleware>();
app.MapControllers();
app.MapHub<GroundTruthHub>("/hubs/ground-truth");
app.MapHub<ObservationHub>("/hubs/observations");
app.MapHub<AiHub>("/hubs/ai");
app.Run();

public partial class Program
{
}
