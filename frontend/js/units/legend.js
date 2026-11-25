import { LEGEND, drawSymbol } from "./symbols.js";
import { DASH, backing, clusterBadge, foreignRing, glyph, headingTick, strengthBar } from "./markers.js";
import { INK, INK_RGB, MID_RGB, RED, RED_RGB, TEAL_RGB, rgba } from "../theme.js";

const ICON_WIDTH = 52;
const ICON_HEIGHT = 26;
const GROUP_ORDER = ["Units", "Carriers", "Sensors and installations", "Lines", "Markers", "Colour roles"];

const symbolRow = (entry) => ({
  id: entry.id,
  group: entry.group,
  label: entry.label,
  meaning: entry.meaning,
  draw(ctx, w, h) {
    ctx.strokeStyle = entry.kind === "Missile" ? RED : INK;
    ctx.lineWidth = 1.5;
    drawSymbol(ctx, entry.kind, w / 2, h / 2, 9, entry.kind === "Missile" ? Math.PI / 4 : 0);
  }
});

function route(ctx, w, h, rgb, alpha, dash) {
  ctx.setLineDash(dash);
  ctx.strokeStyle = rgba(rgb, alpha);
  ctx.lineWidth = 1.3;
  ctx.beginPath();
  ctx.moveTo(2, h / 2);
  ctx.lineTo(w - 2, h / 2);
  ctx.stroke();
  ctx.setLineDash([]);
}

function carrierHead(ctx, kind, x, y, heading) {
  backing(ctx, x, y, 7);
  ctx.strokeStyle = INK;
  ctx.lineWidth = 1.5;
  drawSymbol(ctx, kind, x, y, 7, heading);
}

const LINES = [
  {
    id: "line:negotiation",
    label: "Negotiation",
    meaning: "Talks in progress between two countries. Dashed line with a hollow diamond.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.6, DASH.None);
      glyph(ctx, "hollow", w / 2, h / 2, 4, INK_RGB, 1);
    }
  },
  {
    id: "line:concluded",
    label: "Talks concluded",
    meaning: "Solid line with a filled diamond.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.85, []);
      glyph(ctx, "diamond", w / 2, h / 2, 4, INK_RGB, 1);
    }
  },
  {
    id: "line:supply-ground",
    label: "Supply by ground",
    meaning: "Solid line. A truck carries the cargo over land.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.8, DASH.Ground);
      carrierHead(ctx, "Truck", w - 12, h / 2, 0);
    }
  },
  {
    id: "line:supply-naval",
    label: "Supply by sea",
    meaning: "Dotted line. A cargo ship carries the cargo across open water.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.8, DASH.Naval);
      carrierHead(ctx, "CargoShip", w - 12, h / 2, 0);
    }
  },
  {
    id: "line:supply-air",
    label: "Supply by air",
    meaning: "Long-dash line. A cargo plane or a helicopter carries the cargo.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.8, DASH.Air);
      carrierHead(ctx, "Transport", w - 12, h / 2, Math.PI / 2);
    }
  },
  {
    id: "line:lost",
    label: "Lost or collapsed",
    meaning: "A shipment intercepted or talks that collapsed. Red line ending in a cross.",
    draw(ctx, w, h) {
      route(ctx, w - 12, h, RED_RGB, 0.8, [10, 4]);
      glyph(ctx, "cross", w - 9, h / 2, 5, RED_RGB, 1);
    }
  },
  {
    id: "line:clash",
    label: "Clash",
    meaning: "Armed engagement. Red dashed lines meeting at a pulsing bracket.",
    draw(ctx, w, h) {
      route(ctx, w, h, RED_RGB, 0.8, DASH.Clash);
      backing(ctx, w / 2, h / 2, 6);
      glyph(ctx, "bracket", w / 2, h / 2, 7, RED_RGB, 1);
    }
  },
  {
    id: "line:missile",
    label: "Missile strike",
    meaning: "A high red arc. The ring on the target warns of arrival.",
    draw(ctx, w, h) {
      ctx.strokeStyle = rgba(RED_RGB, 0.9);
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(2, h - 3);
      ctx.quadraticCurveTo(w / 2, -h * 0.5, w - 4, h - 3);
      ctx.stroke();
    }
  },
  {
    id: "line:hostile",
    label: "Hostile relation",
    meaning: "Standing relation. Faint red dotted arc.",
    draw(ctx, w, h) {
      route(ctx, w, h, RED_RGB, 0.45, DASH.Hostile);
    }
  },
  {
    id: "line:allied",
    label: "Allied relation",
    meaning: "Standing relation. Faint solid line.",
    draw(ctx, w, h) {
      route(ctx, w, h, INK_RGB, 0.25, []);
    }
  }
].map((row) => ({ group: "Lines", ...row }));

