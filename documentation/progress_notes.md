# Progress notes

Covers everything added since Milestone 7. Verification status is stated per item.

## Milestone 8: simulation reset (pushed, tests passed)
- `POST /api/simulation/reset?seed=N` (ground-truth key). Missing seed keeps the current one, negative seed returns 400.
- `SimulationSession` keeps the scenario factory and seed. `SimulationBroadcaster` serializes steps and resets, clears the observation buffer and the AI operational map, and pushes snapshot, `reset` and points events.
- `SimulationStatus` gains an optional `seed`.
- Tests: `ResetTests.cs`.

## Milestone 9: training environment (pushed, tests passed)
- `ml/environment/simulation_env.py`: `SimulationEnv` (reset pauses first, chunked advance, horizon clamp).
- `ml/training/episode.py`: `run_episode`.
- `OperatorClient.reset`. `FakeApi` now fakes the operator endpoints.
- No reward yet. The environment reads no ground truth.

## Milestone 10: model registry (pushed, tests passed)
- `ml/registry/`: versions, training runs, evaluations, human corrections, version comparison, weight hashing.
- Records live in `ml/experiments/registry/` (committed). Weights stay in `ml/models/` (ignored).
- Records are never overwritten. Corrections never modify existing model records.

## Milestone 11: feedback backend and importer (pushed, tests passed)
- C#: `FeedbackStore`, `FeedbackController`, `FeedbackContracts`. Routes `/api/feedback/{categories,corrections}` require the ground-truth key (AI key is rejected). Reports are appended to `data/feedback/corrections.jsonl`.
- `SimulationOptions.FeedbackPath` overrides the inbox location. `ApiFactory` now points it at a temp folder.
- C# tests: `FeedbackTests.cs`.
- Python: `HumanCorrection` gains `source_id` and `context`. `ml/feedback/` imports the inbox into the registry once per report (`python -m ml.feedback`).
- Python tests: `test_feedback.py`, including a check that the category list matches the C# list.
- `.gitignore`: `data/feedback/*` ignored.

## Milestone 12: terminal UI (written, needs a manual browser check)
- `frontend/js/terminal/`: `parser.js` (parser logic checked with Node), `feedback.js` (API link), `terminal.js` (report dialog, history, recent reports). Wired into `index.html` (HUD button and a panel in the right column), `style.css` and `main.js`. Toggle with the Terminal button or the backtick key.
- The frontend was reworked while this was in progress (HUD, dossier, camera). The terminal was fitted to the current layout.
- One-line reports should use underscores or a number for the category: `operating alpha 12 friendly_fire note` or `operating alpha 12 3 note`. A spaced `friendly fire` splits wrongly.
- Report convention: `operating [name] [number]`, then category and note, or all in one line.

## Milestone 13: ground-truth evaluator (pushed, tests passed)
- `ml/evaluation/` is the only Python package that reads ground truth. Agent, training, environment, registry and feedback code never mention it, and a test enforces that.
- `scoring.py`: scores the AI's `Tracking` points against real non-destroyed units. Metrics: recall over units its sensors could reach, precision, mean position error, false tracks, missed units, tracks sitting on its own units, and precision of `Confirmed` tracks. Undefined ratios score 1.0.
- `evaluator.py`: `TruthClient` (operator key) and `EpisodeEvaluator` (per-step scores and an episode summary).
- `runner.py`: `evaluate_version` runs a version over a list of seeds and records the averaged metrics in the registry, so versions can be compared.
- `run_episode` takes an optional evaluator and evaluate_every. Summaries gain `metrics`.
- Not covered yet: prediction accuracy (`prd-` points), friendly-fire and casualties, supply outcomes. These need actions the agent does not take yet. There is still no reward function.
- Tests: `ml/tests/test_evaluation.py`.

## Milestone 14: Window A geography, territory and domain rules (pushed, 104 C# and 106 Python tests passed)
- `WorldEngine/Territory/`: `TerritoryMap` (raster of `coldwar.json`: sea, free land, owner per cell, 2-unit cells) and `TerritoryLoader` (reads the map once, found by searching upward from the executable, shared by every scenario build). The map file stays the only source of geography.
- `Units/Domain.cs`: `Domain` (Land, Sea, Air) and `UnitKind.DomainOf()`.
- Rules in `TerritoryMap.Permits`: land units only on their own land, naval units only on water, air units over sea, free land and their own land, never over foreign land. `SegmentPermits` walks every grid cell a straight path touches, so a path cannot clip a corner.
- `WorldState.Territory`, `CanMove`, `TryOrderMove`. `UnitDispatchEvent` goes through `TryOrderMove`, so an illegal order is refused instead of executed.
- `WorldActivityGenerator` retries patrol destinations until one is legal and skips the dispatch when none is. Worlds without a territory behave exactly as before and consume the same random numbers.
- `DefaultScenario`: land and air units are placed from an anchor on their own land (the capital, or the nearest own land cell when the capital falls in the sea, as Havana does), ports snap to the nearest sea cell, scatter radius shrinks by the anchor shift so the old spacing limits still hold.
- Tests: `TerritoryTests.cs` (synthetic maps, corner cutting, world refusal, every unit legal at start and for 1500 ticks on two seeds).
- Checked first with a Python port of the rasterizer, the grid walk and the placement against the real map (86 of 86 units legal, spacing limits hold, 1500 ticks with zero violations), then confirmed by the C# tests.
- Known gap: Cuba's capital marker sits in the sea on the 110m map. Shipments still run capital to capital until milestone 16.

