import test from "node:test";
import assert from "node:assert/strict";
import { DISPLAY_BOUNDS, entityAt, fromDisplay, pointInRings, toDisplay } from "../js/maps/geo.js";
import { DEFAULT_TILT, MAX_TILT, Projection } from "../js/maps/projection.js";

const near = (a, b, eps, message) => assert.ok(Math.abs(a - b) <= eps, `${message || ""} expected ${a} ~ ${b} (eps ${eps})`);

function camera(overrides = {}) {
  const p = new Projection();
  p.reducedMotion = true;
  p.resize(1280, 800);
  p.set({ center: { x: 1100, y: 250 }, zoom: 2.5, tilt: DEFAULT_TILT, yaw: 0, ...overrides });
  return p;
}

test("geo: toDisplay and fromDisplay round-trip across the drawable world", () => {
  for (let x = 0; x <= 2000; x += 125) {
    for (let y = 56; y <= 830; y += 37) {
      const d = toDisplay(x, y);
      const back = fromDisplay(d.x, d.y);
      near(back.x, x, 1e-9, "x");
      near(back.y, y, 1e-6, "y");
    }
  }
});

test("geo: x is unchanged and the equator stays put", () => {
  assert.equal(toDisplay(321, 500).x, 321);
  near(toDisplay(321, 500).y, 500, 1e-9);
});

test("geo: north is up in display space", () => {
  assert.ok(toDisplay(1000, 150).y < toDisplay(1000, 250).y);
});

test("geo: display y stretches by sec(lat), so land is conformal on the sphere", () => {
  // The engine grid is equirectangular (equal units per degree in x and y), so Mercator display makes
  // the local y/x scale equal to 1 / cos(lat). That is what makes land shapes look right.
  // Consequence, documented in frontend.md: an engine-space circle (sensor range) is NOT a circle in
  // display space; it is an ellipse stretched north-south by the same factor.
  for (const y of [100, 200, 300, 500, 700]) {
    const r = 0.05;
    const vertical = toDisplay(1000, y + r).y - toDisplay(1000, y - r).y;
    const horizontal = toDisplay(1000 + r, y).x - toDisplay(1000 - r, y).x;
    const lat = 90 - (y / 1000) * 180;
    near(vertical / horizontal, 1 / Math.cos((lat * Math.PI) / 180), 0.01, `aspect at y=${y}`);
  }
});

test("geo: latitude is clamped so the poles stay finite", () => {
  assert.ok(Number.isFinite(toDisplay(0, 0).y));
  assert.ok(Number.isFinite(toDisplay(0, 1000).y));
  assert.ok(DISPLAY_BOUNDS.minY < DISPLAY_BOUNDS.maxY);
});

test("projection: project then unproject returns the same ground point", () => {
  for (const tilt of [0, 0.4, DEFAULT_TILT, MAX_TILT]) {
    for (const yaw of [0, 0.3, -0.6]) {
      const p = camera({ tilt, yaw });
      for (let dx = -300; dx <= 300; dx += 60) {
        for (let dy = -180; dy <= 180; dy += 45) {
          const point = { x: p.center.x + dx, y: p.center.y + dy };
          const s = p.project(point.x, point.y, 0);
          const back = p.unproject(s.x, s.y);
          assert.ok(back, "unproject returned null");
          near(back.x, point.x, 1e-6, `x tilt=${tilt} yaw=${yaw}`);
          near(back.y, point.y, 1e-6, `y tilt=${tilt} yaw=${yaw}`);
        }
      }
    }
  }
});

test("projection: engine round-trip through the display transform", () => {
  const p = camera();
  for (let x = 1000; x <= 1250; x += 50) {
    for (let y = 120; y <= 330; y += 30) {
      const s = p.projectWorld(x, y);
      const back = p.unprojectWorld(s.x, s.y);
      near(back.x, x, 1e-6, "x");
      near(back.y, y, 1e-6, "y");
    }
  }
});

test("projection: north is up and the near edge is larger than the far edge", () => {
  const p = camera();
  const north = p.project(p.center.x, p.center.y - 100);
  const south = p.project(p.center.x, p.center.y + 100);
  assert.ok(north.y < south.y, "north should be above south");
  assert.ok(south.k > north.k, "near ground should be larger than far ground");
});

