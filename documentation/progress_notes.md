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
- Resolved in milestone 16: `Sensor.RandomOtherKind` no longer draws legacy kinds.

## Milestone 16: logistics routing, hand-offs and route rendering (written; in review)
- Engine: `Routing/RouteNetwork.cs` (ports, canal cuts, A* over sea, land and air on the territory raster, hostile land blocked, cache) and `Routing/ShipmentRoute.cs` (legs, hand-offs, `At(progress)`). `InteractionEngine.Ship` plans with `PlanBest` and drops the shipment when nothing is legal. `Interaction.ProgressAt` is the single progress rule shared with the frontend.
- Ports: 33 sites, four for the Soviet Union (Murmansk, Baltiysk, Sevastopol, Vladivostok). Each berth is re-snapped to a water cell that touches its owner's land, so the truck-to-dock leg is always a single cell and `IsLegal` holds. A port with no such cell is dropped.
- Canal cuts (shipping only, trucks may still cross them): Turkish Straits, Suez Canal, Gulf of Suez and Red Sea, Panama.
- Contract (additive): `InteractionDto.Route` (`RouteDto { Exposure, Legs }`, legs carry carrier, mode, start and end share, flat rounded points). `Carrier` is now the carrier of the current leg. `GroundTruthMapper` caches route DTOs per route.
- Frontend: `maps/route.js` (pure), shipments follow the engine polyline, each leg in the dash of its mode, carrier symbol changes at each hand-off, hand-off ring (`handoff` glyph, manual row `marker:handoff`), tooltip Leg and Chain rows, dossier leg text.
- Polish: `Sensor.RandomOtherKind` no longer misclassifies into the legacy kinds. Comments removed from the routing files; Cuba's capital in the sea is handled by the anchor lookup.
- Tests: `RoutingTests.cs` (synthetic maps: truck, ship, truck; helicopter for an unlinked capital; hostile wall; no-port country; determinism; real map: ports beside land, four Soviet seas, ocean connectivity through the chokepoints, every planned route legal, running world, snapshot route and carrier parity, bare worlds). `CarrierAndSpacingTests` updated. Frontend: `route.test.js`, new cases in `describe.test.js` and `symbols.test.js`.
- NOT verified here: nothing was compiled or run (no copy of the project was made, per the working rule). Run `dotnet test` and `npm test`, then look at a shipment in the browser. The ocean connectivity theory cases are the first thing to read if anything fails: they depend on the real raster.

## Milestone 17: Cold War political logic and hunting of shipments (written; in review)
- Doctrine (`Diplomacy/Doctrine.cs`): `AidTie` (patron to client, with a reason), `ProxyFront` (name, pair, start rung, ceiling rung, reason), `SummitTie`, and forbidden pairs. Supplied by the scenario through `WorldState.Doctrine`. `MayFight` is false for forbidden pairs and for pairs with no front.
- Ladder (`Diplomacy/EscalationLadder.cs`): rungs Calm, Tension, Incident, Clash, Missile, checked every 5 ticks. A rung rises with probability 0.15 times hostility (zero at relation -0.2, full at -1.0) up to the front's ceiling. A rung falls after 60 quiet ticks or when ceasefire talks on that front succeed. Tension and incident nudge relations down, cooling nudges them up. `WorldState.Escalation` holds the state.
- Engine (`InteractionEngine`), only when a doctrine is present:
  - Clashes only on fronts at Clash or above, missiles only on fronts at Missile (ammunition 40 or more). Forbidden pairs never fight.
  - Supply flows along aid ties (patron richer than client, relation above 0), or between same-bloc partners with relation above 0.4. Talks are summits (rare), ceasefire talks on hot fronts, aid agreements, or routine contact between non-hostile pairs.
  - Planning avoids hostile land and hostile coastal waters. When no safe lane exists, aid shipments run the gauntlet with chance 0.35 through the peaceful route and say so in their cause.
  - Hunting: each tick a shipment inside a hostile zone is tracked by that country (logged once, the interceptor is ordered toward it when the move is legal) and caught with chance 0.06 + 0.08 times hostility, only if the hunter still has a warship (ships), tank or armored car (trucks) or warplane or helicopter (aircraft). The shipment ends where it was caught, 40 percent of the cargo is seized and relations drop. No dice are rolled at dispatch any more.
  - Every interaction and its log line carry a cause ("because ...").
