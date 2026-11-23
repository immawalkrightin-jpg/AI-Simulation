import { el } from "./dom.js";

// Hover tooltip for every symbol. Content comes from tooltipFor (describe.js); this file positions it.

export const TIP_OFFSET = 16;

// Top-left corner for a tooltip of size w x h near the pointer: right and below by default, flipped to the
// other side of the pointer when it would leave the viewport, and never off screen.
export function placeTip(x, y, w, h, viewWidth, viewHeight, offset = TIP_OFFSET) {
  let left = x + offset;
  let top = y + offset;
  if (left + w > viewWidth - 4) {
    left = x - offset - w;
  }
  if (top + h > viewHeight - 4) {
    top = y - offset - h;
  }
  return { left: Math.max(4, left), top: Math.max(4, top) };
}

export class Tooltip {
  constructor(root) {
    this.root = root;
    this.visible = false;
  }

  show(tip, x, y) {
    const list = el("dl", "rows");
    for (const [label, value] of tip.rows) {
      list.append(el("dt", "", label), el("dd", "", String(value)));
    }
    this.root.replaceChildren(el("b", "", tip.title), list);
    this.root.hidden = false;
    this.visible = true;
    this.move(x, y);
  }

  move(x, y) {
    if (!this.visible) {
      return;
    }
    const { left, top } = placeTip(x, y, this.root.offsetWidth, this.root.offsetHeight, window.innerWidth, window.innerHeight);
    this.root.style.left = `${left}px`;
    this.root.style.top = `${top}px`;
  }

  hide() {
    this.visible = false;
    this.root.hidden = true;
  }
}
