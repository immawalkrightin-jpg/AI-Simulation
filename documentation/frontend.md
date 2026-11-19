# Frontend: Window A

Window A renders the authoritative ground truth. It reads only the ground-truth channel (`/api/ground-truth`, `/api/simulation`, `/hubs/ground-truth`) with the ground-truth key. It never touches the observation channel.

## Structure

- `js/connection.js`: initial snapshot over REST, live snapshots over SignalR, reconnect loop, simulation controls.
- `js/maps/projection.js`: tilted-plane projection with mild perspective, auto-fitted to the viewport.
- `js/maps/terrain.js`: seeded procedural heightfield and marching-squares contour lines, used for relief and elevation lookup.
- `js/maps/renderer.js`: canvas drawing of grid, land, contours, coastline glow, sensor coverage, standing relations, operations, units, countries, selection reticle, plus pan, zoom and picking.
- `js/units/symbols.js`: one symbol per unit kind.
- `js/telemetry/`: telemetry panel and entity inspector.
- `assets/maps/coldwar.json`: generated country geometry, loaded at start. If missing, the renderer falls back to the procedural coastline.

## Map data

`data/world/build_map.py` builds `frontend/assets/maps/coldwar.json` from Natural Earth 110m countries. It merges the Soviet republics into one entity, merges Czechoslovakia and Yugoslavia, splits Germany into East and West and Vietnam into North and South. Coordinates use the same equirectangular projection as the scenario, so capitals line up with the land.

```
pip install shapely
python data/world/build_map.py
```

## Line language

Standing relations are faint and ongoing operations are bright.

| Line | Meaning |
|---|---|
| Faint grey solid | Allied relation |
| Faint red dotted arc | Hostile relation |
| White dashed arc with hollow diamond | Negotiation in progress |
| White solid arc with filled diamond | Negotiation concluded |
| White solid line with square head | Supply shipment, ground |
| White dotted line with square head | Supply shipment, naval |
| White long-dash arc with square head | Supply shipment, air |
| Red line ending in a cross | Shipment intercepted or talks collapsed |
| Red dashed lines converging on a pulsing bracket | Clash |
| Red high arc with glowing head | Missile in flight; ring on the target warns of arrival |
| Expanding red ring with bracket | Missile impact |
| Red cross on a unit | Unit destroyed |

Finished operations fade over eight ticks. Capital markers are red for the model country and tinted by bloc for the rest: red for East, white for West, grey for non-aligned.

## Running

Start the API, generate the map once, then serve the `frontend` folder on an allowed origin port:

```
cd simulation
dotnet run --project Simulation.API
python data/world/build_map.py
cd frontend
python -m http.server 5500
```

Open `http://localhost:5500`. The API address and key are in `js/config.js`.

## Interaction

- Drag to pan, scroll to zoom, click a unit, sensor or country to inspect it.
- Unit ids, country names and cargo amounts appear when zoomed in or selected.
- Floating controls pause, resume and step the simulation.

## Known limits

- The terrain relief is decorative and generated on the client from the country positions; the engine has no terrain yet.
- Supply and missile routes are straight arcs between capitals; the engine has no routing.
- Event records carry no position, so events appear in the log only.
