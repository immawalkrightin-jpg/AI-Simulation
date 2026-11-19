import { el, rows, fixed } from "./dom.js";

const HOSTILE = /clash|missile|intercepted|cancelled/i;
const EVENT_LIMIT = 12;
const OPERATION_LIMIT = 10;
const BLOCS = [
  ["East", "East bloc"],
  ["West", "West bloc"],
  ["NonAligned", "Non-aligned"]
];
const TAGS = { Negotiation: "NEG", SupplyShipment: "SUP", Clash: "CLS", MissileStrike: "MSL" };

export class Telemetry {
  constructor(root) {
    this.link = root.getElementById("link");
    this.clock = root.getElementById("clock");
    this.countries = root.getElementById("countries");
    this.relations = root.getElementById("relations");
    this.operations = root.getElementById("operations");
    this.events = root.getElementById("events");
    this.peaks = new Map();
  }

  setLink(state) {
    this.link.dataset.state = state;
    this.link.title = state;
  }

  update(snapshot, paused) {
    this.renderClock(snapshot, paused);
    this.renderCountries(snapshot);
    this.renderRelations(snapshot);
    this.renderOperations(snapshot);
    this.renderEvents(snapshot);
  }

  renderClock(snapshot, paused) {
    rows(this.clock, [
      ["Tick", snapshot.tick],
      ["Elapsed", `${fixed(snapshot.elapsedSeconds)} s`],
      ["Clock", paused ? "Paused" : "Running"],
      ["Countries", snapshot.countries.length],
      ["Units", snapshot.units.filter((u) => u.status !== "Destroyed").length],
      ["Sensors", snapshot.sensors.length]
    ]);
  }

  bar(key, value) {
    const peak = Math.max(this.peaks.get(key) || 0, value);
    this.peaks.set(key, peak);
    const ratio = peak > 0 ? value / peak : 0;
    const bar = el("div", ratio < 0.25 ? "bar low" : "bar");
    const fill = el("i");
    fill.style.width = `${Math.round(ratio * 100)}%`;
    bar.append(fill);
    return bar;
  }

  renderCountries(snapshot) {
    const nodes = [];
    for (const [bloc, label] of BLOCS) {
      const members = snapshot.countries
        .filter((country) => country.bloc === bloc)
        .sort((a, b) => Number(b.isModel) - Number(a.isModel) || a.id.localeCompare(b.id));
      if (members.length === 0) {
        continue;
      }
      nodes.push(el("h3", "", label));
      for (const country of members) {
        const block = el("div", country.isModel ? "country model" : "country");
        const head = el("div");
        head.append(
          el("span", "", country.name),
          el("span", "", `${fixed(country.supplies, 0)} / ${fixed(country.ammunition, 0)}`)
        );
        const bars = el("div", "bars");
        bars.append(this.bar(`${country.id}:s`, country.supplies), this.bar(`${country.id}:a`, country.ammunition));
        block.append(head, bars);
        nodes.push(block);
      }
    }
    this.countries.replaceChildren(...nodes);
  }

  renderRelations(snapshot) {
    const counts = new Map();
    for (const relation of snapshot.relations) {
      counts.set(relation.state, (counts.get(relation.state) || 0) + 1);
    }
    const lines = [
      ["Allied pairs", counts.get("Allied") || 0],
      ["Hostile pairs", counts.get("Hostile") || 0],
      ["Tense pairs", counts.get("Tense") || 0]
    ];
    const model = snapshot.countries.find((country) => country.isModel);
    if (model) {
      const mine = snapshot.relations
        .filter((r) => r.first === model.id || r.second === model.id)
        .map((r) => ({ other: r.first === model.id ? r.second : r.first, value: r.value, state: r.state }))
        .sort((a, b) => a.value - b.value);
      const picks = [...mine.slice(0, 4), ...mine.slice(-4).reverse()];
      for (const pick of picks) {
        lines.push([`${model.id} / ${pick.other}`, `${pick.state} ${fixed(pick.value, 2)}`]);
      }
    }
    rows(this.relations, lines);
  }

  renderOperations(snapshot) {
    const active = snapshot.interactions
      .filter((item) => item.endTick == null)
      .sort((a, b) => b.startTick - a.startTick)
      .slice(0, OPERATION_LIMIT);
    if (active.length === 0) {
      this.operations.replaceChildren(el("li", "empty", "No active operations"));
      return;
    }
    this.operations.replaceChildren(
      ...active.map((item) => {
        const hostile = item.kind === "Clash" || item.kind === "MissileStrike";
        const node = el("li", hostile ? "hostile" : "");
        let detail = `${item.source} > ${item.target}`;
        if (item.kind === "SupplyShipment") {
          detail += ` ${item.mode.toLowerCase()} ${fixed(item.cargo, 0)}`;
        } else if (item.kind === "Negotiation") {
          detail += ` ${item.subject}`;
        }
        node.append(el("b", "", TAGS[item.kind] || item.kind), document.createTextNode(detail));
        return node;
      })
    );
  }

  renderEvents(snapshot) {
    const recent = snapshot.events.slice(-EVENT_LIMIT).reverse();
    if (recent.length === 0) {
      this.events.replaceChildren(el("li", "empty", "No events yet"));
      return;
    }
    this.events.replaceChildren(
      ...recent.map((event) => {
        const item = el("li", HOSTILE.test(event.kind) ? "hostile" : "");
        item.append(el("b", "", String(event.tick)), document.createTextNode(event.description));
        return item;
      })
    );
  }
}
