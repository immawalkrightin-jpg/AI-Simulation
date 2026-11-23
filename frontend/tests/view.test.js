import test from "node:test";
import assert from "node:assert/strict";
import { classifyExternal, countryFrame, isExternal, ringsBounds } from "../js/maps/view.js";
import { fadeLabels, overlaps, placeLabels } from "../js/maps/labels.js";

const square = (x, y, w, h) => [x, y, x + w, y, x + w, y + h, x, y + h];
const home = { id: "AAA", x: 100, y: 100 };
const territory = [square(60, 60, 80, 80)]; // 60..140

test("isExternal: a unit inside its own territory is internal", () => {
  assert.equal(isExternal({ x: 110, y: 110, status: "Idle" }, home, territory), false);
});

test("isExternal: outside the territory but close to the capital is still internal", () => {
  assert.equal(isExternal({ x: 143, y: 100, status: "Idle" }, home, territory), false);
});

test("isExternal: outside the territory and far from the capital is external", () => {
  assert.equal(isExternal({ x: 300, y: 100, status: "Idle" }, home, territory), true);
});

test("isExternal: heading to a destination outside the owner is external", () => {
  const unit = { x: 110, y: 110, status: "Moving", destinationX: 400, destinationY: 100 };
  assert.equal(isExternal(unit, home, territory), true);
  assert.equal(isExternal({ ...unit, destinationX: 130, destinationY: 120 }, home, territory), false);
});

test("isExternal: destroyed units are never external; no map falls back to distance", () => {
  assert.equal(isExternal({ x: 300, y: 100, status: "Destroyed" }, home, territory), false);
  assert.equal(isExternal({ x: 300, y: 100, status: "Idle" }, home, null), true);
  assert.equal(isExternal({ x: 120, y: 100, status: "Idle" }, home, null), false);
});

test("classifyExternal: one pass over the snapshot", () => {
  const snapshot = {
    countries: [home],
    units: [
      { id: "a", owner: "AAA", x: 110, y: 110, status: "Idle" },
      { id: "b", owner: "AAA", x: 500, y: 500, status: "Moving" }
    ]
  };
  assert.deepEqual([...classifyExternal(snapshot, () => territory)], ["b"]);
});

test("countryFrame: frames the content, with a minimum span", () => {
  const frame = countryFrame(home, [{ x: 110, y: 105, status: "Idle" }], [], null);
  assert.ok(frame.x1 - frame.x0 >= 120);
  assert.ok(frame.y1 - frame.y0 >= 120);
  assert.ok(frame.x0 <= 100 && frame.x1 >= 110);
});

test("countryFrame: a huge spread is capped to the territory bounding box", () => {
  const units = [{ x: 0, y: 0, status: "Idle" }, { x: 1000, y: 1000, status: "Idle" }];
  const box = { x0: 0, y0: 0, x1: 400, y1: 300 };
  const frame = countryFrame(home, units, [], box);
  assert.ok(frame.x1 - frame.x0 <= 400 + 1e-9);
  assert.ok(frame.y1 - frame.y0 <= 300 + 1e-9);
});

test("countryFrame: destroyed units do not stretch the frame", () => {
  const frame = countryFrame(home, [{ x: 900, y: 900, status: "Destroyed" }], [], null);
  assert.ok(frame.x1 < 300);
});

test("ringsBounds", () => {
  assert.deepEqual(ringsBounds(territory), { x0: 60, y0: 60, x1: 140, y1: 140 });
  assert.equal(ringsBounds([]), null);
});

const item = (id, ax, ay, priority, options) => ({ id, ax, ay, priority, h: 12, radius: 4, options });

test("labels: a free label goes east of its anchor", () => {
  const result = placeLabels([item("A", 100, 100, 1, [{ text: "Alpha", w: 30 }])]);
  assert.equal(result.get("A").x, 107);
});

test("labels: overlapping labels are moved to another offset, never overlapped", () => {
  const result = placeLabels([
    item("A", 100, 100, 1, [{ text: "Alpha", w: 40 }]),
    item("B", 108, 102, 2, [{ text: "Bravo", w: 40 }])
  ]);
  const a = result.get("A");
  const b = result.get("B");
  assert.ok(a);
  if (b) {
    assert.equal(overlaps(a, b, 0), false);
  }
});

test("labels: higher priority wins and the other falls back to a shorter text or is hidden", () => {
  const crowded = [
    item("hero", 100, 100, 0, [{ text: "Soviet Union", w: 60 }]),
    item("B", 100, 100, 5, [{ text: "Long name here", w: 70 }, { text: "BBB", w: 20 }])
  ];
  const result = placeLabels(crowded);
  assert.equal(result.get("hero").text, "Soviet Union");
  const b = result.get("B");
  if (b) {
    assert.equal(overlaps(result.get("hero"), b, 0), false);
  }
});

test("labels: an impossible label is hidden", () => {
  const result = placeLabels([item("A", 5, 5, 1, [{ text: "Alpha", w: 200 }])], { x: 0, y: 0, w: 100, h: 100 });
  assert.equal(result.has("A"), false);
});

test("labels: fade in and out with hysteresis", () => {
  const state = new Map();
  fadeLabels(state, new Set(["A"]), 90);
  assert.ok(state.get("A").alpha > 0 && state.get("A").alpha < 1);
  fadeLabels(state, new Set(["A"]), 200);
  assert.equal(state.get("A").alpha, 1);
  fadeLabels(state, new Set(), 100);
  assert.ok(state.get("A").alpha < 1 && state.get("A").alpha > 0, "does not vanish at once");
  fadeLabels(state, new Set(["A"]), 16);
  assert.ok(state.get("A").alpha > 0.5, "comes back from where it was, no blink");
});
