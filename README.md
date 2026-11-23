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
7. Map correctness and Cold War logic
   - 7.1 Remove render files that do not support the map (in review)
     - [x] Remove terrain.js
     - [x] Remove Window B renderer, picture and link (rebuilt in 8)
     - [x] Remove ai_panel.js and the old telemetry panel.js
     - [ ] Run frontend tests after removal
   - 7.2 Cold War political logic
     - [ ] Doctrine object supplied by the scenario
     - [ ] Patron-to-client supply flows inside each bloc
     - [ ] Summit and arms-limitation talks between the blocs
     - [ ] Proxy conflicts only where the Cold War had them (Korea, Vietnam, Middle East, Sino-Indian border)
     - [ ] Escalation ladder: tension, incident, clash, missile
     - [ ] Cool-down so relations settle instead of oscillating
     - [ ] Event log states the cause of each event
     - [ ] Keep no direct Soviet-American or Pact-NATO war
     - [ ] Engine without a doctrine keeps current behaviour
     - [ ] Tests: doctrine rules, determinism, long run
   - 7.3 Geography in the engine
     - [ ] Territory raster built from coldwar.json
     - [ ] Keep `DefaultScenario.Build(int seed)` signature
     - [ ] Land units stay inside their own territory
     - [ ] Naval units stay at sea
     - [ ] Patrol and dispatch destinations validated against the raster
     - [ ] Ports validated as coastal
     - [ ] Tests: legal start cells, long run keeps units legal
   - 7.4 Logistics routing
     - [ ] Ports per country (Soviet Union: Arctic, Baltic, Black Sea, Pacific)
     - [ ] Sea lane graph with chokepoints (Danish straits, GIUK, Bosporus, Gibraltar, Suez, Malacca, Panama, Cape)
     - [ ] Land links between neighbouring countries
     - [ ] Multi-leg shipments with hand-offs at ports
     - [ ] Interception risk per leg and chokepoint
     - [ ] `Route` added to `InteractionDto`
     - [ ] Validate sea legs on water and land legs on land
     - [ ] Tests: routes, hand-offs, bare worlds still work
   - 7.5 Frontend route rendering
     - [ ] Shipments follow the engine route
     - [ ] Carrier changes at each hand-off
     - [ ] Hand-off symbol
     - [ ] Symbol manual and in-view legend rows
     - [ ] Tooltip and dossier show current leg and chain
   - 7.6 Verification
     - [ ] Long-run invariant tests
     - [ ] Update architecture.md and frontend.md
8. Frontend Window B AI map with confidence-typed markers
9. Python environment bridge and central intelligence skeleton
10. Terminal feedback and model registry
11. Reinforcement learning loop

Each milestone is submitted for review, then committed by the owner, before the next one starts.
