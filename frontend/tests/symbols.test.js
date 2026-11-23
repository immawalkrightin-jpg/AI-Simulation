import test from "node:test";
import assert from "node:assert/strict";
import { LEGEND, drawSymbol, symbolName } from "../js/units/symbols.js";
import { MANUAL_ROWS, groupRows, rowsPresent } from "../js/units/legend.js";
import { carrierIsDefault, carrierOf, screenHeading, unitHeading } from "../js/maps/motion.js";

function fakeContext() {
  const calls = [];
  const state = { lineWidth: 1 };
  return new Proxy(state, {
    get(target, prop) {
      if (prop === "calls") {
        return calls;
      }
      if (prop in target) {
        return target[prop];
      }
      return (...args) => {
        calls.push([prop, ...args]);
      };
    },
    set(target, prop, value) {
      target[prop] = value;
      return true;
    }
  });
}

test("symbols: every legend entry resolves to a real silhouette", () => {
  for (const entry of LEGEND) {
    assert.notEqual(symbolName(entry.kind), "Unknown", `${entry.kind} has no silhouette`);
  }
});

test("symbols: engine unit kinds map to distinct silhouettes", () => {
  const kinds = ["Infantry", "Armor", "Artillery", "SupplyConvoy", "Naval", "Air"];
  const names = kinds.map(symbolName);
  assert.equal(new Set(names).size, kinds.length);
  assert.ok(!names.includes("Unknown"));
});

test("symbols: sensors, carriers and the command centre have their own silhouettes", () => {
  const names = ["Radar", "Optical", "Sonar", "Command", "Truck", "CargoShip", "Transport", "Helicopter", "Missile"].map(symbolName);
  assert.equal(new Set(names).size, names.length);
  assert.ok(!names.includes("Unknown"));
});

test("symbols: an unknown kind falls back to Unknown, not to a known unit", () => {
  assert.equal(symbolName("Zeppelin"), "Unknown");
});

test("symbols: every shape draws and strokes without throwing", () => {
  for (const kind of [...LEGEND.map((e) => e.kind), "Zeppelin"]) {
    const ctx = fakeContext();
    drawSymbol(ctx, kind, 10, 10, 9, 0.7);
    assert.ok(ctx.calls.some((c) => c[0] === "stroke"), `${kind} was not stroked`);
  }
});

test("symbols: ground shapes mirror, top-view shapes rotate", () => {
  const mirrored = fakeContext();
  drawSymbol(mirrored, "Tank", 0, 0, 9, -Math.PI / 2);
  assert.ok(mirrored.calls.some((c) => c[0] === "scale" && c[1] === -1));
  const rotated = fakeContext();
  drawSymbol(rotated, "Fighter", 0, 0, 9, 1.2);
  assert.ok(rotated.calls.some((c) => c[0] === "rotate" && c[1] === 1.2));
  const upright = fakeContext();
  drawSymbol(upright, "Tank", 0, 0, 9, 0);
  assert.ok(!upright.calls.some((c) => c[0] === "scale" && c[1] === -1));
});

test("manual: ids are unique and every legend entry is a manual row", () => {
  const ids = MANUAL_ROWS.map((row) => row.id);
  assert.equal(new Set(ids).size, ids.length);
  for (const entry of LEGEND) {
    assert.ok(ids.includes(entry.id), `${entry.id} missing from the manual`);
  }
});

test("manual: groups come out in the documented order", () => {
  const names = groupRows(MANUAL_ROWS).map((g) => g.name);
  assert.deepEqual(names, ["Units", "Carriers", "Sensors and installations", "Lines", "Markers", "Colour roles"]);
});

test("manual: every row draws on a fake canvas", () => {
  for (const row of MANUAL_ROWS) {
    const ctx = fakeContext();
    row.draw(ctx, 52, 26);
    assert.ok(ctx.calls.length > 0 || row.group === "Colour roles", `${row.id} drew nothing`);
    assert.ok(row.label && row.meaning, `${row.id} needs a label and a meaning`);
  }
});

test("manual: every line type the map draws has a row", () => {
  const ids = new Set(MANUAL_ROWS.map((row) => row.id));
  for (const id of [
    "line:negotiation", "line:concluded", "line:supply-ground", "line:supply-naval", "line:supply-air",
    "line:lost", "line:clash", "line:missile", "line:hostile", "line:allied",
    "marker:hero", "marker:capital", "marker:selection", "marker:cluster", "marker:foreign", "marker:destroyed", "marker:low"
  ]) {
    assert.ok(ids.has(id), `${id} missing`);
  }
});

test("manual: the in-view legend keeps only what is present, in manual order", () => {
  const rows = rowsPresent(new Set(["unit:Tank", "carrier:Truck", "nope"]));
  assert.deepEqual(rows.map((r) => r.id), ["unit:Tank", "carrier:Truck"]);
  assert.deepEqual(rowsPresent(new Set()), []);
});

test("motion: the carrier is the engine's when present, otherwise the mode default", () => {
  assert.equal(carrierOf({ mode: "Ground" }), "Truck");
  assert.equal(carrierOf({ mode: "Naval" }), "CargoShip");
  assert.equal(carrierOf({ mode: "Air" }), "Transport");
  assert.equal(carrierOf({ mode: "Air", carrier: "Helicopter" }), "Helicopter");
  assert.equal(carrierOf({ mode: "None" }), null);
  assert.equal(carrierIsDefault({ mode: "Air" }), true);
  assert.equal(carrierIsDefault({ mode: "Air", carrier: "Transport" }), false);
});

test("motion: the frontend never invents a helicopter", () => {
  for (const mode of ["Ground", "Naval", "Air", "None", undefined]) {
    assert.notEqual(carrierOf({ mode }), "Helicopter");
  }
});

test("motion: screen headings are clockwise from up", () => {
  assert.equal(screenHeading({ x: 0, y: 0 }, { x: 0, y: -10 }), 0);
  assert.ok(Math.abs(screenHeading({ x: 0, y: 0 }, { x: 10, y: 0 }) - Math.PI / 2) < 1e-12);
  assert.ok(Math.abs(screenHeading({ x: 0, y: 0 }, { x: 0, y: 10 }) - Math.PI) < 1e-12);
  assert.ok(Math.abs(screenHeading({ x: 0, y: 0 }, { x: -10, y: 0 }) + Math.PI / 2) < 1e-12);
  assert.equal(screenHeading({ x: 3, y: 3 }, { x: 3, y: 3 }), null);
});

test("motion: a unit heads for its destination, else keeps its last facing, else has none", () => {
  const project = (x, y) => ({ x, y });
  const heading = (unit, facing) => unitHeading(project, 100, 100, unit, facing);
  assert.ok(Math.abs(heading({ destinationX: 200, destinationY: 100 }) - Math.PI / 2) < 1e-9);
  assert.ok(Math.abs(heading({}, { x: 0, y: -5 })) < 1e-9);
  assert.ok(Math.abs(heading({ destinationX: 100, destinationY: 100 }, { x: -5, y: 0 }) + Math.PI / 2) < 1e-9);
  assert.equal(heading({}, null), null);
});
