# Frontend: Window A

Window A renders the authoritative ground truth. It reads only the ground-truth channel (`/api/ground-truth`, `/api/simulation`, `/hubs/ground-truth`) with the ground-truth key. It never touches the observation channel.

## Structure

- `js/connection.js`: initial snapshot over REST, live snapshots over SignalR, reconnect loop, simulation controls.
- `js/maps/projection.js`: tilted-plane projection with mild perspective and elevation.
- `js/maps/terrain.js`: seeded procedural heightfield, marching-squares contour lines, coastline, elevation lookup.
- `js/maps/renderer.js`: canvas drawing of grid, contours, sensor coverage, relation arcs, units, countries, selection reticle, plus pan, zoom and picking.
- `js/units/symbols.js`: one symbol per unit kind.
- `js/telemetry/`: telemetry panel and entity inspector.

## Running

Start the API, then serve the `frontend` folder on an allowed origin port:

```
cd simulation
dotnet run --project Simulation.API
cd frontend
python -m http.server 5500
```

Open `http://localhost:5500`. The API address and key are in `js/config.js`.

## Interaction

- Drag to pan, scroll to zoom, click a unit, sensor or country to inspect it.
- Floating controls pause, resume and step the simulation.

## Known limits

- The terrain is decorative and generated on the client from the country positions; the engine has no terrain yet.
- Event records carry no position, so events appear in the log only.