const MARKERS = [
  {
    id: "marker:hero",
    label: "Model country",
    meaning: "The country the AI plays: a red node with a vertical beam and one pulse ring.",
    draw(ctx, w, h) {
      ctx.strokeStyle = rgba(RED_RGB, 0.8);
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(w / 2, h / 2);
      ctx.lineTo(w / 2, 1);
      ctx.stroke();
      ctx.fillStyle = RED;
      ctx.beginPath();
      ctx.arc(w / 2, h / 2 + 4, 4, 0, Math.PI * 2);
      ctx.fill();
    }
  },
  {
    id: "marker:capital",
    label: "Capital",
    meaning: "Country identity: a white node in a ring, with its code.",
    draw(ctx, w, h) {
      ctx.fillStyle = rgba(INK_RGB, 0.85);
      ctx.beginPath();
      ctx.arc(w / 2, h / 2, 2.8, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = rgba(INK_RGB, 0.4);
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.arc(w / 2, h / 2, 6, 0, Math.PI * 2);
      ctx.stroke();
    }
  },
  {
    id: "marker:selection",
    label: "Selection",
    meaning: "A white bracket that pulses softly, drawn flat on the ground.",
    draw(ctx, w, h) {
      glyph(ctx, "bracket", w / 2, h / 2, 9, INK_RGB, 0.9);
    }
  },
  {
    id: "marker:moving",
    label: "Moving unit",
    meaning: "A tick ahead of the symbol shows the heading. A short trail marks where it has been.",
    draw(ctx, w, h) {
      ctx.strokeStyle = rgba(INK_RGB, 0.3);
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(3, h / 2 + 4);
      ctx.lineTo(w / 2 - 9, h / 2 + 1);
      ctx.stroke();
      backing(ctx, w / 2 - 2, h / 2, 7);
      ctx.strokeStyle = INK;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, "Tank", w / 2 - 2, h / 2, 7, Math.PI / 2);
      headingTick(ctx, w / 2 - 2, h / 2, 7, Math.PI / 2);
    }
  },
  {
    id: "marker:destination",
    label: "Destination",
    meaning: "Dashed line to a small cross: where a moving unit is heading.",
    draw(ctx, w, h) {
      ctx.setLineDash(DASH.Destination);
      ctx.strokeStyle = rgba(MID_RGB, 0.8);
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(2, h / 2);
      ctx.lineTo(w - 12, h / 2);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.beginPath();
      ctx.moveTo(w - 16, h / 2);
      ctx.lineTo(w - 8, h / 2);
      ctx.moveTo(w - 12, h / 2 - 4);
      ctx.lineTo(w - 12, h / 2 + 4);
      ctx.stroke();
    }
  },
  {
    id: "marker:handoff",
    label: "Hand-off point",
    meaning: "A dock or an airfield where a shipment changes carrier. Ring with side ticks, filled once the cargo has passed it.",
    draw(ctx, w, h) {
      backing(ctx, w / 2, h / 2, 7);
      glyph(ctx, "handoff", w / 2, h / 2, 8, INK_RGB, 0.9);
      glyph(ctx, "dot", w / 2, h / 2, 1.8, INK_RGB, 0.9);
    }
  },
  {
    id: "marker:strength",
    label: "Strength bar",
    meaning: "Outlined bar under a unit: its strength against its own peak.",
    draw(ctx, w, h) {
      strengthBar(ctx, w / 2, h / 2 - 2, 10, 0.8);
    }
  },
  {
    id: "marker:low",
    label: "Low strength",
    meaning: "A dim bar with a tick on the low line. Never red.",
    draw(ctx, w, h) {
      strengthBar(ctx, w / 2, h / 2 - 2, 10, 0.15);
    }
  },
  {
    id: "marker:destroyed",
    label: "Destroyed unit",
    meaning: "Broken outline with a red cross.",
    draw(ctx, w, h) {
      ctx.setLineDash(DASH.Broken);
      ctx.strokeStyle = rgba(MID_RGB, 0.8);
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, "Tank", w / 2, h / 2, 9);
      ctx.setLineDash([]);
      glyph(ctx, "cross", w / 2, h / 2, 7, RED_RGB, 0.9);
    }
  },
  {
    id: "marker:cluster",
    label: "Cluster",
    meaning: "Several units too close to tell apart. Click to spread them out; leader lines show true positions.",
    draw(ctx, w, h) {
      clusterBadge(ctx, w / 2, h / 2, 3);
    }
  },
  {
    id: "marker:foreign",
    label: "Foreign unit",
    meaning: "A unit of another country inside the focused one: dashed ring and owner code.",
    draw(ctx, w, h) {
      backing(ctx, w / 2, h / 2, 7);
      ctx.strokeStyle = INK;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, "Tank", w / 2, h / 2, 7);
      foreignRing(ctx, w / 2, h / 2, 7);
    }
  }
].map((row) => ({ group: "Markers", ...row }));

const swatch = (id, label, meaning, paint) => ({ id, group: "Colour roles", label, meaning, draw: paint });