## Milestone 15: unit roster and national power (written; in review)
- `Units/UnitKind.cs`: eight new kinds appended (Tank, ArmoredCar, Warship, Warplane, Helicopter, Truck, CargoShip, CargoPlane). The old generic kinds (Armor, Naval, Air, SupplyConvoy) keep their numeric values and stay valid, so older tests and contracts hold. The scenario no longer places them.
- `Units/UnitCatalog.cs`: one `UnitSpec` per kind with domain, role (Combat or Carrier), speed, strength, range and power weight. `DomainOf` and the new `RoleOf` read from it. Helicopter is a combat unit here; a transport helicopter for milestone 16 is still undecided.
- `Countries/NationalPower.cs`: power is computed, never stored. Forces are the sum of each fielded unit's weight times its share of full strength (destroyed units count zero), plus supplies times 0.02 and ammunition times 0.05.
- `WorldState.AddUnit(owner, kind, position)` takes speed and strength from the catalog.
- `SensorProfiles`: radar sees every vehicle, ship and aircraft kind, sonar sees ships only (Naval, Warship, CargoShip). Optical still sees everything.
- `DefaultScenario`: roster letters are now I infantry, A artillery, K tank, C armored car, W warship, P warplane, H helicopter, T truck, G cargo ship, L cargo plane (T used to mean armor). Three tiers: superpowers (24 units each, plus the Soviet Pacific warship), China, regional powers, then mid and small states. Ships need a port and are skipped without one; several ships at one port are nudged apart. 278 units in total.
- Start power on the real map: USSR 138.6, USA 133.6, China 98.1, regional 41.8 to 67.3, small states at most 27.8.
- API: `CountryDto.Power` (additive, defaulted), filled by `GroundTruthMapper` with one decimal.
- Frontend: `symbols.js` gains the `ArmoredCar` silhouette, aliases `Warplane` and `CargoPlane`, `isAirborne` (the renderer used to lift only `Air`), and legend rows (combat units under Units, truck, cargo ship and cargo plane units under Carriers). `describe.js` has new labels (Tank, Armored car, Warship, Warplane, Cargo ship, Cargo plane) and `powerFor`; the dossier has a National power section with value and rank, and shows "Not reported" when the engine sends no power.
- Tests: `UnitRosterTests.cs` (catalog completeness, roles, power maths, damage, tier order on two seeds, no legacy kinds in the scenario, catalog start figures, snapshot power, radar and sonar per kind). `CarrierAndSpacingTests` and `TerritoryTests` updated for the new kinds. Frontend: `symbols.test.js` and `describe.test.js` updated and extended.
- Verified here: the engine compiles; a console harness that includes the real `DefaultScenario.cs` and the real map passes every milestone invariant (tier order, legal start cells, spacing limits, 1500 ticks legal on seeds 42 and 7, every interaction kind within 2000 ticks, determinism, sensors per kind). 32 frontend tests pass under Node 22.
- NOT verified here: the xUnit suite (the sandbox cannot restore NuGet packages), `Simulation.API` and `Simulation.Tests` compilation, and the browser rendering of the new silhouette and dossier section.
- Open: `Sensor.RandomOtherKind` draws from every kind, so a misclassified contact can come back as a legacy kind such as Armor. Harmless, left alone.

## Window A roadmap (milestones 15 to 19, one per push)
- 15 units and power: DONE, in review (see above).
- 16 logistics routes, hand-offs and their frontend rendering: shipments become multi-leg (truck to port, cargo ship through sea lanes, truck or plane onward), never a cargo ship over land, with a visible hand-off between carriers. Routes avoid or accept risk through foreign and hostile territory.
- 17 political logic: doctrine for who trades, talks and fights (patrons and clients, proxy wars, escalation ladder), transit permission, and hostile countries actively tracking and hunting shipments that enter their land or waters.
- 18 ambush, conquest and paralysis: ambush units, territory changing owner after a decisive clash, paralyzed zones that are unusable until rebuilt, and secondary explosions when a stockpile is hit.
- 19 randomized worlds and the Window A verification pass: seeded variation of powers, relations and positions so the model faces new environments.

## Still open
- Run `dotnet test` and `npm test` (frontend) on milestone 15, then check the new silhouette and the dossier power section in the browser.
- README milestone list is old; `documentation/architecture.md` and `documentation/ai_agent.md` are not updated.
- Window B has no HTML page and ignores the reset event.
- Reward function: the evaluator (milestone 13) scores tracking only. Reward design and the learning loop come after Window A is finished.
