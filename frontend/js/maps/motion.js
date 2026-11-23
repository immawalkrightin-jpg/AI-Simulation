// Pure motion helpers: which carrier a shipment uses and which way a symbol faces. No canvas, no DOM.

// What the map shows when the engine names no carrier. The frontend never guesses beyond this table:
// in particular it never invents a helicopter.
export const MODE_CARRIER = { Ground: "Truck", Naval: "CargoShip", Air: "Transport" };

// Carrier symbol name for a shipment: the engine's own `carrier` when present, otherwise the mode default.
export function carrierOf(item) {
  if (item.carrier) {
    return item.carrier;
  }
  return MODE_CARRIER[item.mode] || null;
}

// True when the carrier shown is the mode default and not something the engine reported.
export function carrierIsDefault(item) {
  return !item.carrier;
}

// Heading in radians (0 = up the screen, clockwise positive) of the screen vector a -> b, or null for a
// zero-length vector.
export function screenHeading(a, b) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  if (Math.abs(dx) < 1e-9 && Math.abs(dy) < 1e-9) {
    return null;
  }
  return Math.atan2(dx, -dy);
}

// Heading of a unit on screen. `project(x, y, z)` maps engine coordinates to the screen. The direction is
// the vector to the destination while there is a distinct one, otherwise the last movement (`facing`, an
// engine-space vector). Null when the unit has never moved.
export function unitHeading(project, x, y, unit, facing = null) {
  let dx = null;
  let dy = null;
  if (unit.destinationX != null && unit.destinationY != null) {
    dx = unit.destinationX - x;
    dy = unit.destinationY - y;
  }
  if (dx === null || Math.hypot(dx, dy) < 1e-6) {
    if (!facing || Math.hypot(facing.x, facing.y) < 1e-6) {
      return null;
    }
    dx = facing.x;
    dy = facing.y;
  }
  const length = Math.hypot(dx, dy);
  const from = project(x, y, 0);
  const to = project(x + (dx / length) * 4, y + (dy / length) * 4, 0);
  return screenHeading(from, to);
}
