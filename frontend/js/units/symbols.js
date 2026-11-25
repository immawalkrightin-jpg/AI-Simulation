// Every shape the map draws, as outline silhouettes in a unit circle (radius 1, centre 0,0).
//
// Ground and sea shapes are side views and mirror to face the way they move. Aircraft, helicopters and
// missiles are top views and rotate to their heading. `heading` is radians, 0 meaning "up the screen".
//
// The map and the symbol manual both draw through drawSymbol, so what the manual shows is what the map draws.
// Shape carries the meaning. Colour never does.

const TOP_VIEW = new Set(["Fighter", "Transport", "Helicopter", "Missile"]);

// Shapes that fly. They are drawn slightly above the ground and rotate to their heading.
const AIRBORNE = new Set(["Fighter", "Transport", "Helicopter"]);

// Engine kind -> shape. Warplane and the legacy Air share the fighter, CargoPlane the transport, and the
// generic kinds from before milestone 15 keep the shape of their specific successor.
const ALIASES = {
  Air: "Fighter",
  Warplane: "Fighter",
  CargoPlane: "Transport",
  SupplyConvoy: "Truck",
  Naval: "Warship",
  Armor: "Tank"
};

// Symbol rows of the manual, in order. `id` is what the renderer reports as "present in this view".
// Lines, markers and colour roles are added in legend.js.
export const LEGEND = [
  { id: "unit:Infantry", group: "Units", kind: "Infantry", label: "Infantry", meaning: "Infantry unit." },
  { id: "unit:Tank", group: "Units", kind: "Tank", label: "Tank", meaning: "Tracked armored fighting unit." },
  { id: "unit:ArmoredCar", group: "Units", kind: "ArmoredCar", label: "Armored car", meaning: "Wheeled armored unit, lighter and faster than a tank." },
  { id: "unit:Artillery", group: "Units", kind: "Artillery", label: "Artillery", meaning: "Artillery unit." },
  { id: "unit:Warship", group: "Units", kind: "Warship", label: "Warship", meaning: "Combat ship or naval formation." },
  { id: "unit:Fighter", group: "Units", kind: "Fighter", label: "Warplane", meaning: "Combat aircraft. Drawn slightly above the ground." },
  { id: "unit:Helicopter", group: "Units", kind: "Helicopter", label: "Helicopter", meaning: "Combat helicopter. Drawn slightly above the ground." },
  { id: "unit:Truck", group: "Carriers", kind: "Truck", label: "Truck unit", meaning: "Carrier unit on land. Moves cargo and does not fight." },
  { id: "unit:CargoShip", group: "Carriers", kind: "CargoShip", label: "Cargo ship unit", meaning: "Carrier unit at sea. Moves cargo and does not fight." },
  { id: "unit:Transport", group: "Carriers", kind: "Transport", label: "Cargo plane unit", meaning: "Carrier unit in the air. Moves cargo and does not fight." },
  { id: "carrier:Truck", group: "Carriers", kind: "Truck", label: "Truck", meaning: "Supply shipment by ground." },
  { id: "carrier:CargoShip", group: "Carriers", kind: "CargoShip", label: "Cargo ship", meaning: "Supply shipment by sea." },
  { id: "carrier:Transport", group: "Carriers", kind: "Transport", label: "Transport plane", meaning: "Supply shipment by air." },
  { id: "carrier:Helicopter", group: "Carriers", kind: "Helicopter", label: "Helicopter", meaning: "Shipment by helicopter, from a dock to a capital with no land link." },
  { id: "sensor:Radar", group: "Sensors and installations", kind: "Radar", label: "Radar", meaning: "Radar station. The dashed ring is its range." },
  { id: "sensor:Optical", group: "Sensors and installations", kind: "Optical", label: "Optical", meaning: "Optical sensor. The dashed ring is its range." },
  { id: "sensor:Sonar", group: "Sensors and installations", kind: "Sonar", label: "Sonar", meaning: "Sonar. The dashed ring is its range." },
  { id: "installation:Command", group: "Sensors and installations", kind: "Command", label: "Command centre", meaning: "Capital of the focused country." },
  { id: "marker:missile", group: "Markers", kind: "Missile", label: "Missile", meaning: "Missile in flight." }
];

function poly(ctx, points) {
  points.forEach(([x, y], i) => (i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y)));
  ctx.closePath();
}

