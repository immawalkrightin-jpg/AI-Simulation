# Frontend: Window A

Window A renders the authoritative ground truth. It reads only the ground-truth channel (`/api/ground-truth`, `/api/simulation`, `/hubs/ground-truth`) with the ground-truth key. It never touches the observation channel.

The map is a showcase of an AI's decisions, observed by a human: understand the world at a glance, then open one country to see what it contains.

## Structure

- `js/connection.js`: initial snapshot over REST, live snapshots over SignalR, reconnect loop, simulation controls (pause, resume, step, speed). Reports the clock state through `onPaused` and `onStatus`.
- `js/main.js`: wires the renderer to the panels (dossier, inspector, tooltip, breadcrumb, mini-legend, manual, HUD) and reserves the panel inset for framing.
- `js/theme.js`: colour roles, defined once.
- `js/maps/geo.js`: single source of truth for display geometry. `toDisplay` / `fromDisplay`, named framings (`world`, `theatre`), point-in-polygon picking (`pointInRings`, `entityAt`).
- `js/maps/projection.js`: perspective camera over a ground plane (`project`, `unproject`, `flyTo`, zoom to cursor, pan and zoom clamps, panel inset, camera version counter).
- `js/maps/view.js`: pure view logic. `isExternal` / `classifyExternal` (what is visible at world level) and `countryFrame` (how a country is framed). No canvas, no DOM.
- `js/maps/labels.js`: greedy label placement with eight offsets and fade with hysteresis. Pure.
- `js/maps/cluster.js`: pixel-distance clustering and the spread layout. Pure.
- `js/maps/motion.js`: carrier choice and headings. Pure.
- `js/maps/land.js`: builds the projected land paths. `js/maps/renderer.js`: canvas drawing, input and picking.
- `js/units/symbols.js`: every silhouette, `drawSymbol`, and the `LEGEND` list. `js/units/markers.js`: flat glyphs, dash patterns, strength bar, cluster badge. `js/units/legend.js`: the symbol manual and the in-view legend, drawn with the same calls as the map.
- `js/telemetry/`: `describe.js` (pure tooltip and dossier content), `dossier.js`, `inspector.js`, `hud.js` (HUD and event strip), `tooltip.js`, `dom.js`.
- `assets/maps/coldwar.json`: generated country geometry, loaded at start. Without it the map shows no land and picking by area is unavailable.

## Camera and display transform

Engine space stays 2000 x 1000 equirectangular. Everything that draws world geometry goes through `projection.projectWorld(x, y, z)`, which is `toDisplay` (geo.js) followed by the camera.

**Display transform.** x is unchanged. y goes through a Mercator stretch (latitude clamped to -60..80, equator at the same y in both spaces). The engine grid has equal units per degree in both axes, so the local y/x scale is 1 / cos(lat): land shapes look right and Europe is no longer stretched east-west.

**Consequence for ranges.** Arcs and rings are computed in engine space and then mapped point by point, so they show exactly what the engine computes. A sensor range is a circle in engine space and therefore an ellipse on screen, stretched north-south by 1 / cos(lat) (about 1.5 at 50 N). Only land is conformal.

**Camera.** Perspective camera orbiting a centre point on the ground. Tilt is measured from vertical: default 47 degrees, range 0 (top-down) to 60. Yaw defaults to 0 (north up); a compass is drawn only if yaw is non-zero. Near ground (bottom of the screen) is larger than far ground. The camera distance scales with the viewport and 1 / zoom, so the largest-to-smallest scale ratio depends only on tilt (about 1.3 at 47 degrees, 1.5 at 60; covered by a unit test). `z` is altitude in display units and is used only for aircraft, missile arcs and the hero beam. Ground symbols use z = 0.

**Controls.** Drag pans by keeping the grabbed ground point under the cursor. Scroll zooms toward the cursor. Floating buttons give World, Theatre, zoom, flat/oblique tilt, reset and the manual. Any user input interrupts a flight. Pan is clamped to the world plus a margin, and zoom is clamped. The default view is the Theatre preset (Europe). `setInset` tells the camera how much room panels take, so nothing important is centred under one.

## View modes

The view state is `{ mode: "world" | "country", focus: countryId | null }`.

**World mode** shows only external information: land, borders, bloc patterns, graticule, one capital node per country with a collision-managed code, relations and operations (negotiations, shipments with carrier symbols, clashes, missiles). It shows no interior symbols: no units, sensors, installations, radar sweeps or per-country supplies.

**Expeditionary exception.** A unit is external, and so visible at world level, when it is outside its owner's polygon and farther than 45 engine units from its capital, or is heading to such a place. The set is computed once per snapshot (`classifyExternal`), not per frame.

**Country mode** (click a country, picked by polygon): the camera flies to the bounds of the country's units, sensors and capital (minimum span 120 engine units, never larger than the country's bounding box), other countries dim to 40%, only operations touching the focus stay visible, and the interior layer fades in over 300 ms: units, sensors with range rings, the command centre, foreign units with a dashed ring and owner code. The dossier opens on the right. Esc, clicking open ocean, the breadcrumb or "Back to world" return to the previous framing. Clicking another country flies straight to it. Scroll zoom never reveals interiors by itself. Clicking unaffiliated land only flashes it.