const COLOURS = [
  swatch("color:red", "Red", "The model country, hostile or destructive events, and alerts. Nothing else.", (ctx, w, h) => {
    ctx.fillStyle = RED;
    ctx.fillRect(w / 2 - 12, h / 2 - 5, 24, 10);
  }),
  swatch("color:white", "White", "Active and focused information.", (ctx, w, h) => {
    ctx.fillStyle = INK;
    ctx.fillRect(w / 2 - 12, h / 2 - 5, 24, 10);
  }),
  swatch("color:gray", "Gray", "Background and inactive things.", (ctx, w, h) => {
    ctx.fillStyle = rgba(MID_RGB, 0.6);
    ctx.fillRect(w / 2 - 12, h / 2 - 5, 24, 10);
  }),
  swatch("color:teal", "Teal tint", "Sensor coverage only.", (ctx, w, h) => {
    ctx.fillStyle = rgba(TEAL_RGB, 0.25);
    ctx.fillRect(w / 2 - 12, h / 2 - 5, 24, 10);
    ctx.setLineDash(DASH.Sensor);
    ctx.strokeStyle = rgba(TEAL_RGB, 0.7);
    ctx.strokeRect(w / 2 - 12, h / 2 - 5, 24, 10);
    ctx.setLineDash([]);
  }),
  swatch("pattern:east", "Diagonal hatch", "East bloc territory.", (ctx, w, h) => {
    ctx.save();
    ctx.beginPath();
    ctx.rect(w / 2 - 12, h / 2 - 7, 24, 14);
    ctx.clip();
    ctx.strokeStyle = rgba(INK_RGB, 0.55);
    ctx.lineWidth = 1;
    ctx.beginPath();
    for (let x = w / 2 - 24; x < w / 2 + 24; x += 5) {
      ctx.moveTo(x, h / 2 + 7);
      ctx.lineTo(x + 14, h / 2 - 7);
    }
    ctx.stroke();
    ctx.restore();
    ctx.strokeStyle = rgba(INK_RGB, 0.4);
    ctx.strokeRect(w / 2 - 12, h / 2 - 7, 24, 14);
  }),
  swatch("pattern:west", "Dot hatch", "West bloc territory.", (ctx, w, h) => {
    ctx.fillStyle = rgba(INK_RGB, 0.7);
    for (let x = w / 2 - 9; x <= w / 2 + 9; x += 6) {
      for (let y = h / 2 - 4; y <= h / 2 + 4; y += 6) {
        ctx.beginPath();
        ctx.arc(x, y, 0.9, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    ctx.strokeStyle = rgba(INK_RGB, 0.4);
    ctx.strokeRect(w / 2 - 12, h / 2 - 7, 24, 14);
  }),
  swatch("pattern:plain", "Plain faint fill", "Non-aligned territory.", (ctx, w, h) => {
    ctx.fillStyle = rgba(MID_RGB, 0.18);
    ctx.fillRect(w / 2 - 12, h / 2 - 7, 24, 14);
    ctx.strokeStyle = rgba(INK_RGB, 0.4);
    ctx.strokeRect(w / 2 - 12, h / 2 - 7, 24, 14);
  })
];

export const MANUAL_ROWS = [...LEGEND.map(symbolRow), ...LINES, ...MARKERS, ...COLOURS];

export function groupRows(rows) {
  const groups = new Map();
  for (const row of rows) {
    if (!groups.has(row.group)) {
      groups.set(row.group, []);
    }
    groups.get(row.group).push(row);
  }
  return GROUP_ORDER.filter((name) => groups.has(name)).map((name) => ({ name, rows: groups.get(name) }));
}

export function rowsPresent(ids) {
  return MANUAL_ROWS.filter((row) => ids.has(row.id));
}

function icon(row) {
  const ratio = window.devicePixelRatio || 1;
  const canvas = document.createElement("canvas");
  canvas.width = ICON_WIDTH * ratio;
  canvas.height = ICON_HEIGHT * ratio;
  canvas.style.width = `${ICON_WIDTH}px`;
  canvas.style.height = `${ICON_HEIGHT}px`;
  const ctx = canvas.getContext("2d");
  ctx.scale(ratio, ratio);
  ctx.lineCap = "butt";
  row.draw(ctx, ICON_WIDTH, ICON_HEIGHT);
  return canvas;
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) {
    node.className = className;
  }
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}

export function renderManual(root, onClose) {
  const head = element("div", "manual-head");
  const close = element("button", "manual-close", "Close");
  close.type = "button";
  close.addEventListener("click", onClose);
  head.append(element("h1", "", "Symbol manual"), element("span", "hint", "L or Esc to close"), close);
  const body = element("div", "manual-body");
  for (const group of groupRows(MANUAL_ROWS)) {
    body.append(element("h2", "", group.name));
    for (const row of group.rows) {
      const line = element("div", "manual-row");
      const text = element("div", "manual-text");
      text.append(element("b", "", row.label), element("span", "", row.meaning));
      line.append(icon(row), text);
      body.append(line);
    }
  }
  root.replaceChildren(head, body);
}

export function renderMini(root, ids) {
  const rows = rowsPresent(ids);
  if (rows.length === 0) {
    root.hidden = true;
    root.replaceChildren();
    return;
  }
  const nodes = [element("h2", "", "In this view")];
  for (const row of rows) {
    const line = element("div", "mini-row");
    line.append(icon(row), element("span", "", row.label));
    nodes.push(line);
  }
  root.replaceChildren(...nodes);
  root.hidden = false;
}
