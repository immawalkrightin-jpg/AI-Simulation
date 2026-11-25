import test from "node:test";
import assert from "node:assert/strict";
import { chainOf, handOffs, legIndexAt, legPath, positionAt, progressOf, routeLegs, segmentsBetween, subdivide } from "../js/maps/route.js";

const legs = [
  { carrier: "Truck", mode: "Ground", start: 0, end: 0.2, points: [0, 0, 10, 0] },
  { carrier: "CargoShip", mode: "Naval", start: 0.2, end: 0.8, points: [10, 0, 10, 30, 40, 30] },
  { carrier: "Helicopter", mode: "Air", start: 0.8, end: 1, points: [40, 30, 40, 40] }
];

const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-9, `${actual} is not ${expected}`);

test("routeLegs: absent, empty or malformed routes give null", () => {
  assert.equal(routeLegs(null), null);
  assert.equal(routeLegs({}), null);
  assert.equal(routeLegs({ route: { legs: [] } }), null);
  assert.equal(routeLegs({ route: {} }), null);
  assert.equal(routeLegs({ route: { legs } }), legs);
});

test("progressOf: runs from start to due, freezes at the end tick, stays in range", () => {
  const item = { startTick: 100, dueTick: 200, endTick: null };
  near(progressOf(item, 150), 0.5);
  near(progressOf(item, 400), 1);
  near(progressOf(item, 10), 0);
  near(progressOf({ ...item, endTick: 120 }, 999), 0.2);
  near(progressOf({ startTick: 5, dueTick: 5, endTick: null }, 9), 1);
});

test("legIndexAt: a boundary belongs to the earlier leg and out-of-range progress clamps", () => {
  assert.equal(legIndexAt(legs, 0), 0);
  assert.equal(legIndexAt(legs, 0.2), 0);
  assert.equal(legIndexAt(legs, 0.5), 1);
  assert.equal(legIndexAt(legs, 0.9), 2);
  assert.equal(legIndexAt(legs, 1), 2);
  assert.equal(legIndexAt(legs, 5), 2);
  assert.equal(legIndexAt(legs, -1), 0);
});

test("positionAt: walks each leg at its own share of the trip and reports the carrier", () => {
  const start = positionAt(legs, 0);
  assert.deepEqual([start.x, start.y, start.carrier, start.leg], [0, 0, "Truck", 0]);
  const first = positionAt(legs, 0.1);
  near(first.x, 5);
  near(first.y, 0);
  const handOff = positionAt(legs, 0.2);
  assert.deepEqual([handOff.x, handOff.y, handOff.carrier], [10, 0, "Truck"]);
  const corner = positionAt(legs, 0.5);
  near(corner.x, 10);
  near(corner.y, 30);
  assert.equal(corner.carrier, "CargoShip");
  const along = positionAt(legs, 0.65);
  near(along.x, 25);
  near(along.y, 30);
  const end = positionAt(legs, 1);
  assert.deepEqual([end.x, end.y, end.carrier, end.mode], [40, 40, "Helicopter", "Air"]);
});

test("legPath: a slice of a leg keeps the vertices it passes", () => {
  const ship = legs[1];
  const whole = legPath(ship, 0, 1);
  assert.equal(whole.length, 3);
  const slice = legPath(ship, 0.25, 0.75);
  assert.equal(slice.length, 3);
  near(slice[0].x, 10);
  near(slice[0].y, 15);
  assert.deepEqual([slice[1].x, slice[1].y], [10, 30]);
  near(slice[2].x, 25);
  near(slice[2].y, 30);
});

test("segmentsBetween: covers exactly the legs the range touches", () => {
  const travelled = segmentsBetween(legs, 0, 0.5);
  assert.deepEqual(travelled.map((s) => s.carrier), ["Truck", "CargoShip"]);
  assert.deepEqual(travelled.map((s) => s.mode), ["Ground", "Naval"]);
  const ahead = segmentsBetween(legs, 0.5, 1);
  assert.deepEqual(ahead.map((s) => s.carrier), ["CargoShip", "Helicopter"]);
  assert.deepEqual(segmentsBetween(legs, 0.5, 0.5), []);
  assert.equal(segmentsBetween(legs, 0, 1).length, 3);
  assert.equal(segmentsBetween(legs, 1, 0).length, 3);
});

test("handOffs: one per change of carrier, at the start of the next leg", () => {
  const stops = handOffs(legs);
  assert.equal(stops.length, 2);
  assert.deepEqual([stops[0].x, stops[0].y, stops[0].from, stops[0].to, stops[0].at], [10, 0, "Truck", "CargoShip", 0.2]);
  assert.deepEqual([stops[1].x, stops[1].y, stops[1].from, stops[1].to, stops[1].at], [40, 30, "CargoShip", "Helicopter", 0.8]);
  assert.deepEqual(handOffs([legs[0]]), []);
});

test("chainOf: carriers in order, repeats collapsed", () => {
  assert.deepEqual(chainOf(legs), ["Truck", "CargoShip", "Helicopter"]);
  assert.deepEqual(chainOf([{ carrier: "Truck" }, { carrier: "Truck" }, { carrier: "CargoShip" }]), ["Truck", "CargoShip"]);
});

test("a single-leg route has a position and no hand-off", () => {
  const single = [{ carrier: "CargoPlane", mode: "Air", start: 0, end: 1, points: [0, 0, 100, 0] }];
  const middle = positionAt(single, 0.5);
  assert.deepEqual([middle.x, middle.y, middle.carrier], [50, 0, "CargoPlane"]);
  assert.equal(handOffs(single).length, 0);
});

test("a zero-length share does not break positions", () => {
  const odd = [
    { carrier: "Truck", mode: "Ground", start: 0, end: 0, points: [0, 0, 5, 0] },
    { carrier: "CargoShip", mode: "Naval", start: 0, end: 1, points: [5, 0, 15, 0] }
  ];
  const at = positionAt(odd, 0.5);
  assert.equal(at.carrier, "CargoShip");
  near(at.x, 10);
});

test("subdivide: long segments are cut so the display transform can bend them", () => {
  const cut = subdivide([{ x: 0, y: 0 }, { x: 100, y: 0 }], 10);
  assert.equal(cut.length, 11);
  assert.deepEqual(cut[0], { x: 0, y: 0 });
  near(cut[10].x, 100);
  assert.equal(subdivide([{ x: 1, y: 1 }], 10).length, 1);
  assert.equal(subdivide([{ x: 0, y: 0 }, { x: 3, y: 0 }], 10).length, 2);
});
