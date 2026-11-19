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

## Map visual reference

`documentation/reference/map_reference.gif`: dark tilted plane, contour lines, glowing white territory outline, red focal point with arcing projection lines, red route traces, red bracket target boxes, faint grid, small telemetry labels.