## Colour roles

Defined in `js/theme.js`, used only with these meanings:

| Colour | Meaning |
|---|---|
| Red | The model country (hero), hostile or destructive events, alerts (lost connection). Nothing else. |
| White | Active and focused information. Selection is a white bracket with a soft pulse. |
| Gray | Background and inactive things. Radar sweeps are neutral pale. |
| Cool teal | Sensor coverage only. |

Bloc is shown by pattern, never by colour: East = fine diagonal hatch, West = dot hatch, Non-aligned = plain faint fill. Low strength and low stocks are an outlined bar with a tick on the low line, never red.

## Symbol grammar

- **Shape is what it is.** Every unit, sensor, carrier and installation has its own silhouette in `symbols.js`. Colour never carries a meaning that shape already carries.
- **Fixed pixel size**, not perspective-scaled: 9 px radius in country view, 6 px at world level, stroke 1.5 px, a dark backing disc under each symbol.
- **Orientation.** Units head along the vector to their destination, otherwise their last movement. Shipments follow the arc tangent. Top-view shapes (Fighter, Transport, Helicopter, Missile) rotate; side-view shapes mirror.
- **State is on the symbol.** Destroyed: broken outline and a red cross. Moving: a heading tick and a short trail. Strength: an outlined bar at least as wide as the symbol, with a tick when low.
- **No ids by default.** Labels appear on hover or selection, with a halo.
- **Declutter.** Symbols within 18 px cluster into a count badge. A click spreads the members on a ring with leader lines to their true positions (display offsets are visual only). Country labels are placed greedily in priority order (selected, hero, capitals) and fade with hysteresis.

### Carrier rule

`InteractionDto` has an optional `Carrier` (`Truck`, `CargoShip`, `Transport`, `Helicopter`). The WorldEngine has no carrier concept, so `GroundTruthMapper.CarrierOf` derives it deterministically from `Mode` (Ground: Truck, Naval: CargoShip, Air: Transport). The frontend uses `item.carrier` when present, otherwise the same mode default, and never guesses a helicopter. The tooltip says "unknown" when a shipment has no carrier at all.

## Manual, tooltips and panels

- **Symbol manual** (`?` button or `L`): grouped into Units, Carriers, Sensors and installations, Lines, Markers, Colour roles. Every row is drawn with the same `drawSymbol` and marker calls the map uses (`MANUAL_ROWS` in `legend.js`), so it cannot drift from the map.
- **In this view** (bottom right, country view only): only the symbol types currently on screen.
- **Tooltips** on hover for every symbol: kind, owner, id, status, strength; for shipments carrier, mode, cargo, route and status.
- **Dossier**: name and bloc, labelled Supplies and Ammunition bars (relative to the peak seen since load), units grouped by kind with counts and status, active operations, most hostile and closest relations, back button.
- **HUD** (always visible): link state, tick, clock, running or paused, Pause, Step 1, Step 10, speed (`POST /api/simulation/speed?value=`). The event log is a collapsible strip showing the last three events.

## Performance

Projected land and graticule paths are cached and rebuilt only when the camera version or the map changes, never per frame. The hero glow is pre-rendered to an offscreen canvas. `shadowBlur` is not used in the per-frame path. Animated elements (hero pulse, active operations, selection) are drawn in a separate pass. `prefers-reduced-motion` freezes pulses and sweeps and makes camera moves instant. Press `P` for an on-screen fps and draw-time overlay. No frame-time figure is claimed in this document because none has been measured yet.

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
| Faint gray solid | Allied relation |
| Faint red dotted arc | Hostile relation |
| White dashed arc with hollow diamond | Negotiation in progress |
| White solid arc with filled diamond | Negotiation concluded |
| White solid line, truck head | Supply shipment, ground |
| White dotted line, cargo ship head | Supply shipment, naval |
| White long-dash arc, transport plane head | Supply shipment, air |
| Red line ending in a cross | Shipment intercepted or talks collapsed |
| Red dashed lines converging on a pulsing bracket | Clash |
| Red high arc with missile head | Missile in flight; ring on the target warns of arrival |
| Expanding red ring with bracket | Missile impact |

Finished operations fade over eight ticks.

## Running

```
cd simulation
dotnet run --project Simulation.API
python data/world/build_map.py
cd frontend
python -m http.server 5500
```

Open `http://localhost:5500`. The API address and key are in `js/config.js`. Tests: `cd frontend && node --test` (no dependencies).

## Known limits

- The engine has no terrain, so the map has none.
- Supply and missile routes are straight arcs between capitals; the engine has no routing.
- Event records carry no position, so events appear in the log only.
- The engine has no installations (ports, bases, depots), so none are drawn. A possible additive `InstallationDto { Id, Owner, Kind, X, Y }` (kinds Command, Airbase, Port, Depot) is proposed but not implemented; the scenario's ports table would be its first source.
- Shipment carriers are derived from the transport mode, not reported by the engine.
