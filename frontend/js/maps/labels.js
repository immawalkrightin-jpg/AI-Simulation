// Greedy label placement. Items are placed in priority order (lower number first). Each item offers one or
// more text options (longest first); for each option eight offsets around the anchor are tried, and the
// first one that does not overlap an already placed label or an anchor marker wins. Labels that fit nowhere
// are left out. Pure function, no canvas: widths come from the caller.

const OFFSETS = [
  [1, 0], // east
  [-1, 0], // west
  [0, -1], // north
  [0, 1], // south
  [1, -1],
  [-1, -1],
  [1, 1],
  [-1, 1]
];

export function overlaps(a, b, pad = 2) {
  return a.x < b.x + b.w + pad && a.x + a.w + pad > b.x && a.y < b.y + b.h + pad && a.y + a.h + pad > b.y;
}

// items: [{ id, ax, ay, priority, h, radius, options: [{ text, w }] }]
// Returns Map id -> { text, x, y, w, h } (x, y is the top-left corner of the label box).
export function placeLabels(items, bounds = null) {
  const placed = [];
  const result = new Map();
  // Anchor markers block labels of other items.
  const markers = items.map((item) => ({ id: item.id, x: item.ax - item.radius, y: item.ay - item.radius, w: item.radius * 2, h: item.radius * 2 }));
  const ordered = [...items].sort((a, b) => a.priority - b.priority);
  for (const item of ordered) {
    let chosen = null;
    for (const option of item.options) {
      for (const [ox, oy] of OFFSETS) {
        const gap = item.radius + 3;
        const x = ox > 0 ? item.ax + gap : ox < 0 ? item.ax - gap - option.w : item.ax - option.w / 2;
        const y = oy > 0 ? item.ay + gap : oy < 0 ? item.ay - gap - item.h : item.ay - item.h / 2;
        const box = { x, y, w: option.w, h: item.h };
        if (bounds && (x < bounds.x || y < bounds.y || x + box.w > bounds.x + bounds.w || y + box.h > bounds.y + bounds.h)) {
          continue;
        }
        if (placed.some((other) => overlaps(box, other)) || markers.some((m) => m.id !== item.id && overlaps(box, m, 0))) {
          continue;
        }
        chosen = { text: option.text, ...box };
        break;
      }
      if (chosen) {
        break;
      }
    }
    if (chosen) {
      placed.push(chosen);
      result.set(item.id, chosen);
    }
  }
  return result;
}

// Fade with hysteresis, so a label that flickers between "fits" and "does not fit" does not blink.
// `state` maps id -> alpha. Fades in over `fadeIn` ms, out over `fadeOut` ms, and stays hidden for at
// least `hold` ms after it disappeared before it may come back.
export function fadeLabels(state, visible, dt, fadeIn = 180, fadeOut = 260, hold = 250) {
  for (const id of visible) {
    const entry = state.get(id) || { alpha: 0, hidden: hold };
    entry.hidden = 0;
    entry.alpha = Math.min(1, entry.alpha + dt / fadeIn);
    state.set(id, entry);
  }
  for (const [id, entry] of state) {
    if (!visible.has(id)) {
      entry.hidden = Math.min(hold, (entry.hidden || 0) + dt);
      entry.alpha = Math.max(0, entry.alpha - dt / fadeOut);
      if (entry.alpha === 0 && entry.hidden >= hold) {
        state.delete(id);
      }
    }
  }
}
