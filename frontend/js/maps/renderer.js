import { DEFAULT_TILT, Projection } from "./projection.js";
import { WORLD_HEIGHT, WORLD_WIDTH, displayRect, entityAt, pointInRings, toDisplay } from "./geo.js";
import { buildLand } from "./land.js";
import { classifyExternal, countryFrame, ringsBounds } from "./view.js";
import { fadeLabels, placeLabels } from "./labels.js";
import { layoutUnits } from "./cluster.js";
import { carrierOf, screenHeading, unitHeading } from "./motion.js";
import { drawSymbol, symbolName } from "../units/symbols.js";
import { DASH, backing, clusterBadge, foreignRing, glyph, headingTick, hoverRing, strengthBar } from "../units/markers.js";
import { FONT, FONT_SMALL, HALO, INK, INK_RGB, MID, MID_RGB, RED, RED_RGB, TEAL_RGB, rgba } from "../theme.js";

const TRAIL_LENGTH = 24;
const FADE_TICKS = 8;
const PICK_RADIUS = 14;
const DIM = 0.4;
const INTERIOR_FADE_MS = 300;
const FOCUS_PADDING = 0.22;
const COUNTRY_SYMBOL = 9;
const WORLD_SYMBOL = 6;
const PRESENT_INTERVAL_MS = 300;
const FRAME_WINDOW = 120;
const HOVER_EVERY = 4;

const MODE_LINE = { Ground: "line:supply-ground", Naval: "line:supply-naval", Air: "line:supply-air" };

const sensorName = (kind) => `${kind.charAt(0).toUpperCase()}${kind.slice(1).toLowerCase()}`;

