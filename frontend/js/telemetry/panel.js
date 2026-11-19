import { el, rows, fixed } from "./dom.js";

const HOSTILE = /attack|engage|destroy|intercept|strike|clash|hit|lost/i;
const EVENT_LIMIT = 8;

export class Telemetry {
  constructor(root) {
    this.link = root.getElementById("link");
    this.clock = root.getElementById("clock");
    this.countries = root.getElementById("countries");
    this.relations = root.getElementById("relations");
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
    rows(
      this.relations,
      snapshot.relations.map((r) => [`${r.first} / ${r.second}`, `${r.state} ${fixed(r.value, 2)}`])
    );
    this.renderEvents(snapshot);
  }

  renderClock(snapshot, paused) {
    rows(this.clock, [
      ["Tick", snapshot.tick],
      ["Elapsed", `${fixed(snapshot.elapsedSeconds)} s`],
      ["Clock", paused ? "Paused" : "Running"],
      ["Units", snapshot.units.length],
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
    const nodes = snapshot.countries.map((country) => {
      const block = el("div", "country");
      const head = el("div");
      head.append(el("span", "", country.name), el("span", "", country.id));
      const supplies = el("div");
      supplies.append(el("span", "", "Supplies"), el("span", "", fixed(country.supplies, 0)));
      const ammunition = el("div");
      ammunition.append(el("span", "", "Ammunition"), el("span", "", fixed(country.ammunition, 0)));
      block.append(
        head,
        supplies,
        this.bar(`${country.id}:s`, country.supplies),
        ammunition,
        this.bar(`${country.id}:a`, country.ammunition)
      );
      return block;
    });
    this.countries.replaceChildren(...nodes);
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
