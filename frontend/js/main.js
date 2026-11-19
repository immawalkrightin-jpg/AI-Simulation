import { config } from "./config.js";
import { GroundTruthLink } from "./connection.js";
import { MapRenderer } from "./maps/renderer.js";
import { Telemetry } from "./telemetry/panel.js";
import { Inspector } from "./telemetry/inspector.js";

const toggle = document.getElementById("toggle");
const telemetry = new Telemetry(document);
const inspector = new Inspector(document.getElementById("inspector"));

let snapshot = null;
let paused = false;
let selected = null;

function refreshInspector() {
  if (snapshot && selected) {
    inspector.show(snapshot, selected);
  } else {
    inspector.hide();
  }
}

const renderer = new MapRenderer(document.getElementById("map"), (selection) => {
  selected = selection;
  refreshInspector();
});

const link = new GroundTruthLink(config, {
  onSnapshot: (next) => {
    snapshot = next;
    renderer.setSnapshot(next);
    telemetry.update(next, paused);
    refreshInspector();
  },
  onPaused: (value) => {
    paused = value;
    toggle.textContent = paused ? "Resume" : "Pause";
    if (snapshot) {
      telemetry.update(snapshot, paused);
    }
  },
  onLink: (state) => telemetry.setLink(state)
});

toggle.addEventListener("click", () => link.control(paused ? "resume" : "pause").catch(() => {}));
document.querySelectorAll("[data-step]").forEach((button) => {
  button.addEventListener("click", () => link.control("step", Number(button.dataset.step)).catch(() => {}));
});

link.start();
