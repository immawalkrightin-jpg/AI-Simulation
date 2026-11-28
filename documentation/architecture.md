# Architecture

## Boundaries

- WorldEngine (C#) holds ground truth and is the only writer of world state.
- Simulation.API exposes two separate contracts: `ground-truth` for Window A and `observations` for the AI and Window B. The AI client is never given the ground-truth contract.
- ml/ consumes observations only and returns decisions through the decision contract.
- frontend/ renders; it holds no simulation or learning logic.

## Data flow

WorldEngine tick -> Sensors produce Observations -> API observation stream -> ml/agents -> ml/central_intelligence -> Decisions -> API -> WorldEngine applies -> outcomes recorded to data/events -> evaluation compares decisions to ground truth.

## Observation boundary

Sensors read WorldState and emit Observation records. Observation carries no unit identity, no unit reference and no world reference; classification and ownership are optional and may be wrong or absent. Everything on the AI side consumes IObservationSource only. SensorNetwork uses its own seeded random generator so sensing never changes world history.

## API channels

Default address: `http://localhost:5080`. Two access keys are configured under `Simulation` in `appsettings.json`; the development values must be overridden outside local use.

| Channel | REST | SignalR hub | Key | Message |
|---|---|---|---|---|
| Ground truth (Window A, operator) | `GET /api/ground-truth/snapshot` | `/hubs/ground-truth` | `GroundTruthKey` | `snapshot` |
| Simulation control (operator) | `GET /api/simulation/status`, `POST /api/simulation/pause`, `POST /api/simulation/resume`, `POST /api/simulation/step?count=N` | none | `GroundTruthKey` | none |
| Observations (AI, Window B) | `GET /api/observations?after=SEQ` | `/hubs/observations` | `AiKey` | `observations` |

Keys are sent as `X-Access-Key`, `Authorization: Bearer <key>`, or `access_token` query parameter (SignalR WebSocket). Each key opens only its own channel, so the AI key cannot read ground truth and the ground-truth key cannot read the AI observation stream.

Contracts live in `Simulation.API/Contracts`, in two separate namespaces: `Contracts.GroundTruth` and `Contracts.Observations`. A test verifies the observation contracts never reference ground-truth or engine world types.

Observations are drained from the sensor network once per tick, sequenced, kept in a bounded buffer of 5000 and pushed to the observation hub; late or reconnecting clients catch up through the `after` cursor.

## World scenario

`Simulation.API/Scenarios/DefaultScenario.cs` builds the Cold War world: 26 countries in three blocs (East 11, West 10, non-aligned 5) with the Soviet Union as the model country, starting relations by bloc plus per-pair overrides, units per country and a Soviet-only sensor network. Pairs that must never fight directly (Soviet Union against any Western country, United States against the Eastern European allies) are passed to the interaction engine and can only interact through diplomacy and supply.

### Seeded variation

Seed 42 (`DefaultScenario.HistoricalSeed`, also the server default) builds the historical set-up exactly. Every other seed builds a variation of it through `Scenarios/ScenarioVariation.cs`, which draws from its own random stream:

| What varies | Limit |
|---|---|
| Supplies and ammunition per country | up to 10% either way |
| Forces per country | one infantry unit more or fewer, never the last one |
| Relations | up to 0.08 either way, never across a value the engine or doctrine decides on |
| Land and air positions | turned around the country's anchor and drawn in to 80% to 100% of the radius |
| Ship positions | up to 1.5 units around the berth |

Countries, blocs, capitals, ports, aid ties, fronts, summits, forbidden pairs and sensors are the same on every seed. The order of power (superpowers, regional powers, small states) holds on every seed. National power is never set directly; it follows from the varied forces and stockpiles. `RandomizedWorldTests.cs` checks these limits and runs the Window A invariants on the historical seed and on varied ones.

## Interactions

`WorldEngine/Interactions` spawns and resolves four kinds of operation between countries: negotiation, supply shipment (ground, naval or air), clash and missile strike. Each carries a start tick, a due tick and a planned outcome fixed at spawn from the seeded generator, so history is reproducible. Outcomes move relations, supplies, ammunition and unit strength, and relations drift back toward their starting values so the world does not collapse into permanent war. Finished operations stay visible for a fade window and are then pruned.

## Routing

`WorldEngine/Routing` plans supply shipments over the territory raster. `RouteNetwork` holds the ports (each snapped to a water cell beside its owner's land), a few shipping-only canal cuts and an A* planner for three media: sea (ships), land (trucks, with a surcharge on foreign land) and air (cargo planes and helicopters). Land of countries hostile to the sender or the receiver is never crossed. Path smoothing never spends more distance in third-country, free or canal ground than the searched path did, and the truck runs between a capital and its own port stay on home ground whenever a home route exists. A naval shipment is a `ShipmentRoute` of legs: truck to the dock, cargo ship through the sea, then truck onward, or a helicopter when the capital has no land link to its port. `PlanBest` tries the preferred mode, then the others. Results are cached per (medium, start, goal, owners, hostile set). Worlds without a territory or a network (`WorldState.Routes` null) keep direct capital-to-capital shipments. The route is part of the ground-truth contract (`InteractionDto.Route`); the frontend only draws it.

## Frontline state

When a doctrine is present, three pieces of state sit on `WorldState` next to the interactions, and none of them edits the territory raster:

- `Zones` (`ZoneBook`): paralyzed patches left by a missile that lands. Severity fades linearly to zero at the repair tick. At 0.5 or more the owner's units inside cannot move and its shipping stops; any live zone cuts its income.
- `Occupations` (`OccupationBook`): patches of one country's land that a neighbour holds after winning a clash by a power margin. The raster still says who owns the land; the book says who holds it. Land units of other countries cannot enter held ground, the holder garrisons it, and it is handed back when the front cools or ceasefire talks succeed. A march on held ground may use only the holder's own land and held land, never water (`WorldState.HeldPath`). At hand-back, units left on returned ground are moved home and units whose orders now lead onto it are halted.
- Units in ambush (`UnitStatus.Ambushing`): hidden land combat units waiting just inside their border on a hot front. Sensors read them with a fifth of the normal detection chance (`AmbushRules.Concealment`).

Ambushes and conquest roll on `WorldState.Chance`, a second random stream, so they never move the draws of the main stream. Both snapshot lists (`Zones`, `Occupations`) are part of the ground-truth contract and are never sent on the observation channel; the AI sees a hidden unit only the way it sees any other, through its sensors.
