# AI-Simulation

Autonomous AI geopolitical and military simulation. The simulation owns ground truth; the AI builds its own operational picture from partial, delayed and uncertain observations.

## Layout

- `simulation/` C# / .NET authoritative world engine and API
- `frontend/` HTML, CSS, JavaScript map workstation (Window A ground truth, Window B AI map)
- `ml/` Python / PyTorch agents, central intelligence, training, evaluation, feedback
- `data/` world snapshots, event logs, observations, datasets
- `documentation/` specification, architecture, experiments

## Run

```
cd simulation
dotnet test
dotnet run --project Simulation.API
```

The API listens on `http://localhost:5080`. See `documentation/architecture.md` for channels and access keys.

## Milestones

1. Repository scaffold (done)
2. World engine core: clock, countries, units, events, diplomacy (done)
3. Sensors and observation separation (done)
4. API contracts and streaming hub (done)
5. Frontend Window A map renderer
6. Frontend Window B AI map with confidence-typed markers
7. Python environment bridge and central intelligence skeleton
8. Terminal feedback and model registry
9. Reinforcement learning loop
