import { config } from "./config.js";
import { GroundTruthLink } from "./connection.js";
import { MapRenderer } from "./maps/renderer.js";
import { Dossier } from "./telemetry/dossier.js";
import { Inspector } from "./telemetry/inspector.js";
import { Hud } from "./telemetry/hud.js";
import { Tooltip } from "./telemetry/tooltip.js";
import { tooltipFor } from "./telemetry/describe.js";
import { renderManual, renderMini } from "./units/legend.js";
import { FeedbackLink } from "./terminal/feedback.js";
import { Terminal } from "./terminal/terminal.js";

const $ = (id) => document.getElementById(id);
const canvas = $("map");

const hud = new Hud(document);
const tooltip = new Tooltip($("tip"));
const inspector = new Inspector($("inspector"));
const dossier = new Dossier($("dossier"), { onBack: () => renderer.exitCountry() });
const mini = $("mini");
const manual = $("manual");
const crumbSeparator = $("crumb-sep");
const crumbName = $("crumb-name");

let snapshot = null;
let paused = false;
let selected = null;
let view = { mode: "world", focus: null, name: null };
let hover = null;
let pointer = { x: 0, y: 0 };
let dragging = false;
let manualBuilt = false;

// Panels ---------------------------------------------------------------------------------------------------

function refreshPanels() {
  if (snapshot && view.focus) {
    dossier.show(snapshot, view.focus);
  } else {
    dossier.hide();
  }
  if (snapshot && selected && selected.type !== "country") {
    inspector.show(snapshot, selected);
  } else {
    inspector.hide();
  }
}

function refreshTip() {
  const tip = snapshot && hover && !dragging ? tooltipFor(snapshot, hover) : null;
  if (tip) {
    tooltip.show(tip, pointer.x, pointer.y);
  } else {
    tooltip.hide();
  }
}

function updateCrumb() {
  const inCountry = view.mode === "country" && view.focus;
  crumbSeparator.hidden = !inCountry;
  crumbName.hidden = !inCountry;
  crumbName.textContent = inCountry && view.name ? view.name.toUpperCase() : "";
}

// Framing leaves room for the dossier on the right and the HUD and breadcrumb on top, so the focused
// country is never centred under a panel.
function updateInset() {
  const wide = window.innerWidth > 700;
  const inCountry = view.mode === "country";
  renderer.setInset({
    left: 0,
    bottom: 0,
    right: wide && inCountry ? 304 : 0,
    top: inCountry ? 92 : 56
  });
}

// Symbol manual --------------------------------------------------------------------------------------------

function setManual(open) {
  if (open && !manualBuilt) {
    renderManual(manual, () => setManual(false));
    manualBuilt = true;
  }
  manual.hidden = !open;
}

$("cam-manual").addEventListener("click", () => setManual(manual.hidden));

window.addEventListener(
  "keydown",
  (event) => {
    if (event.target && /input|select|textarea/i.test(event.target.tagName)) {
      return;
    }
    if (event.key === "l" || event.key === "L") {
      setManual(manual.hidden);
    } else if (event.key === "Escape" && !manual.hidden) {
      // Closes the manual only; the renderer's own Esc handler (leave country view) must not also fire.
      setManual(false);
      event.stopImmediatePropagation();
    }
  },
  true
);

// Map ------------------------------------------------------------------------------------------------------

const renderer = new MapRenderer(canvas, {
  onSelect: (selection) => {
    selected = selection;
    refreshPanels();
  },
  onView: (next) => {
    view = next;
    updateCrumb();
    updateInset();
    refreshPanels();
  },
  onHover: (hit) => {
    hover = hit;
    refreshTip();
  },
  onPresent: ({ mode, ids }) => {
    if (mode === "country") {
      renderMini(mini, ids);
    } else {
      mini.hidden = true;
    }
  }
});

updateInset();
window.addEventListener("resize", updateInset);

window.addEventListener("pointermove", (event) => {
  pointer = { x: event.clientX, y: event.clientY };
  tooltip.move(pointer.x, pointer.y);
});
canvas.addEventListener("pointerdown", () => {
  dragging = true;
  refreshTip();
});
const release = () => {
  dragging = false;
  refreshTip();
};
canvas.addEventListener("pointerup", release);
canvas.addEventListener("pointercancel", release);

$("crumb-world").addEventListener("click", () => renderer.exitCountry());
$("cam-world").addEventListener("click", () => renderer.goPreset("world"));
$("cam-theatre").addEventListener("click", () => renderer.goPreset("theatre"));
$("cam-in").addEventListener("click", () => renderer.zoomBy(1.6));
$("cam-out").addEventListener("click", () => renderer.zoomBy(1 / 1.6));
$("cam-tilt").addEventListener("click", () => renderer.toggleTilt());
$("cam-reset").addEventListener("click", () => renderer.reset());

// Link and controls ----------------------------------------------------------------------------------------

const link = new GroundTruthLink(config, {
  onSnapshot: (next) => {
    snapshot = next;
    renderer.setSnapshot(next);
    hud.update(next, paused);
    refreshPanels();
    refreshTip();
  },
  onPaused: (value) => {
    paused = value;
    $("toggle").textContent = paused ? "Resume" : "Pause";
    hud.setPaused(paused);
  },
  onStatus: (status) => hud.setSpeed(status.speed),
  onLink: (state) => hud.setLink(state)
});

$("toggle").addEventListener("click", () => link.control(paused ? "resume" : "pause").catch(() => {}));
document.querySelectorAll("[data-step]").forEach((button) => {
  button.addEventListener("click", () => link.control("step", Number(button.dataset.step)).catch(() => {}));
});
$("speed").addEventListener("change", (event) => {
  link.control("speed", Number(event.target.value)).catch(() => {});
});

fetch("assets/maps/coldwar.json")
  .then((response) => (response.ok ? response.json() : null))
  .then((map) => renderer.setMap(map))
  .catch(() => renderer.setMap(null));

const terminal = new Terminal(document, new FeedbackLink(config));
$("terminal-toggle").addEventListener("click", () => terminal.toggle());
window.addEventListener("keydown", (event) => {
  if (event.key !== "`" || event.ctrlKey || event.metaKey || event.altKey) {
    return;
  }
  if (event.target && /input|select|textarea/i.test(event.target.tagName)) {
    return;
  }
  event.preventDefault();
  terminal.toggle();
});

link.start();
