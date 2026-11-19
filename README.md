# AI-Simulation

Autonomous AI geopolitical and military simulation. The simulation owns ground truth; the AI builds its own operational picture from partial, delayed and uncertain observations which was inspired by the recent hit Iron Nest.

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

Generate the world map once (needs `pip install shapely` and network access), then serve the frontend:

```
python data/world/build_map.py
cd frontend
python -m http.server 5500
```

## Milestones

1. Repository scaffold (done)
2. World engine core: clock, countries, units, events, diplomacy (done)
3. Sensors and observation separation (done)
4. API contracts and streaming hub (done)
5. Frontend Window A map renderer (done)
6. Cold War world and Window A line language (done)
7. Frontend Window B AI map with confidence-typed markers
8. Python environment bridge and central intelligence skeleton
9. Terminal feedback and model registry
10. Reinforcement learning loop
