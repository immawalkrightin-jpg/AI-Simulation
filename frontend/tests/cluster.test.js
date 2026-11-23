import test from "node:test";
import assert from "node:assert/strict";
import { CLUSTER_PIXELS, clusterPoints, layoutUnits, spreadMembers, spreadRadius } from "../js/maps/cluster.js";

const pt = (id, x, y) => ({ id, x, y });

test("clusterPoints: points closer than the threshold share a cluster", () => {
  const clusters = clusterPoints([pt(1, 100, 100), pt(2, 110, 100), pt(3, 400, 400)], CLUSTER_PIXELS);
  assert.equal(clusters.length, 2);
  const pair = clusters.find((c) => c.members.length === 2);
  assert.deepEqual(pair.members.map((m) => m.id), [1, 2]);
  assert.equal(pair.x, 105);
  assert.equal(pair.y, 100);
});

test("clusterPoints: the threshold is exclusive", () => {
  assert.equal(clusterPoints([pt(1, 0, 0), pt(2, 18, 0)], 18).length, 2);
  assert.equal(clusterPoints([pt(1, 0, 0), pt(2, 17.9, 0)], 18).length, 1);
});

test("clusterPoints: chains merge into one cluster", () => {
  const clusters = clusterPoints([pt(1, 0, 0), pt(2, 15, 0), pt(3, 30, 0), pt(4, 45, 0)], 18);
  assert.equal(clusters.length, 1);
  assert.equal(clusters[0].members.length, 4);
});

test("clusterPoints: every point lands in exactly one cluster", () => {
  const points = [];
  for (let i = 0; i < 40; i++) {
    points.push(pt(i, (i * 37) % 200, (i * 91) % 160));
  }
  const clusters = clusterPoints(points, 18);
  const ids = clusters.flatMap((c) => c.members.map((m) => m.id)).sort((a, b) => a - b);
  assert.deepEqual(ids, points.map((p) => p.id));
});

test("clusterPoints: keys are stable and use the smallest member id", () => {
  const first = clusterPoints([pt(7, 0, 0), pt(3, 5, 0)], 18);
  const second = clusterPoints([pt(3, 0, 0), pt(7, 5, 0)], 18);
  assert.equal(first[0].key, "c3");
  assert.equal(second[0].key, "c3");
});

test("clusterPoints: empty input", () => {
  assert.deepEqual(clusterPoints([], 18), []);
});

test("spreadMembers: members sit on a ring around the centroid", () => {
  const cluster = clusterPoints([pt(1, 100, 100), pt(2, 104, 100), pt(3, 100, 104)], 18)[0];
  const radius = spreadRadius(3);
  const slots = spreadMembers(cluster);
  assert.equal(slots.length, 3);
  for (const slot of slots) {
    assert.ok(Math.abs(Math.hypot(slot.x - cluster.x, slot.y - cluster.y) - radius) < 1e-9);
  }
  const gaps = slots.map((s, i) => {
    const next = slots[(i + 1) % slots.length];
    return Math.hypot(s.x - next.x, s.y - next.y);
  });
  assert.ok(Math.min(...gaps) > 2 * CLUSTER_PIXELS * 0.9, "spread members must not themselves overlap");
});

test("spreadRadius grows with the member count", () => {
  assert.ok(spreadRadius(10) > spreadRadius(3));
  assert.ok(spreadRadius(2) >= 30);
});

test("layoutUnits: singles, badges and spreads", () => {
  const points = [pt(1, 0, 0), pt(2, 5, 0), pt(3, 300, 300), pt(4, 600, 0), pt(5, 604, 3)];
  const collapsed = layoutUnits(points, new Set());
  assert.equal(collapsed.singles.length, 1);
  assert.equal(collapsed.badges.length, 2);
  assert.equal(collapsed.spreads.length, 0);
  const expanded = layoutUnits(points, new Set(["c1"]));
  assert.equal(expanded.badges.length, 1);
  assert.equal(expanded.spreads.length, 1);
  assert.equal(expanded.spreads[0].slots.length, 2);
});

test("layoutUnits: after spreading no two symbols are within the cluster threshold", () => {
  const points = [pt(1, 100, 100), pt(2, 103, 101), pt(3, 99, 104), pt(4, 104, 98), pt(5, 101, 106)];
  const layout = layoutUnits(points, new Set(["c1"]));
  const slots = layout.spreads[0].slots;
  for (let i = 0; i < slots.length; i++) {
    for (let j = i + 1; j < slots.length; j++) {
      assert.ok(Math.hypot(slots[i].x - slots[j].x, slots[i].y - slots[j].y) >= CLUSTER_PIXELS);
    }
  }
});