export class MapRenderer {
  constructor(canvas, handlers = {}) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.onSelect = handlers.onSelect || (() => {});
    this.onView = handlers.onView || (() => {});
    this.onHover = handlers.onHover || (() => {});
    this.onPresent = handlers.onPresent || (() => {});
    this.projection = new Projection();
    this.snapshot = null;
    this.map = null;
    this.mapRev = 0;
    this.territory = new Map();
    this.countryIndex = new Map();
    this.modelId = null;
    this.units = new Map();
    this.external = new Set();
    this.selected = null;
    this.hover = null;
    this.hoverKey = "";
    this.flash = null;
    this.interval = 1000;
    this.lastSnapshotAt = 0;
    this.lastFrameAt = 0;
    this.frameCount = 0;
    this.drag = null;
    this.moved = false;
    this.pointer = null;
    this.pointerVersion = -1;
    this.touched = false;
    this.preset = null;
    this.view = { mode: "world", focus: null };
    this.interiorFocus = null;
    this.interiorAlpha = 0;
    this.focusZoom = 0;
    this.returnCam = null;
    this.cache = { version: -1, mapRev: -1, land: null, grid: null };
    this.patterns = null;
    this.glow = null;
    this.widths = new Map();
    this.labelFade = new Map();
    this.labelLast = new Map();
    this.expanded = new Set();
    this.slots = [];
    this.nextSlots = [];
    this.present = new Set();
    this.presentSignature = "";
    this.presentMode = "";
    this.presentAt = 0;
    this.queued = [];
    this.times = [];
    this.intervals = [];
    this.showStats = false;
    const query = typeof matchMedia === "function" ? matchMedia("(prefers-reduced-motion: reduce)") : null;
    this.reduced = query ? query.matches : false;
    if (query && query.addEventListener) {
      query.addEventListener("change", (event) => {
        this.reduced = event.matches;
      });
    }
    this.bind();
    this.resize();
    this.showPreset("theatre", { instant: true });
    requestAnimationFrame((now) => this.frame(now));
  }

  setInset(inset) {
    this.projection.setInset(inset);
    if (this.view.mode === "country") {
      this.flyToFocus(400);
    } else if (!this.touched) {
      this.showPreset("theatre", { instant: true });
    }
  }

  showPreset(name, { instant = false } = {}) {
    const target = this.projection.frame(name, DEFAULT_TILT);
    if (instant) {
      this.projection.set(target);
    } else {
      this.projection.flyTo(target, 800);
    }
    this.preset = name;
  }

  // A named framing from the floating buttons. Leaves country view first, so the dossier closes with it.
  goPreset(name) {
    this.touched = true;
    if (this.view.mode === "country") {
      this.exitCountry({ restore: false });
    }
    this.showPreset(name);
  }

  zoomBy(factor) {
    this.touched = true;
    this.projection.flyTo({ zoom: this.projection.zoom * factor }, 260);
  }

  toggleTilt() {
    this.touched = true;
    this.projection.flyTo({ tilt: this.projection.tilt > 0.05 ? 0 : DEFAULT_TILT }, 500);
  }

  reset() {
    this.touched = false;
    if (this.view.mode === "country") {
      this.exitCountry({ restore: false });
    }
    this.showPreset("theatre");
  }

  enterCountry(id) {
    if (!this.countryIndex.has(id)) {
      return;
    }
    if (this.view.mode === "world") {
      const pr = this.projection;
      this.returnCam = { center: { ...pr.center }, zoom: pr.zoom, tilt: pr.tilt, yaw: pr.yaw };
    }
    this.expanded.clear();
    this.view = { mode: "country", focus: id };
    this.touched = true;
    this.flyToFocus(800);
    this.emitView();
  }

  exitCountry({ restore = true } = {}) {
    if (this.view.mode !== "country") {
      return;
    }
    this.view = { mode: "world", focus: null };
    this.expanded.clear();
    if (this.selected && this.selected.type !== "unit") {
      this.selected = null;
      this.onSelect(null);
    }
    if (restore) {
      if (this.returnCam) {
        this.projection.flyTo(this.returnCam, 800);
      } else {
        this.showPreset("theatre");
      }
    }
    this.returnCam = null;
    this.emitView();
  }

  emitView() {
    const country = this.view.focus ? this.countryIndex.get(this.view.focus) : null;
    this.onView({ mode: this.view.mode, focus: this.view.focus, name: country ? country.name : null });
  }

  flyToFocus(ms = 800) {
    const country = this.countryIndex.get(this.view.focus);
    if (!country || !this.snapshot) {
      return;
    }
    const units = this.snapshot.units.filter((u) => u.owner === country.id);
    const sensors = this.snapshot.sensors.filter((s) => s.owner === country.id);
    const territory = this.territory.get(country.id);
    const frame = countryFrame(country, units, sensors, territory ? territory.bbox : null);
    const fit = this.projection.fitRect(displayRect(frame.x0, frame.y0, frame.x1, frame.y1), FOCUS_PADDING, DEFAULT_TILT);
    this.focusZoom = Math.min(14, fit.zoom);
    this.projection.flyTo({ center: fit.center, zoom: fit.zoom, tilt: DEFAULT_TILT, yaw: 0 }, ms);
  }

  local(event) {
    const rect = this.canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  bind() {
    window.addEventListener("resize", () => this.resize());
    window.addEventListener("keydown", (event) => {
      if (event.target && /input|select|textarea/i.test(event.target.tagName)) {
        return;
      }
      if (event.key === "Escape" && this.view.mode === "country") {
        this.exitCountry();
      } else if (event.key === "p" || event.key === "P") {
        this.showStats = !this.showStats;
      }
    });
    this.canvas.style.cursor = "grab";
    this.canvas.addEventListener("pointerdown", (event) => {
      this.canvas.setPointerCapture(event.pointerId);
      const at = this.local(event);
      this.drag = { x: event.clientX, y: event.clientY };
      this.moved = false;
      this.projection.beginDrag(at.x, at.y);
    });
    this.canvas.addEventListener("pointermove", (event) => {
      this.pointer = this.local(event);
      if (!this.drag) {
        return;
      }
      if (Math.abs(event.clientX - this.drag.x) + Math.abs(event.clientY - this.drag.y) > 4) {
        this.moved = true;
        this.canvas.style.cursor = "grabbing";
      }
      if (this.moved) {
        this.touched = true;
        this.projection.drag(this.pointer.x, this.pointer.y);
      }
    });
    const release = (event) => {
      if (!this.drag) {
        return;
      }
      this.drag = null;
      this.projection.endDrag();
      this.pointerVersion = -1;
      if (!this.moved && event.type === "pointerup") {
        this.pick(event.clientX, event.clientY);
      }
    };
    this.canvas.addEventListener("pointerup", release);
    this.canvas.addEventListener("pointercancel", release);
    this.canvas.addEventListener("pointerleave", () => {
      this.pointer = null;
      this.setHover(null);
      if (!this.drag) {
        this.canvas.style.cursor = "grab";
      }
    });
    this.canvas.addEventListener(
      "wheel",
      (event) => {
        event.preventDefault();
        this.touched = true;
        const at = this.local(event);
        this.projection.zoomAt(at.x, at.y, Math.exp(-event.deltaY * 0.0015));
      },
      { passive: false }
    );
  }

  resize() {
    const ratio = window.devicePixelRatio || 1;
    const width = this.canvas.clientWidth;
    const height = this.canvas.clientHeight;
    this.canvas.width = width * ratio;
    this.canvas.height = height * ratio;
    this.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    this.projection.resize(width, height);
    if (this.view.mode === "country") {
      this.flyToFocus(0);
    } else if (!this.touched && this.preset) {
      this.showPreset(this.preset, { instant: true });
    }
  }

  setMap(map) {
    this.map = map && Array.isArray(map.entities) ? map : null;
    this.mapRev++;
    this.territory = new Map();
    if (this.map) {
      for (const entity of this.map.entities) {
        if (entity.id !== "" && entity.name !== "ATA") {
          this.territory.set(entity.id, { rings: entity.rings, bbox: ringsBounds(entity.rings) });
        }
      }
    }
    if (this.snapshot) {
      this.external = classifyExternal(this.snapshot, (id) => this.rings(id));
    }
  }

  rings(id) {
    const entry = this.territory.get(id);
    return entry ? entry.rings : null;
  }

  setSnapshot(snapshot) {
    const now = performance.now();
    if (!this.snapshot && (snapshot.width !== WORLD_WIDTH || snapshot.height !== WORLD_HEIGHT)) {
      console.warn(`Engine world is ${snapshot.width}x${snapshot.height}; geo.js assumes ${WORLD_WIDTH}x${WORLD_HEIGHT}.`);
    }
    this.countryIndex = new Map(snapshot.countries.map((country) => [country.id, country]));
    const model = snapshot.countries.find((country) => country.isModel);
    this.modelId = model ? model.id : null;
    if (this.lastSnapshotAt > 0) {
      this.interval = Math.min(5000, Math.max(100, now - this.lastSnapshotAt));
    }
    this.lastSnapshotAt = now;
    const present = new Set();
    for (const unit of snapshot.units) {
      present.add(unit.id);
      let entry = this.units.get(unit.id);
      if (!entry) {
        entry = { x: unit.x, y: unit.y, trail: [], max: unit.strength, facing: null };
        this.units.set(unit.id, entry);
      }
      if (entry.toX !== undefined && (entry.toX !== unit.x || entry.toY !== unit.y)) {
        entry.trail.push({ x: entry.toX, y: entry.toY });
        entry.facing = { x: unit.x - entry.toX, y: unit.y - entry.toY };
        if (entry.trail.length > TRAIL_LENGTH) {
          entry.trail.shift();
        }
      }
      entry.fromX = entry.x;
      entry.fromY = entry.y;
      entry.toX = unit.x;
      entry.toY = unit.y;
      entry.start = now;
      entry.max = Math.max(entry.max, unit.strength);
      entry.data = unit;
    }
    for (const id of this.units.keys()) {
      if (!present.has(id)) {
        this.units.delete(id);
      }
    }
    this.snapshot = snapshot;
    this.external = classifyExternal(snapshot, (id) => this.rings(id));
    if (this.view.mode === "country" && !this.countryIndex.has(this.view.focus)) {
      this.exitCountry();
    }
  }

  unitMax(id) {
    const entry = this.units.get(id);
    return entry ? entry.max : 0;
  }

  animate(fn) {
    this.queued.push(fn);
  }

  phase(now, period) {
    return this.reduced ? 0.45 : (now / period) % 1;
  }

  frame(now) {
    requestAnimationFrame((next) => this.frame(next));
    const started = performance.now();
    const ctx = this.ctx;
    const pr = this.projection;
    const dt = this.lastFrameAt ? Math.min(100, now - this.lastFrameAt) : 16;
    this.lastFrameAt = now;
    this.frameCount++;
    pr.update(now);
    ctx.clearRect(0, 0, pr.viewWidth, pr.viewHeight);
    if (!this.snapshot) {
      return;
    }
    this.nextSlots = [];
    this.present = new Set();
    this.queued = [];
    this.updateInterior(dt);
    this.updateCache();
    this.updateHover();
    const P = (x, y, z = 0) => pr.projectWorld(x, y, z);
    this.drawGraticule(P);
    if (this.cache.land) {
      this.drawLand(now);
    }
    this.drawRelations(P);
    this.drawInteractions(P, now);
    if (this.interiorAlpha > 0.01 && this.interiorFocus) {
      this.drawSensors(P);
    }
    this.drawUnits(P, now);
    this.drawCapitals(P);
    this.drawLabels(P, dt);
    this.drawAnimated(now);
    this.drawReticle(now);
    this.drawCompass();
    this.slots = this.nextSlots;
    this.emitPresent(now);
    this.record(started, dt);
    if (this.showStats) {
      this.drawStats();
    }
  }

  record(started, dt) {
    this.times.push(performance.now() - started);
    this.intervals.push(dt);
    if (this.times.length > FRAME_WINDOW) {
      this.times.shift();
      this.intervals.shift();
    }
  }

  stats() {
    const n = this.times.length;
    if (n === 0) {
      return null;
    }
    const sorted = [...this.times].sort((a, b) => a - b);
    const mean = (list) => list.reduce((sum, value) => sum + value, 0) / list.length;
    return {
      frames: n,
      workAvg: mean(this.times),
      workP95: sorted[Math.min(n - 1, Math.floor(n * 0.95))],
      workMax: sorted[n - 1],
      fps: 1000 / mean(this.intervals)
    };
  }

  drawStats() {
    const stats = this.stats();
    if (!stats) {
      return;
    }
    const ctx = this.ctx;
    const pr = this.projection;
    ctx.font = FONT_SMALL;
    const lines = [
      `${stats.fps.toFixed(0)} fps over ${stats.frames} frames`,
      `draw avg ${stats.workAvg.toFixed(2)} ms  p95 ${stats.workP95.toFixed(2)} ms  max ${stats.workMax.toFixed(2)} ms`
    ];
    lines.forEach((line, index) => {
      this.text(line, pr.viewWidth - pr.inset.right - 14, pr.viewHeight - 120 + index * 14, MID, "right");
    });
  }

  emitPresent(now) {
    if (now - this.presentAt < PRESENT_INTERVAL_MS) {
      return;
    }
    this.presentAt = now;
    const signature = [...this.present].sort().join("|");
    if (signature === this.presentSignature && this.presentMode === this.view.mode) {
      return;
    }
    this.presentSignature = signature;
    this.presentMode = this.view.mode;
    this.onPresent({ mode: this.view.mode, ids: new Set(this.present) });
  }

  drawAnimated(now) {
    for (const fn of this.queued) {
      fn(now);
    }
    this.queued = [];
    this.ctx.globalAlpha = 1;
    this.ctx.setLineDash([]);
    this.ctx.lineWidth = 1;
  }

  updateInterior(dt) {
    const pr = this.projection;
    if (this.view.focus && this.view.focus !== this.interiorFocus) {
      this.interiorFocus = this.view.focus;
      this.interiorAlpha = 0;
    }
    const near = this.view.mode === "country" && pr.zoom >= this.focusZoom * 0.8;
    const step = dt / INTERIOR_FADE_MS;
    this.interiorAlpha = near ? Math.min(1, this.interiorAlpha + step) : Math.max(0, this.interiorAlpha - step);
    if (!this.view.focus && this.interiorAlpha === 0) {
      this.interiorFocus = null;
    }
  }

  updateCache() {
    const pr = this.projection;
    const cache = this.cache;
    if (cache.version === pr.version && cache.mapRev === this.mapRev) {
      return;
    }
    const P = (x, y, z = 0) => pr.projectWorld(x, y, z);
    cache.land = this.map ? buildLand(this.map, P) : null;
    cache.grid = this.buildGraticule(P);
    cache.version = pr.version;
    cache.mapRev = this.mapRev;
  }

  buildGraticule(P) {
    const minor = new Path2D();
    const major = new Path2D();
    const top = ((90 - 80) / 180) * WORLD_HEIGHT;
    const bottom = ((90 + 60) / 180) * WORLD_HEIGHT;
    for (let lon = -180; lon <= 180; lon += 10) {
      const x = ((lon + 180) / 360) * WORLD_WIDTH;
      const a = P(x, top);
      const b = P(x, bottom);
      const path = lon % 30 === 0 ? major : minor;
      path.moveTo(a.x, a.y);
      path.lineTo(b.x, b.y);
    }
    for (let lat = -60; lat <= 80; lat += 10) {
      const y = ((90 - lat) / 180) * WORLD_HEIGHT;
      const a = P(0, y);
      const b = P(WORLD_WIDTH, y);
      const path = lat % 30 === 0 ? major : minor;
      path.moveTo(a.x, a.y);
      path.lineTo(b.x, b.y);
    }
    return { minor, major };
  }

  makePatterns() {
    const make = (size, draw) => {
      const c = document.createElement("canvas");
      c.width = size;
      c.height = size;
      draw(c.getContext("2d"), size);
      return this.ctx.createPattern(c, "repeat");
    };
    return {
      East: make(7, (g, s) => {
        g.strokeStyle = rgba(INK_RGB, 0.2);
        g.lineWidth = 1;
        g.beginPath();
        g.moveTo(0, s);
        g.lineTo(s, 0);
        g.moveTo(-1, 1);
        g.lineTo(1, -1);
        g.moveTo(s - 1, s + 1);
        g.lineTo(s + 1, s - 1);
        g.stroke();
      }),
      West: make(6, (g, s) => {
        g.fillStyle = rgba(INK_RGB, 0.3);
        g.beginPath();
        g.arc(s / 2, s / 2, 0.9, 0, Math.PI * 2);
        g.fill();
      })
    };
  }

  makeGlow() {
    const size = 96;
    const c = document.createElement("canvas");
    c.width = size;
    c.height = size;
    const g = c.getContext("2d");
    const gradient = g.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
    gradient.addColorStop(0, rgba(RED_RGB, 0.5));
    gradient.addColorStop(0.4, rgba(RED_RGB, 0.14));
    gradient.addColorStop(1, rgba(RED_RGB, 0));
    g.fillStyle = gradient;
    g.fillRect(0, 0, size, size);
    return c;
  }

  dimOf(id) {
    return this.view.mode === "country" && id !== this.view.focus ? DIM : 1;
  }

  drawLand(now) {
    const ctx = this.ctx;
    if (!this.patterns) {
      this.patterns = this.makePatterns();
    }
    const parts = this.cache.land.parts;
    for (const { entity, path } of parts) {
      ctx.globalAlpha = this.dimOf(entity.id);
      ctx.fillStyle = rgba(MID_RGB, entity.bloc === "NonAligned" ? 0.1 : entity.id === "" ? 0.05 : 0.06);
      ctx.fill(path);
      const pattern = this.patterns[entity.bloc];
      if (pattern) {
        ctx.fillStyle = pattern;
        ctx.fill(path);
      }
    }
    ctx.lineJoin = "round";
    for (const { entity, path } of parts) {
      const id = entity.id;
      const focused = id !== "" && (id === this.view.focus || this.isSelected("country", id));
      const hero = id !== "" && id === this.modelId;
      const hovered = this.hover && this.hover.type === "country" && this.hover.id === id;
      ctx.globalAlpha = this.dimOf(id);
      if (hero || focused) {
        ctx.lineWidth = 6;
        ctx.strokeStyle = hero ? rgba(RED_RGB, 0.12) : rgba(INK_RGB, 0.1);
        ctx.stroke(path);
      }
      ctx.lineWidth = focused ? 1.7 : hovered ? 1.3 : 0.8;
      ctx.strokeStyle = hero ? rgba(RED_RGB, 0.85) : rgba(INK_RGB, focused ? 0.95 : hovered ? 0.7 : 0.4);
      ctx.stroke(path);
    }
    if (this.flash) {
      const age = (now - this.flash.start) / 500;
      if (age >= 1) {
        this.flash = null;
      } else {
        const part = parts.find((item) => item.entity === this.flash.entity);
        if (part) {
          ctx.globalAlpha = 1;
          ctx.fillStyle = rgba(INK_RGB, 0.14 * (1 - age));
          ctx.fill(part.path);
        }
      }
    }
    ctx.globalAlpha = 1;
    ctx.lineWidth = 1;
  }

  drawGraticule(P) {
    const ctx = this.ctx;
    const pr = this.projection;
    const grid = this.cache.grid;
    if (!grid) {
      return;
    }
    ctx.lineWidth = 1;
    ctx.strokeStyle = rgba(MID_RGB, 0.07);
    ctx.stroke(grid.minor);
    ctx.strokeStyle = rgba(MID_RGB, 0.15);
    ctx.stroke(grid.major);
    const west = pr.unprojectWorld(pr.inset.left + 34, pr.viewHeight * 0.6);
    const south = pr.unprojectWorld(pr.originX, pr.viewHeight - pr.inset.bottom - 26);
    ctx.font = FONT_SMALL;
    ctx.fillStyle = rgba(INK_RGB, 0.3);
    ctx.textAlign = "left";
    if (west) {
      const x = Math.min(WORLD_WIDTH, Math.max(0, west.x));
      for (let lat = -60; lat <= 80; lat += 30) {
        const p = P(x, ((90 - lat) / 180) * WORLD_HEIGHT);
        ctx.fillText(`${Math.abs(lat)}°${lat < 0 ? "S" : lat > 0 ? "N" : ""}`, p.x + 4, p.y - 3);
      }
    }
    if (south) {
      const y = Math.min(WORLD_HEIGHT, Math.max(0, south.y));
      ctx.textAlign = "center";
      for (let lon = -150; lon <= 180; lon += 30) {
        const p = P(((lon + 180) / 360) * WORLD_WIDTH, y);
        if (p.x > pr.inset.left + 20 && p.x < pr.viewWidth - pr.inset.right - 20) {
          ctx.fillText(`${Math.abs(lon)}°${lon < 0 ? "W" : lon > 0 ? "E" : ""}`, p.x, p.y + 12);
        }
      }
      ctx.textAlign = "left";
    }
  }

  drawCompass() {
    const pr = this.projection;
    if (Math.abs(pr.yaw) < 0.01) {
      return;
    }
    const ctx = this.ctx;
    ctx.save();
    ctx.translate(pr.viewWidth - pr.inset.right - 44, pr.viewHeight - pr.inset.bottom - 150);
    ctx.rotate(pr.yaw);
    ctx.strokeStyle = rgba(INK_RGB, 0.6);
    ctx.fillStyle = rgba(INK_RGB, 0.6);
    ctx.beginPath();
    ctx.arc(0, 0, 14, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, -12);
    ctx.lineTo(4, 2);
    ctx.lineTo(-4, 2);
    ctx.closePath();
    ctx.fill();
    ctx.font = FONT;
    ctx.textAlign = "center";
    ctx.fillText("N", 0, -18);
    ctx.restore();
  }

  px(pixels) {
    return pixels / this.projection.zoom;
  }

  ring(P, x, y, radius, z) {
    const ctx = this.ctx;
    ctx.beginPath();
    for (let i = 0; i <= 56; i++) {
      const angle = (i / 56) * Math.PI * 2;
      const p = P(x + Math.cos(angle) * radius, y + Math.sin(angle) * radius, z);
      if (i === 0) {
        ctx.moveTo(p.x, p.y);
      } else {
        ctx.lineTo(p.x, p.y);
      }
    }
  }

  span(a, b) {
    return Math.hypot(a.x - b.x, a.y - b.y);
  }

  curve(a, b, lift, lateral) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const length = Math.hypot(dx, dy) || 1;
    const nx = -dy / length;
    const ny = dx / length;
    return (t) => {
      const arc = Math.sin(Math.PI * t);
      return { x: a.x + dx * t + nx * lateral * arc, y: a.y + dy * t + ny * lateral * arc, z: arc * lift };
    };
  }

  trace(P, at, from, to) {
    const ctx = this.ctx;
    const steps = Math.max(2, Math.ceil(Math.abs(to - from) * 44));
    ctx.beginPath();
    for (let i = 0; i <= steps; i++) {
      const point = at(from + ((to - from) * i) / steps);
      const p = P(point.x, point.y, point.z);
      if (i === 0) {
        ctx.moveTo(p.x, p.y);
      } else {
        ctx.lineTo(p.x, p.y);
      }
    }
  }

  marker(P, at, t, shape, rgb, alpha, size) {
    const point = at(t);
    const p = P(point.x, point.y, point.z);
    glyph(this.ctx, shape, p.x, p.y, size, rgb, alpha);
    return p;
  }

  tangent(P, at, t) {
    const before = at(Math.max(0, t - 0.02));
    const after = at(Math.min(1, t + 0.02));
    const heading = screenHeading(P(before.x, before.y, before.z), P(after.x, after.y, after.z));
    return heading === null ? 0 : heading;
  }

  text(str, x, y, color = INK, align = "left") {
    const ctx = this.ctx;
    ctx.font = FONT;
    ctx.textAlign = align;
    ctx.lineWidth = 3;
    ctx.lineJoin = "round";
    ctx.strokeStyle = HALO;
    ctx.strokeText(str, x, y);
    ctx.fillStyle = color;
    ctx.fillText(str, x, y);
    ctx.lineWidth = 1;
    ctx.textAlign = "left";
  }

  measure(str) {
    let w = this.widths.get(str);
    if (w === undefined) {
      this.ctx.font = FONT;
      w = this.ctx.measureText(str).width;
      this.widths.set(str, w);
    }
    return w;
  }

  touchesFocus(...ids) {
    return this.view.mode !== "country" || ids.includes(this.view.focus);
  }

  drawRelations(P) {
    const ctx = this.ctx;
    for (const relation of this.snapshot.relations) {
      const hostile = relation.state === "Hostile";
      if (!hostile && relation.state !== "Allied") {
        continue;
      }
      if (!this.touchesFocus(relation.first, relation.second)) {
        continue;
      }
      const a = this.countryIndex.get(relation.first);
      const b = this.countryIndex.get(relation.second);
      if (!a || !b) {
        continue;
      }
      this.present.add(hostile ? "line:hostile" : "line:allied");
      this.trace(P, this.curve(a, b, 0, hostile ? this.span(a, b) * 0.1 : 0), 0, 1);
      ctx.lineWidth = 1;
      if (hostile) {
        ctx.setLineDash(DASH.Hostile);
        ctx.strokeStyle = rgba(RED_RGB, 0.3);
      } else {
        ctx.setLineDash([]);
        ctx.strokeStyle = rgba(INK_RGB, 0.1);
      }
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  drawInteractions(P, now) {
    const snapshot = this.snapshot;
    const fraction = Math.min(1, (now - this.lastSnapshotAt) / this.interval);
    const tickNow = snapshot.tick + fraction;
    for (const item of snapshot.interactions) {
      if (!this.touchesFocus(item.source, item.target)) {
        continue;
      }
      const a = this.countryIndex.get(item.source);
      const b = this.countryIndex.get(item.target);
      if (!a || !b) {
        continue;
      }
      const finished = item.endTick != null;
      const span = Math.max(1, item.dueTick - item.startTick);
      const reference = finished ? item.endTick : tickNow;
      const progress = Math.min(1, Math.max(0, (reference - item.startTick) / span));
      const age = finished ? Math.max(0, tickNow - item.endTick) : 0;
      const fade = finished ? Math.max(0, 1 - age / FADE_TICKS) : 1;
      if (fade <= 0) {
        continue;
      }
      const view = { a, b, item, progress, finished, age, fade, detailed: this.view.mode === "country" };
      switch (item.kind) {
        case "Negotiation":
          this.drawNegotiation(P, view);
          break;
        case "SupplyShipment":
          this.drawShipment(P, view);
          break;
        case "Clash":
          this.drawClash(P, view);
          break;
        case "MissileStrike":
          this.drawMissile(P, view);
          break;
        default:
          break;
      }
    }
    this.ctx.setLineDash([]);
    this.ctx.lineWidth = 1;
  }

  drawNegotiation(P, view) {
    const ctx = this.ctx;
    const { a, b, item, progress, finished, fade } = view;
    const at = this.curve(a, b, 0, 6 + this.span(a, b) * 0.05);
    const concluded = item.status === "Succeeded";
    const failed = item.status === "Cancelled";
    ctx.lineWidth = 1;
    ctx.setLineDash(concluded ? [] : DASH.None);
    this.trace(P, at, 0, finished ? progress : 1);
    ctx.strokeStyle = rgba(failed ? RED_RGB : INK_RGB, (concluded ? 0.85 : 0.5) * fade);
    ctx.stroke();
    ctx.setLineDash([]);
    if (!finished) {
      this.present.add("line:negotiation");
      this.marker(P, at, progress, "hollow", INK_RGB, 1, 4);
    } else if (concluded) {
      this.present.add("line:concluded");
      this.marker(P, at, 0.5, "diamond", INK_RGB, fade, 4);
    } else {
      this.present.add("line:lost");
      this.marker(P, at, progress, "cross", RED_RGB, fade, 5);
    }
  }

  drawShipment(P, view) {
    const ctx = this.ctx;
    const { a, b, item, progress, finished, fade, detailed } = view;
    const distance = this.span(a, b);
    const air = item.mode === "Air";
    const naval = item.mode === "Naval";
    const at = this.curve(a, b, air ? 4 + distance * 0.1 : 0, naval ? distance * 0.05 : 0);
    const lost = item.status === "Intercepted";
    const rgb = lost ? RED_RGB : INK_RGB;
    if (!finished && progress < 1) {
      ctx.setLineDash([]);
      ctx.lineWidth = 1;
      this.trace(P, at, progress, 1);
      ctx.strokeStyle = rgba(INK_RGB, 0.14);
      ctx.stroke();
    }
    ctx.setLineDash(DASH[item.mode] || []);
    ctx.lineWidth = 1.3;
    this.trace(P, at, 0, progress);
    ctx.strokeStyle = rgba(rgb, 0.8 * fade);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    this.present.add(MODE_LINE[item.mode] || "line:supply-ground");
    if (lost && finished) {
      this.present.add("line:lost");
      this.marker(P, at, progress, "cross", RED_RGB, fade, 5);
      return;
    }
    const point = at(progress);
    const p = P(point.x, point.y, point.z);
    const carrier = carrierOf(item);
    const r = detailed ? COUNTRY_SYMBOL - 1 : WORLD_SYMBOL;
    const hovered = this.hover && this.hover.type === "shipment" && this.hover.id === item.id;
    ctx.globalAlpha = fade;
    if (carrier) {
      backing(ctx, p.x, p.y, r);
      ctx.strokeStyle = INK;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, carrier, p.x, p.y, r, this.tangent(P, at, progress));
      ctx.lineWidth = 1;
      this.present.add(`carrier:${symbolName(carrier)}`);
    } else {
      glyph(ctx, "dot", p.x, p.y, 3, INK_RGB, 1);
    }
    if (hovered) {
      hoverRing(ctx, p.x, p.y, r);
    }
    ctx.globalAlpha = 1;
    if (detailed || hovered) {
      this.text(String(Math.round(item.cargo)), p.x + r + 5, p.y - 5, rgba(rgb, fade));
    }
    this.nextSlots.push({ type: "shipment", id: item.id, x: p.x, y: p.y, r, carrier });
  }

  drawClash(P, view) {
    const ctx = this.ctx;
    const { a, b, finished, fade } = view;
    const at = this.curve(a, b, 0, 0);
    this.present.add("line:clash");
    ctx.setLineDash(DASH.Clash);
    ctx.lineWidth = 1.2;
    ctx.strokeStyle = rgba(RED_RGB, 0.8 * fade);
    this.trace(P, at, 0, 0.5);
    ctx.stroke();
    this.trace(P, at, 1, 0.5);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    const point = at(0.5);
    const p = P(point.x, point.y, 0);
    if (finished) {
      glyph(ctx, "cross", p.x, p.y, 6, RED_RGB, fade);
      return;
    }
    glyph(ctx, "dot", p.x, p.y, 1.8, RED_RGB, 1);
    this.animate((now) => {
      const wobble = this.reduced ? 0 : Math.sin(now / 160) * 2;
      glyph(this.ctx, "bracket", p.x, p.y, 9 + wobble, RED_RGB, 1);
    });
  }

  drawMissile(P, view) {
    const ctx = this.ctx;
    const { a, b, item, progress, finished, age, fade } = view;
    const at = this.curve(a, b, 10 + this.span(a, b) * 0.18, 0);
    const hit = item.status === "Succeeded";
    this.present.add("line:missile");
    if (!finished) {
      ctx.setLineDash(DASH.Missile);
      ctx.lineWidth = 1;
      this.trace(P, at, progress, 1);
      ctx.strokeStyle = rgba(RED_RGB, 0.26);
      ctx.stroke();
      ctx.setLineDash([]);
      if (progress > 0.55) {
        this.animate((now) => {
          const warn = this.phase(now, 700);
          this.ring(P, b.x, b.y, this.px(8 + warn * 22), 0);
          this.ctx.strokeStyle = rgba(RED_RGB, 0.5 * (1 - warn));
          this.ctx.stroke();
        });
      }
    }
    ctx.lineWidth = 1.5;
    this.trace(P, at, 0, progress);
    ctx.strokeStyle = rgba(RED_RGB, 0.9 * fade);
    ctx.stroke();
    ctx.lineWidth = 1;
    if (!finished) {
      const point = at(progress);
      const p = P(point.x, point.y, point.z);
      ctx.strokeStyle = RED;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, "Missile", p.x, p.y, WORLD_SYMBOL, this.tangent(P, at, progress));
      ctx.lineWidth = 1;
      this.present.add("marker:missile");
    } else if (hit) {
      this.ring(P, b.x, b.y, this.px(8 + age * 7), 0);
      ctx.strokeStyle = rgba(RED_RGB, 0.9 * fade);
      ctx.lineWidth = 1.5;
      ctx.stroke();
      ctx.lineWidth = 1;
      const p = P(b.x, b.y, 0);
      glyph(ctx, "bracket", p.x, p.y, 10, RED_RGB, fade);
    } else {
      this.marker(P, at, progress, "cross", RED_RGB, fade, 6);
    }
  }

  moveUnits(now) {
    for (const entry of this.units.values()) {
      const k = Math.min(1, (now - entry.start) / this.interval);
      entry.x = entry.fromX + (entry.toX - entry.fromX) * k;
      entry.y = entry.fromY + (entry.toY - entry.fromY) * k;
    }
  }

  collectUnits(P) {
    const pr = this.projection;
    const focus = this.interiorFocus;
    const interiorOn = this.interiorAlpha > 0.01 && Boolean(focus);
    const rings = interiorOn ? this.rings(focus) : null;
    const items = [];
    for (const entry of this.units.values()) {
      const unit = entry.data;
      const external = this.external.has(unit.id);
      const own = unit.owner === focus;
      const inside = interiorOn && rings ? pointInRings(rings, entry.x, entry.y) : false;
      let alpha;
      if (external) {
        alpha = this.view.mode === "country" && unit.owner !== this.view.focus && !inside ? DIM : 1;
      } else if (interiorOn && (own || inside)) {
        alpha = this.interiorAlpha;
      } else {
        continue;
      }
      const destroyed = unit.status === "Destroyed";
      const p = P(entry.x, entry.y, 0);
      const altitude = unit.kind === "Air" && !destroyed ? 14 / pr.zoom : 0;
      const s = altitude > 0 ? P(entry.x, entry.y, altitude) : p;
      items.push({ id: unit.id, x: s.x, y: s.y, entry, unit, p, s, alpha, destroyed, foreign: interiorOn && !own && inside });
    }
    return items;
  }

  drawUnitLines(P, item) {
    const ctx = this.ctx;
    const { entry, unit, p, s } = item;
    const selected = this.isSelected("unit", unit.id);
    ctx.globalAlpha = item.alpha;
    if (entry.trail.length > 0) {
      ctx.beginPath();
      entry.trail.forEach((point, i) => {
        const q = P(point.x, point.y, 0);
        if (i === 0) {
          ctx.moveTo(q.x, q.y);
        } else {
          ctx.lineTo(q.x, q.y);
        }
      });
      ctx.lineTo(p.x, p.y);
      ctx.strokeStyle = rgba(INK_RGB, selected ? 0.6 : 0.25);
      ctx.lineWidth = 1;
      ctx.stroke();
    }
    if (unit.destinationX != null && unit.destinationY != null) {
      const d = P(unit.destinationX, unit.destinationY, 0);
      ctx.setLineDash(DASH.Destination);
      ctx.beginPath();
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(d.x, d.y);
      ctx.strokeStyle = rgba(MID_RGB, selected ? 0.9 : 0.6);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.beginPath();
      ctx.moveTo(d.x - 4, d.y);
      ctx.lineTo(d.x + 4, d.y);
      ctx.moveTo(d.x, d.y - 4);
      ctx.lineTo(d.x, d.y + 4);
      ctx.stroke();
    }
    if (s !== p) {
      ctx.beginPath();
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(s.x, s.y);
      ctx.strokeStyle = rgba(INK_RGB, 0.25);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  drawUnitSymbol(P, item, x, y, r, register) {
    const ctx = this.ctx;
    const { entry, unit } = item;
    const selected = this.isSelected("unit", unit.id);
    const hovered = this.hover && this.hover.type === "unit" && this.hover.id === unit.id;
    ctx.globalAlpha = item.alpha;
    backing(ctx, x, y, r);
    if (item.destroyed) {
      ctx.setLineDash(DASH.Broken);
      ctx.strokeStyle = rgba(MID_RGB, 0.8);
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, unit.kind, x, y, r);
      ctx.setLineDash([]);
      glyph(ctx, "cross", x, y, r * 0.85, RED_RGB, 0.9);
      this.present.add("marker:destroyed");
      ctx.globalAlpha = 1;
      return;
    }
    const heading = unitHeading(P, entry.x, entry.y, unit, entry.facing);
    ctx.strokeStyle = INK;
    ctx.lineWidth = 1.5;
    drawSymbol(ctx, unit.kind, x, y, r, heading === null ? 0 : heading);
    ctx.lineWidth = 1;
    if (unit.status === "Moving" && heading !== null) {
      headingTick(ctx, x, y, r, heading);
      this.present.add("marker:moving");
    }
    const ratio = Math.min(1, unit.strength / (entry.max || 1));
    strengthBar(ctx, x, y + r + 3, r, ratio);
    this.present.add("marker:strength");
    if (ratio < 0.3) {
      this.present.add("marker:low");
    }
    if (item.foreign) {
      foreignRing(ctx, x, y, r);
      this.text(unit.owner, x, y - r - 9, MID, "center");
      this.present.add("marker:foreign");
    }
    if (hovered) {
      hoverRing(ctx, x, y, r);
    }
    if (selected || hovered) {
      this.text(`${unit.owner}-${unit.id}`, x + r + 7, y + 4);
    }
    ctx.globalAlpha = 1;
    this.present.add(`unit:${symbolName(unit.kind)}`);
    if (register) {
      this.nextSlots.push({ type: "unit", id: unit.id, x, y, r, max: entry.max });
    }
  }

  drawUnits(P, now) {
    this.moveUnits(now);
    const ctx = this.ctx;
    const items = this.collectUnits(P);
    if (items.length === 0) {
      return;
    }
    const r = this.view.mode === "country" ? COUNTRY_SYMBOL : WORLD_SYMBOL;
    const live = items.filter((item) => !item.destroyed);
    const dead = items.filter((item) => item.destroyed);
    const byId = new Map(live.map((item) => [item.id, item]));
    const layout = layoutUnits(
      live.map((item) => ({ id: item.id, x: item.x, y: item.y })),
      this.expanded
    );
    for (const item of dead) {
      this.drawUnitSymbol(P, item, item.x, item.y, r, false);
    }
    for (const point of layout.singles) {
      this.drawUnitLines(P, byId.get(point.id));
    }
    for (const spread of layout.spreads) {
      for (const slot of spread.slots) {
        this.drawUnitLines(P, byId.get(slot.member.id));
      }
    }
    ctx.lineWidth = 1;
    for (const spread of layout.spreads) {
      const { cluster, slots } = spread;
      ctx.strokeStyle = rgba(INK_RGB, 0.55);
      ctx.setLineDash([]);
      for (const slot of slots) {
        const item = byId.get(slot.member.id);
        ctx.globalAlpha = item.alpha;
        ctx.beginPath();
        ctx.moveTo(item.x, item.y);
        ctx.lineTo(slot.x, slot.y);
        ctx.stroke();
        ctx.fillStyle = rgba(INK_RGB, 0.85);
        ctx.beginPath();
        ctx.arc(item.x, item.y, 2, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
      ctx.strokeStyle = rgba(INK_RGB, 0.7);
      ctx.beginPath();
      ctx.arc(cluster.x, cluster.y, 4, 0, Math.PI * 2);
      ctx.stroke();
      this.present.add("marker:cluster");
      this.nextSlots.push({ type: "cluster", id: cluster.key, ids: cluster.members.map((m) => m.id), x: cluster.x, y: cluster.y, r: 5, expanded: true });
    }
    for (const spread of layout.spreads) {
      for (const slot of spread.slots) {
        this.drawUnitSymbol(P, byId.get(slot.member.id), slot.x, slot.y, r, true);
      }
    }
    for (const point of layout.singles) {
      this.drawUnitSymbol(P, byId.get(point.id), point.x, point.y, r, true);
    }
    for (const cluster of layout.badges) {
      const hovered = this.hover && this.hover.type === "cluster" && this.hover.id === cluster.key;
      ctx.globalAlpha = Math.max(...cluster.members.map((m) => byId.get(m.id).alpha));
      clusterBadge(ctx, cluster.x, cluster.y, cluster.members.length);
      if (hovered) {
        hoverRing(ctx, cluster.x, cluster.y, 10);
      }
      ctx.globalAlpha = 1;
      this.present.add("marker:cluster");
      this.nextSlots.push({ type: "cluster", id: cluster.key, ids: cluster.members.map((m) => m.id), x: cluster.x, y: cluster.y, r: 10, expanded: false });
    }
  }

  drawSensors(P) {
    const ctx = this.ctx;
    const focus = this.interiorFocus;
    ctx.globalAlpha = this.interiorAlpha;
    for (const sensor of this.snapshot.sensors) {
      if (sensor.owner !== focus) {
        continue;
      }
      const name = sensorName(sensor.kind);
      const selected = this.isSelected("sensor", sensor.id);
      const hovered = this.hover && this.hover.type === "sensor" && this.hover.id === sensor.id;
      this.ring(P, sensor.x, sensor.y, sensor.range, 0);
      ctx.fillStyle = rgba(TEAL_RGB, 0.05);
      ctx.fill();
      ctx.setLineDash(DASH.Sensor);
      ctx.strokeStyle = rgba(TEAL_RGB, selected ? 0.9 : 0.42);
      ctx.lineWidth = 1;
      ctx.stroke();
      ctx.setLineDash([]);
      const center = P(sensor.x, sensor.y, 0);
      const alpha = this.interiorAlpha;
      if (name === "Radar") {
        this.animate((now) => this.drawSweep(P, sensor, center, now, alpha));
      } else if (name === "Sonar") {
        this.animate((now) => this.drawPing(P, sensor, now, alpha));
      }
      backing(ctx, center.x, center.y, COUNTRY_SYMBOL);
      ctx.strokeStyle = INK;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, name, center.x, center.y, COUNTRY_SYMBOL);
      ctx.lineWidth = 1;
      if (hovered) {
        hoverRing(ctx, center.x, center.y, COUNTRY_SYMBOL);
      }
      if (selected || hovered) {
        this.text(`${sensor.owner} ${name} ${sensor.id}`, center.x + COUNTRY_SYMBOL + 7, center.y + 4);
      }
      this.present.add(`sensor:${name}`);
      this.nextSlots.push({ type: "sensor", id: sensor.id, x: center.x, y: center.y, r: COUNTRY_SYMBOL });
    }
    ctx.globalAlpha = 1;
  }

  drawSweep(P, sensor, center, now, alpha) {
    const ctx = this.ctx;
    const angle = this.reduced ? 0.9 : (now / 2600) * Math.PI * 2;
    ctx.globalAlpha = alpha;
    ctx.beginPath();
    ctx.moveTo(center.x, center.y);
    for (let i = 0; i <= 12; i++) {
      const a = angle - (i / 12) * 0.7;
      const q = P(sensor.x + Math.cos(a) * sensor.range, sensor.y + Math.sin(a) * sensor.range, 0);
      ctx.lineTo(q.x, q.y);
    }
    ctx.closePath();
    ctx.fillStyle = rgba(INK_RGB, 0.06);
    ctx.fill();
    const edge = P(sensor.x + Math.cos(angle) * sensor.range, sensor.y + Math.sin(angle) * sensor.range, 0);
    ctx.beginPath();
    ctx.moveTo(center.x, center.y);
    ctx.lineTo(edge.x, edge.y);
    ctx.strokeStyle = rgba(INK_RGB, 0.45);
    ctx.lineWidth = 1;
    ctx.stroke();
    ctx.globalAlpha = 1;
  }

  drawPing(P, sensor, now, alpha) {
    const ctx = this.ctx;
    const phase = this.phase(now, 3000);
    this.ring(P, sensor.x, sensor.y, sensor.range * phase, 0);
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = rgba(INK_RGB, 0.4 * (1 - phase));
    ctx.lineWidth = 1;
    ctx.stroke();
    ctx.globalAlpha = 1;
  }

  drawCapitals(P) {
    const ctx = this.ctx;
    if (!this.glow) {
      this.glow = this.makeGlow();
    }
    for (const country of this.snapshot.countries) {
      const base = P(country.x, country.y, 0);
      const selected = this.isSelected("country", country.id);
      const hovered = this.hover && this.hover.type === "country" && this.hover.id === country.id;
      const alpha = this.dimOf(country.id);
      const command = country.id === this.interiorFocus && this.interiorAlpha > 0.01;
      ctx.globalAlpha = alpha;
      if (country.isModel) {
        ctx.drawImage(this.glow, base.x - 48, base.y - 48);
        this.animate((now) => this.drawHeroAnimation(P, country, base, alpha, now));
        if (!command) {
          ctx.fillStyle = RED;
          ctx.beginPath();
          ctx.arc(base.x, base.y, 4.5, 0, Math.PI * 2);
          ctx.fill();
        }
      } else if (!command) {
        ctx.fillStyle = selected || hovered ? INK : rgba(INK_RGB, 0.8);
        ctx.beginPath();
        ctx.arc(base.x, base.y, 2.8, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = rgba(INK_RGB, selected || hovered ? 0.7 : 0.3);
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(base.x, base.y, 6, 0, Math.PI * 2);
        ctx.stroke();
      }
      if (command) {
        ctx.globalAlpha = this.interiorAlpha;
        backing(ctx, base.x, base.y, COUNTRY_SYMBOL);
        ctx.strokeStyle = country.isModel ? RED : INK;
        ctx.lineWidth = 1.5;
        drawSymbol(ctx, "Command", base.x, base.y, COUNTRY_SYMBOL);
        ctx.lineWidth = 1;
        this.present.add("installation:Command");
      }
      ctx.globalAlpha = 1;
    }
    this.present.add("marker:capital");
    if (this.modelId) {
      this.present.add("marker:hero");
    }
  }

  drawHeroAnimation(P, country, base, alpha, now) {
    const ctx = this.ctx;
    const top = P(country.x, country.y, this.px(95));
    const gradient = ctx.createLinearGradient(base.x, base.y, top.x, top.y);
    gradient.addColorStop(0, rgba(RED_RGB, 0.9));
    gradient.addColorStop(1, rgba(RED_RGB, 0));
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = gradient;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(base.x, base.y);
    ctx.lineTo(top.x, top.y);
    ctx.stroke();
    const phase = this.phase(now, 3000);
    this.ring(P, country.x, country.y, this.px(8 + phase * 34), 0);
    ctx.strokeStyle = rgba(INK_RGB, 0.5 * (1 - phase));
    ctx.stroke();
    ctx.globalAlpha = 1;
  }

  drawLabels(P, dt) {
    const ctx = this.ctx;
    ctx.font = FONT;
    const items = [];
    for (const [index, country] of this.snapshot.countries.entries()) {
      const p = P(country.x, country.y, 0);
      const focus = country.id === this.view.focus;
      const selected = this.isSelected("country", country.id);
      const roomy = this.projection.zoom > 2.2 || focus || country.isModel || selected;
      const options = roomy
        ? [{ text: country.name, w: this.measure(country.name) }, { text: country.id, w: this.measure(country.id) }]
        : [{ text: country.id, w: this.measure(country.id) }];
      items.push({
        id: country.id,
        ax: p.x,
        ay: p.y,
        h: 12,
        radius: country.isModel ? 6 : 7,
        priority: selected ? 0 : focus ? 0.1 : country.isModel ? 0.5 : 1 + index / 100,
        options
      });
    }
    const pr = this.projection;
    const placed = placeLabels(items, { x: pr.inset.left, y: pr.inset.top, w: pr.viewWidth - pr.inset.left - pr.inset.right, h: pr.viewHeight - pr.inset.top - pr.inset.bottom });
    fadeLabels(this.labelFade, new Set(placed.keys()), dt);
    for (const [id, place] of placed) {
      this.labelLast.set(id, place);
    }
    for (const [id, state] of this.labelFade) {
      const place = this.labelLast.get(id);
      if (!place) {
        continue;
      }
      const country = this.countryIndex.get(id);
      ctx.globalAlpha = state.alpha * this.dimOf(id);
      const strong = id === this.view.focus || (country && country.isModel) || this.isSelected("country", id);
      this.text(place.text, place.x, place.y + 9.5, strong ? INK : rgba(INK_RGB, 0.75));
      if (country && id === this.view.focus && this.interiorAlpha > 0.01) {
        ctx.globalAlpha = state.alpha * this.interiorAlpha;
        this.text(`Supplies ${Math.round(country.supplies)}   Ammo ${Math.round(country.ammunition)}`, place.x, place.y + 22, MID);
      }
    }
    ctx.globalAlpha = 1;
  }

  drawReticle(now) {
    if (!this.selected) {
      return;
    }
    const position = this.positionOf(this.selected);
    if (!position) {
      return;
    }
    const ctx = this.ctx;
    const pr = this.projection;
    const c = toDisplay(position.x, position.y);
    const pulse = this.reduced ? 0 : Math.sin(now / 400);
    const h = this.px(15 + pulse * 1.5);
    const arm = h * 0.5;
    ctx.strokeStyle = rgba(INK_RGB, 0.55 + 0.25 * pulse);
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
      const a = pr.project(c.x + sx * h, c.y + sy * (h - arm));
      const b = pr.project(c.x + sx * h, c.y + sy * h);
      const d = pr.project(c.x + sx * (h - arm), c.y + sy * h);
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.lineTo(d.x, d.y);
    }
    ctx.stroke();
    ctx.lineWidth = 1;
    this.present.add("marker:selection");
  }

  isSelected(type, id) {
    return this.selected !== null && this.selected.type === type && this.selected.id === id;
  }

  positionOf(selection) {
    if (selection.type === "unit") {
      const entry = this.units.get(selection.id);
      return entry ? { x: entry.x, y: entry.y } : null;
    }
    const list = selection.type === "sensor" ? this.snapshot.sensors : this.snapshot.countries;
    return list.find((item) => item.id === selection.id) || null;
  }

  hitTest(x, y) {
    if (!this.snapshot) {
      return null;
    }
    const pr = this.projection;
    let best = null;
    let bestDistance = Infinity;
    for (const slot of this.slots) {
      const distance = Math.hypot(slot.x - x, slot.y - y);
      const reach = Math.max(PICK_RADIUS - 2, slot.r + 4);
      if (distance < reach && distance < bestDistance) {
        best = slot;
        bestDistance = distance;
      }
    }
    if (best) {
      return { type: best.type, id: best.id, ids: best.ids, max: best.max, carrier: best.carrier, expanded: best.expanded };
    }
    let capital = null;
    let capitalDistance = PICK_RADIUS;
    for (const country of this.snapshot.countries) {
      const p = pr.projectWorld(country.x, country.y, 0);
      const distance = Math.hypot(p.x - x, p.y - y);
      if (distance < capitalDistance) {
        capitalDistance = distance;
        capital = { type: "country", id: country.id };
      }
    }
    if (capital) {
      return capital;
    }
    const world = pr.unprojectWorld(x, y);
    const entity = world ? entityAt(this.map, world.x, world.y) : null;
    if (!entity) {
      return null;
    }
    if (entity.id !== "" && this.countryIndex.has(entity.id)) {
      return { type: "country", id: entity.id };
    }
    return { type: "land", entity };
  }

  setHover(hover) {
    const key = hover ? `${hover.type}:${hover.id}` : "";
    this.hover = hover;
    if (key !== this.hoverKey) {
      this.hoverKey = key;
      this.onHover(hover);
    }
  }

  updateHover() {
    if (!this.pointer || (this.drag && this.moved)) {
      return;
    }
    const key = this.projection.version + this.pointer.x * 7 + this.pointer.y * 13;
    if (key === this.pointerVersion && this.frameCount % HOVER_EVERY !== 0) {
      return;
    }
    this.pointerVersion = key;
    const hit = this.hitTest(this.pointer.x, this.pointer.y);
    this.setHover(hit && hit.type !== "land" ? hit : null);
    this.canvas.style.cursor = this.hover ? "pointer" : "grab";
  }

  toggleCluster(key) {
    if (this.expanded.has(key)) {
      this.expanded.delete(key);
    } else {
      this.expanded.add(key);
    }
  }

  pick(clientX, clientY) {
    if (!this.snapshot) {
      return;
    }
    const at = this.local({ clientX, clientY });
    const hit = this.hitTest(at.x, at.y);
    if (hit && hit.type === "land") {
      this.flash = { entity: hit.entity, start: performance.now() };
      return;
    }
    if (hit && hit.type === "cluster") {
      this.toggleCluster(hit.id);
      return;
    }
    if (hit && hit.type === "shipment") {
      return;
    }
    if (!hit) {
      if (this.view.mode === "country") {
        this.exitCountry();
      } else if (this.selected) {
        this.selected = null;
        this.onSelect(null);
      }
      return;
    }
    this.touched = true;
    this.selected = { type: hit.type, id: hit.id };
    this.onSelect(this.selected);
    if (hit.type === "country" && hit.id !== this.view.focus) {
      this.enterCountry(hit.id);
    }
  }
}
