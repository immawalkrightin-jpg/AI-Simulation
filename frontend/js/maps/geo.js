// Single source of truth for display geometry.
//
// Engine space is 2000 x 1000 equirectangular: x = (lon + 180) / 360 * W, y = (90 - lat) / 180 * H.
// Display space keeps x unchanged and stretches y with a Mercator-style transform. Because the engine grid
// is equirectangular, the local y/x scale becomes 1 / cos(lat), so land shapes look right on the sphere.
// Engine distances are never changed: arcs and rings are computed in engine space and then mapped through
// toDisplay point by point. Consequence: an engine-space circle (a sensor range) is NOT a circle on screen;
// it is an ellipse stretched north-south by 1 / cos(lat). This is deliberate: the ring shows exactly what
// the engine computes.
//
// Display-space units: one unit of x is one engine unit. The equator is at the same y (H / 2) in both spaces.

export const WORLD_WIDTH = 2000;
export const WORLD_HEIGHT = 1000;
export const LAT_MIN = -60;
export const LAT_MAX = 80;

const RAD = Math.PI / 180;
// Display units per radian of longitude, so that x stays unchanged.
const R = WORLD_WIDTH / (2 * Math.PI);

const clampLat = (lat) => Math.min(LAT_MAX, Math.max(LAT_MIN, lat));
const mercator = (lat) => Math.log(Math.tan(Math.PI / 4 + (clampLat(lat) * RAD) / 2));

export function latOf(y) {
  return 90 - (y / WORLD_HEIGHT) * 180;
}

export function toDisplay(x, y) {
  return { x, y: WORLD_HEIGHT / 2 - R * mercator(latOf(y)) };
}

export function fromDisplay(dx, dy) {
  const m = (WORLD_HEIGHT / 2 - dy) / R;
  const lat = (2 * Math.atan(Math.exp(m)) - Math.PI / 2) / RAD;
  return { x: dx, y: ((90 - lat) / 180) * WORLD_HEIGHT };
}

// Display extent of the drawable world (latitudes outside LAT_MIN..LAT_MAX are clamped).
export const DISPLAY_BOUNDS = (() => {
  const top = toDisplay(0, 0).y; // lat 90, clamped to LAT_MAX
  const bottom = toDisplay(0, WORLD_HEIGHT).y; // lat -90, clamped to LAT_MIN
  return { minX: 0, maxX: WORLD_WIDTH, minY: top, maxY: bottom };
})();

// Display rectangle of an engine rectangle, for framing.
export function displayRect(x0, y0, x1, y1) {
  const a = toDisplay(x0, y0);
  const b = toDisplay(x1, y1);
  return { minX: Math.min(a.x, b.x), maxX: Math.max(a.x, b.x), minY: Math.min(a.y, b.y), maxY: Math.max(a.y, b.y) };
}

// Named framings, in engine coordinates.
export const FRAMES = {
  // The whole drawable world.
  world: { x0: 0, y0: 0, x1: WORLD_WIDTH, y1: WORLD_HEIGHT, clip: true },
  // Europe and the Mediterranean: Britain to Moscow, Scandinavia to Egypt.
  theatre: { x0: 940, y0: 105, x1: 1255, y1: 348 }
};

// Point-in-polygon with the even-odd rule across all rings of one entity. Rings are flat [x0, y0, x1, y1, ...]
// arrays in engine space, so picking works on engine coordinates (unproject, then fromDisplay).
export function pointInRings(rings, x, y) {
  let inside = false;
  for (const ring of rings) {
    const count = ring.length / 2;
    for (let i = 0, j = count - 1; i < count; j = i++) {
      const xi = ring[i * 2];
      const yi = ring[i * 2 + 1];
      const xj = ring[j * 2];
      const yj = ring[j * 2 + 1];
      if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) {
        inside = !inside;
      }
    }
  }
  return inside;
}

// Entity whose rings contain the engine point, or null. Entities with an empty id (unaffiliated land) are
// returned too, so the caller can tell "land with no id" from "open ocean".
export function entityAt(map, x, y) {
  if (!map) {
    return null;
  }
  for (const entity of map.entities) {
    if (entity.name === "ATA") {
      continue;
    }
    if (pointInRings(entity.rings, x, y)) {
      return entity;
    }
  }
  return null;
}
