import { pointInRings } from "./geo.js";

// Pure view logic: which things are visible at world level, and how a country is framed.
// No canvas and no DOM, so it can be unit-tested.

// A unit farther than this from its own capital (and outside its own territory) is expeditionary.
export const EXTERNAL_RADIUS = 45;
export const MIN_SPAN = 120;

const far = (x, y, country, radius) => Math.hypot(x - country.x, y - country.y) > radius;

// "External" units are shown at world level as small symbols, so movements between countries stay visible.
// External means: outside the owner's territory and farther than `radius` from its capital, or heading to a
// destination that is outside the owner's territory and farther than `radius` from its capital.
// `rings` are the owner's polygon rings in engine space (empty or null when there is no map).
export function isExternal(unit, country, rings, radius = EXTERNAL_RADIUS) {
  if (!country || unit.status === "Destroyed") {
    return false;
  }
  const has = rings && rings.length > 0;
  const outside = (x, y) => !(has && pointInRings(rings, x, y)) && far(x, y, country, radius);
  if (outside(unit.x, unit.y)) {
    return true;
  }
  return unit.destinationX != null && unit.destinationY != null && outside(unit.destinationX, unit.destinationY);
}

// Set of external unit ids for a snapshot. Called once per snapshot, never per frame.
export function classifyExternal(snapshot, ringsOf) {
  const byId = new Map(snapshot.countries.map((country) => [country.id, country]));
  const external = new Set();
  for (const unit of snapshot.units) {
    if (isExternal(unit, byId.get(unit.owner), ringsOf(unit.owner))) {
      external.add(unit.id);
    }
  }
  return external;
}

export function ringsBounds(rings) {
  let x0 = Infinity;
  let y0 = Infinity;
  let x1 = -Infinity;
  let y1 = -Infinity;
  for (const ring of rings || []) {
    for (let i = 0; i < ring.length; i += 2) {
      x0 = Math.min(x0, ring[i]);
      x1 = Math.max(x1, ring[i]);
      y0 = Math.min(y0, ring[i + 1]);
      y1 = Math.max(y1, ring[i + 1]);
    }
  }
  return x0 === Infinity ? null : { x0, y0, x1, y1 };
}

// Engine-space rectangle to frame when a country is opened: the bounds of its capital, units and sensors,
// at least MIN_SPAN wide and high, and never larger than the country's own bounding box (so a huge country
// is framed on its content, not on its whole territory).
export function countryFrame(country, units, sensors, territory = null, minSpan = MIN_SPAN) {
  const points = [{ x: country.x, y: country.y }];
  for (const unit of units) {
    if (unit.status !== "Destroyed") {
      points.push({ x: unit.x, y: unit.y });
    }
  }
  for (const sensor of sensors) {
    points.push({ x: sensor.x, y: sensor.y });
  }
  let x0 = Math.min(...points.map((p) => p.x));
  let x1 = Math.max(...points.map((p) => p.x));
  let y0 = Math.min(...points.map((p) => p.y));
  let y1 = Math.max(...points.map((p) => p.y));
  const fit = (lo, hi, maxSpan) => {
    const span = Math.min(Math.max(hi - lo, minSpan), Math.max(maxSpan, minSpan));
    const mid = (lo + hi) / 2;
    return [mid - span / 2, mid + span / 2];
  };
  const maxW = territory ? territory.x1 - territory.x0 : Infinity;
  const maxH = territory ? territory.y1 - territory.y0 : Infinity;
  [x0, x1] = fit(x0, x1, maxW);
  [y0, y1] = fit(y0, y1, maxH);
  return { x0, y0, x1, y1 };
}
