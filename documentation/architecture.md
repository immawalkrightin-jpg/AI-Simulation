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

## Map visual reference

`documentation/reference/map_reference.gif`: dark tilted plane, contour lines, glowing white territory outline, red focal point with arcing projection lines, red route traces, red bracket target boxes, faint grid, small telemetry labels.
