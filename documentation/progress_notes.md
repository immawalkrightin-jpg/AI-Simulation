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

## Milestone 11: feedback backend and importer (written, NOT yet verified)
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

## Milestone 13: ground-truth evaluator (written, NOT yet verified)
- `ml/evaluation/` is the only Python package that reads ground truth. Agent, training, environment, registry and feedback code never mention it, and a test enforces that.
- `scoring.py`: scores the AI's `Tracking` points against real non-destroyed units. Metrics: recall over units its sensors could reach, precision, mean position error, false tracks, missed units, tracks sitting on its own units, and precision of `Confirmed` tracks. Undefined ratios score 1.0.
- `evaluator.py`: `TruthClient` (operator key) and `EpisodeEvaluator` (per-step scores and an episode summary).
- `runner.py`: `evaluate_version` runs a version over a list of seeds and records the averaged metrics in the registry, so versions can be compared.
- `run_episode` takes an optional evaluator and evaluate_every. Summaries gain `metrics`.
- Not covered yet: prediction accuracy (`prd-` points), friendly-fire and casualties, supply outcomes. These need actions the agent does not take yet. There is still no reward function.
- Tests: `ml/tests/test_evaluation.py`.

## Milestone 14: Window A geography, territory and domain rules (written, NOT yet built or run here)
- `WorldEngine/Territory/`: `TerritoryMap` (raster of `coldwar.json`: sea, free land, owner per cell, 2-unit cells) and `TerritoryLoader` (reads the map once, found by searching upward from the executable, shared by every scenario build). The map file stays the only source of geography.
- `Units/Domain.cs`: `Domain` (Land, Sea, Air) and `UnitKind.DomainOf()`.
- Rules in `TerritoryMap.Permits`: land units only on their own land, naval units only on water, air units over sea, free land and their own land, never over foreign land. `SegmentPermits` walks every grid cell a straight path touches, so a path cannot clip a corner.
- `WorldState.Territory`, `CanMove`, `TryOrderMove`. `UnitDispatchEvent` goes through `TryOrderMove`, so an illegal order is refused instead of executed.
- `WorldActivityGenerator` retries patrol destinations until one is legal and skips the dispatch when none is. Worlds without a territory behave exactly as before and consume the same random numbers.
- `DefaultScenario`: land and air units are placed from an anchor on their own land (the capital, or the nearest own land cell when the capital falls in the sea, as Havana does), ports snap to the nearest sea cell, scatter radius shrinks by the anchor shift so the old spacing limits still hold.
- Tests: `TerritoryTests.cs` (synthetic maps, corner cutting, world refusal, every unit legal at start and for 1500 ticks on two seeds).
- Checked with a Python port of the rasterizer, the grid walk and the placement against the real map: 86 of 86 units legal, spacing limits hold, 1500 simulated ticks with zero violations and 80 of 86 units moving. The C# itself has not been compiled or run.
- Known gap: Cuba's capital marker sits in the sea on the 110m map. Shipments still run capital to capital until milestone 16.

## Window A roadmap (milestones 15 to 19, one per push)
- 15 units and power: separate unit kinds (tank, armored car, warship, warplane, helicopter, cargo ship, cargo plane, truck), each with domain, speed, strength, range and role; country power computed from what it fields and what it stockpiles, with Cold War figures. Needs frontend symbols for the new kinds.
- 16 logistics routes, hand-offs and their frontend rendering: shipments become multi-leg (truck to port, cargo ship through sea lanes, truck or plane onward), never a cargo ship over land, with a visible hand-off between carriers. Routes avoid or accept risk through foreign and hostile territory.
- 17 political logic: doctrine for who trades, talks and fights (patrons and clients, proxy wars, escalation ladder), transit permission, and hostile countries actively tracking and hunting shipments that enter their land or waters.
- 18 ambush, conquest and paralysis: ambush units, territory changing owner after a decisive clash, paralyzed zones that are unusable until rebuilt, and secondary explosions when a stockpile is hit.
- 19 randomized worlds and the Window A verification pass: seeded variation of powers, relations and positions so the model faces new environments.

## Still open
- README and `documentation/ai_agent.md` are not updated.
- Window B has no HTML page and ignores the reset event.
- Ground-truth evaluator (reward and evaluation metrics) is next after the terminal.
- Two xUnit2012 warnings remain in `InteractionEngineTests.cs`.