// Builds a left-right symmetric outline from the right half, listed from the nose down to the tail axis.
function mirrored(ctx, half) {
  const left = half
    .slice(1, -1)
    .reverse()
    .map(([x, y]) => [-x, y]);
  poly(ctx, [...half, ...left]);
}

function line(ctx, x1, y1, x2, y2) {
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
}

function circle(ctx, x, y, radius) {
  ctx.moveTo(x + radius, y);
  ctx.arc(x, y, radius, 0, Math.PI * 2);
}

// An arc as its own sub-path, so no stray line joins it to the previous point.
function arcPath(ctx, x, y, radius, from, to) {
  ctx.moveTo(x + Math.cos(from) * radius, y + Math.sin(from) * radius);
  ctx.arc(x, y, radius, from, to);
}

const SHAPES = {
  Infantry(ctx) {
    ctx.rect(-0.8, -0.8, 1.6, 1.6);
    line(ctx, -0.8, -0.8, 0.8, 0.8);
    line(ctx, 0.8, -0.8, -0.8, 0.8);
  },

  Tank(ctx) {
    poly(ctx, [[-0.95, 0.5], [-0.8, 0.25], [0.7, 0.25], [0.85, 0.5], [0.7, 0.8], [-0.8, 0.8]]);
    ctx.moveTo(-0.4, 0.25);
    poly(ctx, [[-0.4, 0.25], [-0.3, -0.15], [0.35, -0.15], [0.5, 0.25]]);
    line(ctx, 0.45, -0.05, 1.2, -0.05);
  },

  Artillery(ctx) {
    circle(ctx, -0.15, 0.55, 0.36);
    poly(ctx, [[-0.45, 0.12], [0.15, 0.12], [0.15, 0.4], [-0.45, 0.4]]);
    line(ctx, 0.1, 0.2, 1.15, -0.7);
    line(ctx, -0.15, 0.55, -0.95, 0.85);
  },

  // Wheeled hull with a small turret: lower and lighter than the tracked tank.
  ArmoredCar(ctx) {
    poly(ctx, [[-0.95, 0.28], [-0.8, 0.02], [0.6, 0.02], [0.95, 0.28], [0.95, 0.5], [-0.95, 0.5]]);
    poly(ctx, [[-0.2, 0.02], [-0.1, -0.35], [0.35, -0.35], [0.45, 0.02]]);
    line(ctx, 0.4, -0.2, 1.15, -0.2);
    circle(ctx, -0.6, 0.62, 0.2);
    circle(ctx, 0, 0.62, 0.2);
    circle(ctx, 0.6, 0.62, 0.2);
  },

  Truck(ctx) {
    ctx.rect(-1, -0.5, 1.1, 0.9);
    poly(ctx, [[0.1, -0.1], [0.55, -0.1], [1, 0.25], [1, 0.4], [0.1, 0.4]]);
    circle(ctx, -0.6, 0.58, 0.24);
    circle(ctx, 0.6, 0.58, 0.24);
  },

  Warship(ctx) {
    poly(ctx, [[-1, 0.05], [1, 0.05], [0.72, 0.62], [-0.82, 0.62]]);
    poly(ctx, [[-0.3, 0.05], [-0.3, -0.3], [0.15, -0.3], [0.3, 0.05]]);
    line(ctx, -0.05, -0.3, -0.05, -0.75);
    line(ctx, 0.55, 0.05, 0.55, -0.12);
    line(ctx, 0.55, -0.1, 1.05, -0.1);
  },

  CargoShip(ctx) {
    poly(ctx, [[-1, 0.05], [1, 0.05], [0.72, 0.62], [-0.82, 0.62]]);
    poly(ctx, [[-0.88, 0.05], [-0.88, -0.5], [-0.5, -0.5], [-0.5, 0.05]]);
    line(ctx, -0.69, -0.5, -0.69, -0.72);
    for (const x of [-0.4, 0, 0.4]) {
      ctx.rect(x, -0.32, 0.4, 0.37);
    }
  },

  Fighter(ctx) {
    mirrored(ctx, [[0, -1], [0.09, -0.55], [0.1, -0.15], [0.95, 0.5], [0.95, 0.68], [0.13, 0.45], [0.14, 0.8], [0.42, 1], [0, 0.9]]);
  },

  Transport(ctx) {
    mirrored(ctx, [[0, -1], [0.11, -0.85], [0.13, -0.1], [1.05, -0.02], [1.05, 0.2], [0.13, 0.24], [0.14, 0.72], [0.46, 0.82], [0.46, 0.95], [0.1, 0.95], [0, 1]]);
  },

  Helicopter(ctx) {
    ctx.moveTo(0.28, -0.1);
    ctx.ellipse(0, -0.1, 0.28, 0.5, 0, 0, Math.PI * 2);
    line(ctx, 0, 0.4, 0, 1);
    line(ctx, -0.22, 1, 0.22, 1);
    circle(ctx, 0, -0.1, 0.95);
    line(ctx, -0.67, -0.77, 0.67, 0.57);
    line(ctx, 0.67, -0.77, -0.67, 0.57);
  },

  Missile(ctx) {
    mirrored(ctx, [[0, -1], [0.15, -0.6], [0.15, 0.5], [0.45, 0.95], [0.15, 0.8], [0, 0.8]]);
  },

  // Dish with signal arcs.
  Radar(ctx) {
    arcPath(ctx, 0, -0.1, 0.55, 0, Math.PI);
    line(ctx, -0.55, -0.1, 0.55, -0.1);
    line(ctx, 0, 0.45, 0, 0.85);
    line(ctx, -0.45, 0.85, 0.45, 0.85);
    arcPath(ctx, 0, -0.1, 0.82, -1.25, -0.3);
    arcPath(ctx, 0, -0.1, 1.08, -1.25, -0.3);
  },

  // An eye.
  Optical(ctx) {
    ctx.moveTo(-1, 0);
    ctx.quadraticCurveTo(0, -0.95, 1, 0);
    ctx.quadraticCurveTo(0, 0.95, -1, 0);
    circle(ctx, 0, 0, 0.3);
  },

  // Ping rings.
  Sonar(ctx) {
    circle(ctx, 0, 0, 0.16);
    circle(ctx, 0, 0, 0.55);
    circle(ctx, 0, 0, 0.92);
  },

  // A crenellated block.
  Command(ctx) {
    poly(ctx, [
      [-0.9, 0.8], [-0.9, -0.2], [-0.55, -0.2], [-0.55, -0.55], [-0.2, -0.55], [-0.2, -0.2],
      [0.2, -0.2], [0.2, -0.55], [0.55, -0.55], [0.55, -0.2], [0.9, -0.2], [0.9, 0.8]
    ]);
    line(ctx, -0.3, 0.8, -0.3, 0.3);
    line(ctx, 0.3, 0.8, 0.3, 0.3);
    line(ctx, -0.3, 0.3, 0.3, 0.3);
  },

  // Fallback for a kind this build does not know. It must not pass for a known unit.
  Unknown(ctx) {
    circle(ctx, 0, 0, 0.8);
    line(ctx, -0.3, 0, 0.3, 0);
  }
};

