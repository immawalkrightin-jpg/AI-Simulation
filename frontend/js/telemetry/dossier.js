import { el } from "./dom.js";
import { LOW_SHARE, dossierFor } from "./describe.js";

// The country dossier: a contextual panel that appears when a country is opened. All content comes from
// dossierFor (describe.js), which is pure and unit-tested. This file only turns it into DOM.

const percent = (ratio) => `${Math.round(Math.min(1, Math.max(0, ratio)) * 100)}%`;

// A labelled bar. Low values dim the fill and show a tick on the low line, never red.
function meter(label, entry) {
  const node = el("div", entry.ratio < LOW_SHARE ? "meter low" : "meter");
  const head = el("div", "meter-head");
  head.append(el("span", "", label), el("b", "", String(Math.round(entry.value))));
  const bar = el("div", "meter-bar");
  const fill = el("i");
  fill.style.width = percent(entry.ratio);
  bar.append(fill);
  node.append(head, bar, el("small", "", `${percent(entry.ratio)} of the peak seen`));
  return node;
}

function section(title, ...children) {
  const node = el("section", "dossier-section");
  node.append(el("h2", "", title), ...children);
  return node;
}

function list(items, empty) {
  const node = el("ul", "dossier-list");
  if (items.length === 0) {
    node.append(el("li", "empty", empty));
    return node;
  }
  node.append(...items);
  return node;
}

function unitLine(group) {
  const parts = [`${group.active} active`];
  if (group.moving > 0) {
    parts.push(`${group.moving} moving`);
  }
  if (group.destroyed > 0) {
    parts.push(`${group.destroyed} destroyed`);
  }
  const line = el("li");
  line.append(el("span", "", group.label), el("b", "", String(group.total)), el("small", "", parts.join(", ")));
  return line;
}

function powerLine(power) {
  const line = el("li");
  line.append(el("span", "", "Power"), el("b", "", power.value.toFixed(1)), el("small", "", `rank ${power.rank} of ${power.of}`));
  return line;
}

function operationLine(op) {
  const line = el("li", op.hostile ? "hostile" : "");
  const arrow = op.direction === "out" ? "to" : "from";
  line.append(el("span", "", op.kind), el("small", "", `${arrow} ${op.other}${op.detail ? `, ${op.detail}` : ""}`));
  return line;
}

function relationLine(relation) {
  const line = el("li");
  line.append(el("span", "", relation.other), el("small", "", `${relation.state} ${relation.value.toFixed(2)}`));
  return line;
}

export class Dossier {
  constructor(root, handlers = {}) {
    this.root = root;
    this.peaks = new Map();
    this.focus = null;
    this.onBack = handlers.onBack || (() => {});
    // Delegated, so a click still lands when a snapshot replaces the content between press and release.
    root.addEventListener("click", (event) => {
      if (event.target.closest("[data-back]")) {
        this.onBack();
      }
    });
  }

  hide() {
    this.focus = null;
    this.root.hidden = true;
  }

  show(snapshot, id) {
    if (id !== this.focus) {
      this.root.scrollTop = 0;
    }
    const data = dossierFor(snapshot, id, this.peaks);
    if (!data) {
      this.hide();
      return;
    }
    this.focus = id;
    const head = el("header", "dossier-head");
    const title = el("h1", data.isModel ? "model" : "", data.name);
    head.append(title, el("span", "tag", data.isModel ? `${data.bloc}, model country` : data.bloc));
    const back = el("button", "back", "Back to world");
    back.type = "button";
    back.dataset.back = "1";
    this.root.replaceChildren(
      head,
      section("National power", list(data.power ? [powerLine(data.power)] : [], "Not reported")),
      section("Stocks", meter("Supplies", data.supplies), meter("Ammunition", data.ammunition)),
      section("Units", list(data.units.map(unitLine), "No units")),
      section("Active operations", list(data.operations.map(operationLine), "None active")),
      section("Relations", list([...data.hostile.map((r) => relationLine({ ...r, other: `${r.other} (hostile)` })), ...data.closest.map((r) => relationLine({ ...r, other: `${r.other} (closest)` }))], "No notable relations")),
      back
    );
    this.root.hidden = false;
  }
}
