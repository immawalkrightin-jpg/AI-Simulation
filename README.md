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
7. Render pipeline cleanup (in review)
8. Simulation reset (done)
9. Training environment (done)
10. Model registry (done)
11. Feedback backend and importer
    - [x] Feedback store, controller and contracts
    - [x] Importer into the registry
    - [x] Run C# and Python tests
12. Terminal UI
    - [x] Report parser, API link, report dialog and history
    - [ ] Manual check in the browser (Didn't make a proper trigger for it yet..)
13. Ground-truth evaluator
    - [x] Scoring of the AI's tracks against real units
    - [x] Version evaluation over a list of seeds
    - [x] Run Python tests
14. Window A geography (in review)
    - [x] Territory raster built from coldwar.json
    - [x] Keep `DefaultScenario.Build(int seed)` signature
    - [x] Land units stay inside their own territory
    - [x] Naval units stay at sea
    - [x] Air units stay out of foreign airspace
    - [x] Patrol and dispatch destinations validated against the raster
    - [x] Ports snap to the nearest sea cell
    - [x] Tests: legal start cells, long run keeps units legal
    - [x] Run `dotnet test` (104 passed)
15. Unit roster and national power (in review)
    - [x] Tank, armored car, warship, warplane, helicopter
    - [x] Truck, cargo ship, cargo plane as carriers, separate from combat units
    - [x] Domain, speed, strength, range and role per kind
    - [x] Country power computed from fielded forces and stockpiles
    - [x] Cold War starting figures (superpowers, regional powers, small states)
    - [x] Power shown in the dossier
    - [x] Frontend symbols, manual and legend rows for the new kinds
    - [x] Tests: power order, one domain per kind, legal start cells
    - [x] Run `dotnet test` and `npm test`
16. Logistics routing, hand-offs and route rendering (in review)
    - [x] Ports per country (Soviet Union: Arctic, Baltic, Black Sea, Pacific)
    - [x] Sea routes over the raster with shipping canal cuts (Turkish Straits, Suez, Red Sea, Panama)
    - [x] Land links between neighbouring countries
    - [x] Multi-leg shipments with hand-offs at ports
    - [x] Truck, helicopter or plane continues after a cargo ship, never a ship over land
    - [x] `Route` added to `InteractionDto`
    - [x] Validate sea legs on water and land legs on land
    - [x] Shipments follow the engine route in the frontend
    - [x] Carrier changes at each hand-off, hand-off symbol, legend rows
    - [x] Tooltip and dossier show current leg and chain
    - [x] Tests: routes, hand-offs, bare worlds still work
    - [x] Run `dotnet test` and `npm test`
    - [x] Manual check in the browser (route lines, hand-off rings, carrier change)
17. Cold War political logic and hunting of shipments (done)
    - [x] Doctrine object supplied by the scenario (`world.Doctrine`)
    - [x] Patron-to-client supply flows inside each bloc (and to non-aligned clients)
    - [x] Summit and arms-limitation talks between the blocs
    - [x] Proxy conflicts only where the Cold War had them (Korea, Indochina, Sino-Indian border, Suez, plus capped German and Caribbean fronts)
    - [x] Escalation ladder: tension, incident, clash, missile
    - [x] Cool-down so relations settle instead of oscillating (quiet period and ceasefire talks)
    - [x] Transit permission: routes avoid hostile land and coastal waters by default
    - [x] Running the gauntlet: aid may take a hostile route only when no safe lane exists
    - [x] Hostile countries track and hunt shipments that enter their land or waters
    - [x] Event log states the cause of each event
    - [x] Keep no direct Soviet-American or Pact-NATO war
    - [x] Engine without a doctrine keeps current behaviour
    - [x] Frontend: cause and hunter in the shipment tooltip, Fronts section in the dossier
    - [x] Tests: doctrine rules, ladder, hunting, waters, determinism, long run
    - [x] Run `dotnet test` and `npm test`
    - [x] Manual check in the browser (Fronts section, Cause and Hunted by rows)
18. Ambush, conquest, paralyzed zones and secondary explosions (in progress)
    - [ ] Ambush units that wait on routes and chokepoints
    - [ ] Territory changes owner after a decisive clash
    - [x] Paralyzed zones created by a missile strike, healing over time, with a reduced supply income and no shipping while badly hit
    - [x] Secondary explosion when a stockpile is hit
    - [x] Paralyzed zones in the snapshot (`Zones`)
    - [ ] Frontend overlays for conquered land and paralyzed zones
    - [x] Tests so far: zone severity and repair, creation, shipping block, secondary blasts, bare worlds unchanged
    - [ ] Tests: ownership change, ambush, determinism
19. Randomized worlds and Window A verification
    - [ ] Seeded variation of powers, stockpiles, relations and positions inside Cold War limits
    - [ ] Default seed keeps the historical set-up
    - [ ] Long-run invariant tests on default and randomized worlds
    - [ ] Update architecture.md and frontend.md
20. Frontend Window B AI map with confidence-typed markers
21. Reinforcement learning loop