// The shape name for a kind (units, sensors, carriers), through the aliases. Unknown kinds map to "Unknown".
export function symbolName(kind) {
  const name = ALIASES[kind] || kind;
  return SHAPES[name] ? name : "Unknown";
}

// True for kinds drawn above the ground (aircraft and helicopters).
export const isAirborne = (kind) => AIRBORNE.has(symbolName(kind));

// Draws one symbol centred on (x, y) with the given radius in pixels. The caller sets the stroke style
// and line width. `fill`, when given, paints a backing colour under the outline. `dash` is a pixel dash
// pattern (it is scaled back, because the path is built in unit space).
export function drawSymbol(ctx, kind, x, y, r, heading = 0, fill = null, dash = null) {
  const name = symbolName(kind);
  ctx.save();
  ctx.translate(x, y);
  if (TOP_VIEW.has(name)) {
    ctx.rotate(heading);
  } else if (Math.sin(heading) < -0.01) {
    ctx.scale(-1, 1);
  }
  const width = ctx.lineWidth;
  ctx.scale(r, r);
  ctx.lineWidth = width / r;
  if (dash) {
    ctx.setLineDash(dash.map((d) => d / r));
  }
  ctx.beginPath();
  SHAPES[name](ctx);
  if (fill) {
    ctx.fillStyle = fill;
    ctx.fill();
  }
  ctx.stroke();
  ctx.restore();
}
