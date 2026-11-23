import { el, fixed } from "./dom.js";

// Always-visible HUD (tick, clock, running or paused, speed) and the collapsible event strip.

const HOSTILE = /clash|missile|intercepted|cancelled/i;
export const COLLAPSED_EVENTS = 3;
export const EXPANDED_EVENTS = 12;

export const isHostileEvent = (event) => HOSTILE.test(event.kind);

// Newest first, at most `limit`.
export function recentEvents(events, limit) {
  return events.slice(-limit).reverse();
}

export function clockText(seconds) {
  return `${fixed(seconds, 0)} s`;
}

export class Hud {
  constructor(root) {
    this.link = root.getElementById("link");
    this.tick = root.getElementById("hud-tick");
    this.time = root.getElementById("hud-time");
    this.state = root.getElementById("hud-state");
    this.speed = root.getElementById("speed");
    this.log = root.getElementById("log");
    this.logToggle = root.getElementById("log-toggle");
    this.logHint = root.getElementById("log-hint");
    this.logList = root.getElementById("log-list");
    this.open = false;
    this.events = [];
    this.logToggle.addEventListener("click", () => this.setOpen(!this.open));
  }

  setLink(state) {
    this.link.dataset.state = state;
    this.link.title = state;
  }

  update(snapshot, paused) {
    this.tick.textContent = String(snapshot.tick);
    this.time.textContent = clockText(snapshot.elapsedSeconds);
    this.setPaused(paused);
    this.events = snapshot.events;
    this.renderLog();
  }

  setPaused(paused) {
    this.state.dataset.state = paused ? "paused" : "running";
    this.state.textContent = paused ? "Paused" : "Running";
  }

  // Keeps the speed control in step with the engine, unless the person is using it.
  setSpeed(speed) {
    if (!this.speed || document.activeElement === this.speed) {
      return;
    }
    const wanted = String(speed);
    if ([...this.speed.options].some((option) => option.value === wanted)) {
      this.speed.value = wanted;
    }
  }

  setOpen(open) {
    this.open = open;
    this.log.dataset.open = String(open);
    this.logToggle.setAttribute("aria-expanded", String(open));
    this.logHint.textContent = open ? "click to collapse" : "click to expand";
    this.renderLog();
  }

  renderLog() {
    const recent = recentEvents(this.events, this.open ? EXPANDED_EVENTS : COLLAPSED_EVENTS);
    if (recent.length === 0) {
      this.logList.replaceChildren(el("li", "empty", "No events yet"));
      return;
    }
    this.logList.replaceChildren(
      ...recent.map((event) => {
        const item = el("li", isHostileEvent(event) ? "hostile" : "");
        item.append(el("b", "", String(event.tick)), document.createTextNode(event.description));
        return item;
      })
    );
  }
}
