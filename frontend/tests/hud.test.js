import test from "node:test";
import assert from "node:assert/strict";
import { COLLAPSED_EVENTS, EXPANDED_EVENTS, clockText, isHostileEvent, recentEvents } from "../js/telemetry/hud.js";
import { placeTip } from "../js/telemetry/tooltip.js";
import { GroundTruthLink } from "../js/connection.js";

const events = Array.from({ length: 20 }, (_, i) => ({ tick: i, kind: i % 5 === 0 ? "Clash" : "RelationShift", description: `e${i}` }));

test("event strip: collapsed shows the last three events, newest first", () => {
  const recent = recentEvents(events, COLLAPSED_EVENTS);
  assert.deepEqual(recent.map((e) => e.tick), [19, 18, 17]);
});

test("event strip: expanded shows more, and fewer events than the limit is fine", () => {
  assert.equal(recentEvents(events, EXPANDED_EVENTS).length, EXPANDED_EVENTS);
  assert.deepEqual(recentEvents(events.slice(0, 2), COLLAPSED_EVENTS).map((e) => e.tick), [1, 0]);
  assert.deepEqual(recentEvents([], COLLAPSED_EVENTS), []);
});

test("event strip: only hostile kinds are marked", () => {
  assert.equal(isHostileEvent({ kind: "Clash" }), true);
  assert.equal(isHostileEvent({ kind: "MissileStrike" }), true);
  assert.equal(isHostileEvent({ kind: "RelationShift" }), false);
});

test("clock text", () => {
  assert.equal(clockText(12.4), "12 s");
});

test("tooltip: placed right and below the pointer by default", () => {
  assert.deepEqual(placeTip(100, 100, 120, 60, 1280, 800), { left: 116, top: 116 });
});

test("tooltip: flips to the other side near the right and bottom edges", () => {
  const spot = placeTip(1250, 780, 120, 60, 1280, 800);
  assert.ok(spot.left + 120 <= 1250, "left of the pointer");
  assert.ok(spot.top + 60 <= 780, "above the pointer");
});

test("tooltip: never leaves the viewport on the top left", () => {
  const spot = placeTip(2, 2, 400, 300, 300, 200);
  assert.ok(spot.left >= 4 && spot.top >= 4);
});

test("controls: step takes a count, speed takes a value, others take nothing", () => {
  assert.equal(GroundTruthLink.query("step", 10), "?count=10");
  assert.equal(GroundTruthLink.query("speed", 2), "?value=2");
  assert.equal(GroundTruthLink.query("pause"), "");
  assert.equal(GroundTruthLink.query("resume"), "");
});

test("controls: a speed change posts to the speed endpoint and reports the status", async () => {
  const calls = [];
  const realFetch = globalThis.fetch;
  globalThis.fetch = async (url, options) => {
    calls.push([url, options.method, options.headers["X-Access-Key"]]);
    return { ok: true, json: async () => ({ paused: false, speed: 2 }) };
  };
  try {
    const seen = [];
    const link = new GroundTruthLink(
      { apiBase: "http://x", groundTruthKey: "k" },
      { onPaused: (p) => seen.push(["paused", p]), onStatus: (s) => seen.push(["speed", s.speed]) }
    );
    await link.control("speed", 2);
    assert.deepEqual(calls, [["http://x/api/simulation/speed?value=2", "POST", "k"]]);
    assert.deepEqual(seen, [["paused", false], ["speed", 2]]);
  } finally {
    globalThis.fetch = realFetch;
  }
});

test("controls: a handler set without onStatus still works", async () => {
  const realFetch = globalThis.fetch;
  globalThis.fetch = async () => ({ ok: true, json: async () => ({ paused: true, speed: 1 }) });
  try {
    let paused = null;
    const link = new GroundTruthLink({ apiBase: "http://x", groundTruthKey: "k" }, { onPaused: (p) => (paused = p) });
    await link.control("pause");
    assert.equal(paused, true);
  } finally {
    globalThis.fetch = realFetch;
  }
});