- Without a doctrine the engine takes the old code paths and draws the same random numbers, so bare worlds are unchanged.
- `Routing/RouteNetwork.cs`: optional `waterReach`. Sea cells within reach of an owned coast belong to that country. `ZoneAt(point)` gives the country holding a point's land or waters, sea planning blocks hostile waters, `IsLegal` checks them. Default scenario reach is 4 units (two cells). Without a reach nothing changes.
- Scenario: 24 aid ties, 6 fronts (Korean peninsula, Indochina, Sino-Indian border, inner German border and Caribbean capped at Incident, Suez), 2 summit pairs. The Soviet-American and Pact-NATO ban is passed into the doctrine as forbidden pairs.
- Contract (additive): `InteractionDto.Cause` and `Hunter`, `GroundTruthSnapshot.Fronts` (`FrontDto`).
- Frontend: shipment tooltip rows `Cause` and `Hunted by` (only when sent), dossier detail "hunted by", dossier Fronts section (`frontsFor`, `rungLabel`).
- Tests: `DoctrineTests.cs` (lookups, ladder climb, ceiling, cooling, ceasefire, no front no war, forbidden pairs, causes, aid-only shipments, summits, bare worlds unchanged, hunting on a synthetic corridor, peaceful transit never intercepted, no interceptors no catch, waters, planning around hostile waters, default doctrine validity, long run on seeds 42 and 7). Frontend: `describe.test.js`.
- NOT verified here: nothing was compiled or run (no copy of the project was made, per the working rule). Run `dotnet test` and `npm test`, then open a country in the browser. Watch these first if something fails: `ScenarioTests.LongRunProducesEveryKindOfInteraction` (needs a clash and a missile within 2000 ticks on seed 42), `RoutingTests.EveryPlannedRouteOnTheRealMapIsLegal` (hostile waters now remove some ship routes, it needs 60 routes and 20 ship legs), and the hunting tests, which rely on chances in `InteractionEngine`.
- Known limits: waters are a coarse band, not real territorial seas. Interceptors are ordered to move but do not fight a visible battle. A shipment that runs the gauntlet is only tracked, never warned off by diplomacy.

## Milestone 18: ambush, conquest, paralyzed zones and secondary explosions (written; in review)
- Engine, all of it only when a doctrine is present (bare worlds keep their old code paths):
  - Paralyzed zones (`Territory/ParalyzedZone.cs`, `WorldState.Zones`): a missile that lands calls `Devastate`, which puts a patch of 10 to 18 units around the victim's capital out of use, severity 0.6 to 1.0, fading linearly to zero over 80 to 160 ticks. Severity 0.5 or more freezes the owner's units inside it (`WorldState.CanMove`) and stops the owner's shipping. Any live zone cuts the owner's income by 0.75 times its worst severity (never below 10 percent).
  - Secondary explosion: a stockpile of 150 or more goes up with chance `stock / 600` (0.1 to 0.8). The blast is 1.6 times larger and 0.2 more severe, destroys a quarter of the supplies and hits the two nearest units.
  - Ambush (`InteractionEngine.Ambush.cs`, `Units/AmbushRules.cs`, `UnitStatus.Ambushing`): every 20 ticks, on a front at Incident or above between neighbours on one landmass, each side sends its idle land combat unit (artillery excepted) nearest the rival's capital to a post just inside its own border. Up to 2 per country and 6 in the world. A unit that reaches its post takes cover. Sensors see a hidden unit with 0.2 times the normal chance. A hidden unit springs with chance 0.5 per tick on a hostile convoy whose truck leg passes within 6 units (the convoy is intercepted) or on a hostile land unit that comes that close (25 to 45 damage, 6 recoil to the ambusher), and is revealed when it does. Strikes and conquest roll on the `Chance` stream, so they never shift the main random sequence.
  - Conquest (`InteractionEngine.Conquest.cs`, `Territory/Occupation.cs`, `WorldState.Occupations`): when a clash ends between neighbours and one side has 1.15 times the other's national power, ground changes hands with chance 0.6, at most two patches per pair. The patch (radius 10 to 16) is an overlay on the raster, the raster itself is never edited. The loser's units inside it take 30 damage, the winner garrisons it with two land combat units, the loser's income drops by 10 points per patch, and land units of other countries cannot march onto held ground. When the front cools to Tension or ceasefire talks succeed, the oldest patch held for 30 ticks or more is handed back and the garrison pulls out.