test("projection: scale ratio across the screen is at most about 1.5", () => {
  for (const tilt of [DEFAULT_TILT, MAX_TILT]) {
    for (const zoom of [0.5, 2, 8]) {
      const p = camera({ tilt, zoom, center: { x: 1000, y: 500 } });
      const top = p.unproject(p.originX, 0);
      const bottom = p.unproject(p.originX, p.viewHeight);
      const ratio = p.project(bottom.x, bottom.y).k / p.project(top.x, top.y).k;
      assert.ok(ratio > 1, `ratio ${ratio} should exceed 1 when tilted`);
      assert.ok(ratio <= 1.55, `ratio ${ratio} at tilt ${tilt} zoom ${zoom}`);
    }
  }
});

test("projection: top-down has no perspective", () => {
  const p = camera({ tilt: 0 });
  const a = p.project(p.center.x - 50, p.center.y - 100);
  const b = p.project(p.center.x + 50, p.center.y + 100);
  near(a.k, b.k, 1e-9);
});

test("projection: zoom to cursor keeps the point under the cursor fixed", () => {
  const p = camera();
  const before = p.unproject(900, 300);
  p.zoomAt(900, 300, 1.8);
  const after = p.unproject(900, 300);
  near(after.x, before.x, 1e-3, "x");
  near(after.y, before.y, 1e-3, "y");
});

test("projection: dragging keeps the grabbed point under the cursor", () => {
  const p = camera();
  p.beginDrag(600, 400);
  const grabbed = p.grabbed;
  p.drag(700, 450);
  const under = p.unproject(700, 450);
  near(under.x, grabbed.x, 1e-3, "x");
  near(under.y, grabbed.y, 1e-3, "y");
  p.endDrag();
});

test("projection: pan and zoom are clamped", () => {
  const p = camera();
  p.set({ center: { x: 99999, y: -99999 }, zoom: 9999 });
  assert.ok(p.center.x <= DISPLAY_BOUNDS.maxX + 150);
  assert.ok(p.center.y >= DISPLAY_BOUNDS.minY - 150);
  assert.ok(p.zoom <= 14);
  p.set({ zoom: 1e-9 });
  assert.ok(p.zoom >= p.minZoom - 1e-12);
});

test("projection: the world frame fits and the panel inset shifts the origin", () => {
  const p = camera();
  p.setInset({ left: 280 });
  p.set(p.frame("world"));
  const a = p.project(DISPLAY_BOUNDS.minX, 500);
  const b = p.project(DISPLAY_BOUNDS.maxX, 500);
  assert.ok(a.x >= 280 - 1, `west edge ${a.x} should clear the panel`);
  assert.ok(b.x <= p.viewWidth + 1, `east edge ${b.x} should fit`);
  p.set(p.frame("theatre"));
  assert.ok(p.originX > 280, "origin sits right of the left panel");
});

test("projection: flyTo interpolates and can be interrupted", () => {
  const p = camera();
  p.reducedMotion = false;
  p.flyTo({ center: { x: 1200, y: 200 }, zoom: 4 }, 1000);
  assert.ok(p.flying);
  p.update(performance.now() + 500);
  assert.ok(p.zoom > 2.5 && p.zoom < 4);
  p.cancelFlight();
  assert.ok(!p.flying);
  p.flyTo({ center: { x: 1200, y: 200 }, zoom: 4 }, 100);
  p.update(performance.now() + 5000);
  near(p.zoom, 4, 1e-9);
  assert.ok(!p.flying);
});

test("picking: point in polygon with the even-odd rule", () => {
  const square = [0, 0, 10, 0, 10, 10, 0, 10];
  const hole = [3, 3, 7, 3, 7, 7, 3, 7];
  assert.equal(pointInRings([square], 5, 5), true);
  assert.equal(pointInRings([square], 11, 5), false);
  assert.equal(pointInRings([square, hole], 5, 5), false);
  assert.equal(pointInRings([square, hole], 1, 1), true);
});

test("picking: entityAt finds the country by area, not by nearest capital", () => {
  const map = {
    entities: [
      { id: "AAA", name: "A", rings: [[0, 0, 100, 0, 100, 100, 0, 100]] },
      { id: "", name: "Unaffiliated", rings: [[100, 0, 200, 0, 200, 100, 100, 100]] },
      { id: "ATA", name: "ATA", rings: [[0, 100, 200, 100, 200, 200, 0, 200]] }
    ]
  };
  assert.equal(entityAt(map, 10, 90).id, "AAA");
  assert.equal(entityAt(map, 150, 50).id, "");
  assert.equal(entityAt(map, 500, 50), null);
  assert.equal(entityAt(map, 50, 150), null, "Antarctica is skipped");
});