- Contract (additive): `GroundTruthSnapshot.Zones` (`ZoneDto`) and `Occupations` (`OccupationDto`), both always arrays in the mapper. `UnitDto.Status` can now be `Ambushing`.
- Frontend: `drawZones` and `drawOccupations` in `maps/renderer.js` (red dashed ring with a cross whose fill fades as repairs go on, white dashed ring with a bracket and the holder's code), hover and tooltip rows, dotted outline for a unit in ambush, manual rows `marker:ambush`, `area:zone`, `area:occupation`. New in this pass: dossier sections Paralyzed ground and Occupied ground (only when there is something to show), an "in ambush" count in the dossier unit lines, and frontline events (`ambush_sprung`, `territory_seized`, `zone_paralyzed`, `secondary_explosion`) marked as hostile in the event strip.
- Polish: hidden and destroyed unit outlines were dashed with `ctx.setLineDash` before `drawSymbol`, which scales the context, so the dashes came out about `r` times too long. They now go through the `dash` argument of `drawSymbol`, which divides them back to pixels, on the map and in the manual.
- Tests: `FrontlineTests.cs` (zone severity and repair, blocking, ownership change, garrison, income, hand-back, determinism, ambush on units and convoys, peaceful neighbours, secondary blast, default-world invariants on seeds 42 and 7) plus three new snapshot tests (zones and occupations mapped, empty lists on a bare world, ambushing status mapped) and two array checks in `ApiTests`. Frontend: `describe.test.js` (zones, occupations, tooltips, ambush counts), `hud.test.js` (frontline events), `symbols.test.js` (manual rows).
- NOT verified here: nothing was compiled or run (the tools in this session can read and write files but not run `dotnet`, `node` or a browser). Run `dotnet test` and `npm test`, then look at a hot front in the browser. Watch these first if something fails: `FrontlineTests.TroopsTakeCoverInsideTheirBorderOnAHotFront` (needs a post reached within 58 ticks), `FrontlineTests.AHiddenUnitSpringsOnAHostileConvoyThatDrivesPast` (relies on chances along a 750-step run), `FrontlineTests.OccupationCutsTheLosersIncome` (assumes exactly one income step in 10 ticks) and the new `TheSnapshotCarriesLiveZonesAndOccupations`.
- Known limits: the evaluator still counts a hidden unit as detectable whenever it sits in sensor range, so recall is a little harsher than the sensors are; waters and zones are circles, not shapes that follow coastlines; a garrison does not fight a visible battle.

## Window A roadmap (milestones 15 to 19, one per push)
- 15 units and power: DONE, in review (see above).
- 16 logistics routes, hand-offs and their frontend rendering: written, in review (see above).
- 17 political logic: written, in review (see above).
- 18 ambush, conquest and paralysis: written, in review (see above).
- 19 randomized worlds and the Window A verification pass: seeded variation of powers, relations and positions so the model faces new environments.

## Still open
- Run `dotnet test` and `npm test` (frontend) on milestone 18, then look at a hot front in the browser (paralyzed ring, occupied ring, dotted ambush outlines, the two new dossier sections).
- README milestone list is old; `documentation/architecture.md` and `documentation/ai_agent.md` are not updated.
- Window B has no HTML page and ignores the reset event.
- Reward function: the evaluator (milestone 13) scores tracking only. Reward design and the learning loop come after Window A is finished.
