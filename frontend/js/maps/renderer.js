import { Projection } from "./projection.js";
import { buildTerrain } from "./terrain.js";
import { drawSymbol } from "../units/symbols.js";

const INK = "#ecece9";
const MID = "#9a9da1";
const RED = "#ff2b2b";
const WHITE_RGB = "236,236,233";
const RED_RGB = "255,43,43";
const BLOC_TONE = { East: "#ff6a63", West: "#f2f2f0", NonAligned: "#9a9da1" };
const BLOC_FILL = {
  East: "rgba(255,43,43,0.11)",
  West: "rgba(236,236,233,0.075)",
  NonAligned: "rgba(154,157,161,0.08)",
  none: "rgba(154,157,161,0.03)"
};
const BLOC_BORDER = {
  East: "rgba(255,106,99,0.55)",
  West: "rgba(236,236,233,0.5)",
  NonAligned: "rgba(154,157,161,0.5)",
  none: "rgba(154,157,161,0.16)"
};
const MODE_DASH = { Ground: [], Naval: [2, 5], Air: [12, 6], None: [6, 6] };
const FONT = '11px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
const TRAIL_LENGTH = 24;
const FADE_TICKS = 8;
const DETAIL_ZOOM = 1.8;
const MIN_ZOOM = 0.6;
const MAX_ZOOM = 8;

const tint = (rgb, alpha) => `rgba(${rgb},${alpha})`;

export class MapRenderer {
  constructor(canvas, onSelect) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.onSelect = onSelect;
    this.projection = new Projection();
    this.snapshot = null;
    this.terrain = null;
    this.map = null;
    this.countryIndex = new Map();
    this.units = new Map();
    this.selected = null;
    this.interval = 1000;
    this.lastSnapshotAt = 0;
    this.drag = null;
    this.moved = false;
    this.bind();
    this.resize();
    requestAnimationFrame((now) => this.frame(now));
  }

  bind() {
    window.addEventListener("resize", () => this.resize());
    this.canvas.addEventListener("pointerdown", (event) => {
      this.canvas.setPointerCapture(event.pointerId);
      this.drag = { x: event.clientX, y: event.clientY, panX: this.projection.panX, panY: this.projection.panY };
      this.moved = false;
    });
    this.canvas.addEventListener("pointermove", (event) => {
      if (!this.drag) {
        return;
      }
      const dx = event.clientX - this.drag.x;
      const dy = event.clientY - this.drag.y;
      if (Math.abs(dx) + Math.abs(dy) > 4) {
        this.moved = true;
      }
      this.projection.panX = this.drag.panX + dx;
      this.projection.panY = this.drag.panY + dy;
    });
    this.canvas.addEventListener("pointerup", (event) => {
      this.drag = null;
      if (!this.moved) {
        this.pick(event.clientX, event.clientY);
      }
    });
    this.canvas.addEventListener(
      "wheel",
      (event) => {
        event.preventDefault();
        const zoom = this.projection.zoom * Math.exp(-event.deltaY * 0.001);
        this.projection.zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
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
  }

  setMap(map) {
    this.map = map && Array.isArray(map.entities) ? map : null;
  }

  setSnapshot(snapshot) {
    const now = performance.now();
    if (!this.terrain) {
      this.projection.setWorld(snapshot.width, snapshot.height);
      this.terrain = buildTerrain(snapshot);
    }
    this.countryIndex = new Map(snapshot.countries.map((country) => [country.id, country]));
    if (this.lastSnapshotAt > 0) {
      this.interval = Math.min(5000, Math.max(100, now - this.lastSnapshotAt));
    }
    this.lastSnapshotAt = now;

    const present = new Set();
    for (const unit of snapshot.units) {
      present.add(unit.id);
      let entry = this.units.get(unit.id);
      if (!entry) {
        entry = { x: unit.x, y: unit.y, trail: [], max: unit.strength };
        this.units.set(unit.id, entry);
      }
      if (entry.toX !== undefined && (entry.toX !== unit.x || entry.toY !== unit.y)) {
        entry.trail.push({ x: entry.toX, y: entry.toY });
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
  }

  toneOf(owner) {
    const country = this.countryIndex.get(owner);
    return (country && BLOC_TONE[country.bloc]) || INK;
  }

  frame(now) {
    requestAnimationFrame((next) => this.frame(next));
    const ctx = this.ctx;
    const pr = this.projection;
    ctx.clearRect(0, 0, pr.viewWidth, pr.viewHeight);
    if (!this.snapshot) {
      return;
    }
    const P = (x, y, z = 0) => pr.project(x, y, z);
    this.drawGrid(P);
    const land = this.map ? this.buildLand(P) : null;
    if (land) {
      this.drawLand(land);
    }
    this.drawContours(P, land);
    if (land) {
      this.drawCoast(land);
    }
    this.drawSensors(P, now);
    this.drawRelations(P);
    this.drawInteractions(P, now);
    this.drawUnits(P, now);
    this.drawCountries(P, now);
    this.drawReticle(P, now);
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

  buildLand(P) {
    const all = new Path2D();
    const parts = [];
    for (const entity of this.map.entities) {
      if (entity.name === "ATA") {
        continue;
      }
      const path = new Path2D();
      for (const ring of entity.rings) {
        for (let i = 0; i < ring.length; i += 2) {
          const p = P(ring[i], ring[i + 1], 0);
          if (i === 0) {
            path.moveTo(p.x, p.y);
          } else {
            path.lineTo(p.x, p.y);
          }
        }
        path.closePath();
      }
      all.addPath(path);
      parts.push({ entity, path });
    }
    return { all, parts };
  }

  drawLand(land) {
    const ctx = this.ctx;
    for (const { entity, path } of land.parts) {
      ctx.fillStyle = BLOC_FILL[entity.bloc] || BLOC_FILL.none;
      ctx.fill(path);
    }
    ctx.lineWidth = 1;
    for (const { entity, path } of land.parts) {
      const selected = entity.id !== "" && this.isSelected("country", entity.id);
      ctx.strokeStyle = selected ? RED : BLOC_BORDER[entity.bloc] || BLOC_BORDER.none;
      ctx.lineWidth = selected ? 2 : 1;
      ctx.stroke(path);
    }
    ctx.lineWidth = 1;
  }

  drawCoast(land) {
    const ctx = this.ctx;
    ctx.save();
    ctx.shadowColor = "rgba(255,255,255,0.8)";
    ctx.shadowBlur = 12;
    ctx.strokeStyle = "rgba(236,236,233,0.9)";
    ctx.lineWidth = 1.4;
    ctx.stroke(land.all);
    ctx.restore();
  }

  drawGrid(P) {
    const ctx = this.ctx;
    const { width, height } = this.snapshot;
    ctx.lineWidth = 1;
    ctx.strokeStyle = "rgba(154,157,161,0.12)";
    ctx.beginPath();
    for (let v = 0; v <= width; v += 100) {
      const a = P(v, 0);
      const b = P(v, height);
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
    }
    for (let v = 0; v <= height; v += 100) {
      const a = P(0, v);
      const b = P(width, v);
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
    }
    ctx.stroke();
    ctx.strokeStyle = "rgba(154,157,161,0.38)";
    ctx.beginPath();
    const corners = [P(0, 0), P(width, 0), P(width, height), P(0, height)];
    corners.forEach((p, i) => (i === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y)));
    ctx.closePath();
    ctx.stroke();
  }

  drawContours(P, land) {
    const ctx = this.ctx;
    const terrain = this.terrain;
    for (const level of terrain.levels) {
      const z = terrain.levelElevation(level.value);
      const coast = !land && level.value === terrain.seaLevel;
      ctx.save();
      if (land) {
        ctx.clip(land.all);
      }
      ctx.beginPath();
      const s = level.segments;
      for (let i = 0; i < s.length; i += 4) {
        const a = P(s[i], s[i + 1], z);
        const b = P(s[i + 2], s[i + 3], z);
        ctx.moveTo(a.x, a.y);
        ctx.lineTo(b.x, b.y);
      }
      if (coast) {
        ctx.shadowColor = "rgba(255,255,255,0.85)";
        ctx.shadowBlur = 14;
        ctx.strokeStyle = INK;
        ctx.lineWidth = 2;
      } else {
        const alpha = level.value < terrain.seaLevel ? 0.14 : 0.2 + (level.value - terrain.seaLevel) * 0.5;
        ctx.strokeStyle = `rgba(154,157,161,${alpha})`;
        ctx.lineWidth = 1;
      }
      ctx.stroke();
      ctx.restore();
    }
  }

  drawSensors(P, now) {
    const ctx = this.ctx;
    for (const sensor of this.snapshot.sensors) {
      const z = this.terrain.elevation(sensor.x, sensor.y);
      const kind = sensor.kind.toLowerCase();
      const selected = this.isSelected("sensor", sensor.id);
      this.ring(P, sensor.x, sensor.y, sensor.range, z);
      ctx.fillStyle = "rgba(236,236,233,0.035)";
      ctx.fill();
      ctx.setLineDash([4, 6]);
      ctx.strokeStyle = selected ? RED : "rgba(236,236,233,0.38)";
      ctx.lineWidth = 1;
      ctx.stroke();
      ctx.setLineDash([]);

      if (kind.includes("radar")) {
        const angle = (now / 2600) * Math.PI * 2;
        ctx.beginPath();
        const center = P(sensor.x, sensor.y, z);
        ctx.moveTo(center.x, center.y);
        for (let i = 0; i <= 12; i++) {
          const a = angle - (i / 12) * 0.7;
          const p = P(sensor.x + Math.cos(a) * sensor.range, sensor.y + Math.sin(a) * sensor.range, z);
          ctx.lineTo(p.x, p.y);
        }
        ctx.closePath();
        ctx.fillStyle = "rgba(255,43,43,0.08)";
        ctx.fill();
        const edge = P(sensor.x + Math.cos(angle) * sensor.range, sensor.y + Math.sin(angle) * sensor.range, z);
        ctx.beginPath();
        ctx.moveTo(center.x, center.y);
        ctx.lineTo(edge.x, edge.y);
        ctx.strokeStyle = "rgba(255,43,43,0.7)";
        ctx.stroke();
      } else if (kind.includes("sonar")) {
        const phase = (now / 3000) % 1;
        this.ring(P, sensor.x, sensor.y, sensor.range * phase, z);
        ctx.strokeStyle = `rgba(236,236,233,${0.5 * (1 - phase)})`;
        ctx.stroke();
      }

      const p = P(sensor.x, sensor.y, z);
      ctx.strokeStyle = selected ? RED : INK;
      ctx.beginPath();
      ctx.moveTo(p.x, p.y - 5);
      ctx.lineTo(p.x + 5, p.y);
      ctx.lineTo(p.x, p.y + 5);
      ctx.lineTo(p.x - 5, p.y);
      ctx.closePath();
      ctx.stroke();
    }
  }

  span(a, b) {
    return Math.hypot(a.x - b.x, a.y - b.y);
  }

  curve(a, b, lift, lateral) {
    const za = this.terrain.elevation(a.x, a.y);
    const zb = this.terrain.elevation(b.x, b.y);
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const length = Math.hypot(dx, dy) || 1;
    const nx = -dy / length;
    const ny = dx / length;
    return (t) => {
      const arc = Math.sin(Math.PI * t);
      return {
        x: a.x + dx * t + nx * lateral * arc,
        y: a.y + dy * t + ny * lateral * arc,
        z: za + (zb - za) * t + arc * lift
      };
    };
  }

  trace(P, at, from, to) {
    const ctx = this.ctx;
    const steps = Math.max(2, Math.ceil(Math.abs(to - from) * 44));
    ctx.beginPath();
    for (let i = 0; i <= steps; i++) {
      const t = from + ((to - from) * i) / steps;
      const point = at(t);
      const p = P(point.x, point.y, point.z);
      if (i === 0) {
        ctx.moveTo(p.x, p.y);
      } else {
        ctx.lineTo(p.x, p.y);
      }
    }
  }

  glyph(shape, x, y, r, rgb, alpha) {
    const ctx = this.ctx;
    ctx.strokeStyle = tint(rgb, alpha);
    ctx.fillStyle = tint(rgb, alpha);
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    switch (shape) {
      case "diamond":
      case "hollow":
        ctx.moveTo(x, y - r);
        ctx.lineTo(x + r, y);
        ctx.lineTo(x, y + r);
        ctx.lineTo(x - r, y);
        ctx.closePath();
        if (shape === "diamond") {
          ctx.fill();
        } else {
          ctx.stroke();
        }
        break;
      case "square":
        ctx.rect(x - r, y - r, r * 2, r * 2);
        ctx.fill();
        break;
      case "cross":
        ctx.moveTo(x - r, y - r);
        ctx.lineTo(x + r, y + r);
        ctx.moveTo(x + r, y - r);
        ctx.lineTo(x - r, y + r);
        ctx.stroke();
        break;
      case "bracket": {
        const arm = r * 0.45;
        for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
          ctx.moveTo(x + sx * r, y + sy * (r - arm));
          ctx.lineTo(x + sx * r, y + sy * r);
          ctx.lineTo(x + sx * (r - arm), y + sy * r);
        }
        ctx.stroke();
        break;
      }
      default:
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.lineWidth = 1;
  }

  marker(P, at, t, shape, rgb, alpha, size) {
    const point = at(t);
    const p = P(point.x, point.y, point.z);
    this.glyph(shape, p.x, p.y, size, rgb, alpha);
    return p;
  }

  drawRelations(P) {
    const ctx = this.ctx;
    for (const relation of this.snapshot.relations) {
      const hostile = relation.state === "Hostile";
      if (!hostile && relation.state !== "Allied") {
        continue;
      }
      const a = this.countryIndex.get(relation.first);
      const b = this.countryIndex.get(relation.second);
      if (!a || !b) {
        continue;
      }
      const at = this.curve(a, b, hostile ? 40 + this.span(a, b) * 0.12 : 0, 0);
      this.trace(P, at, 0, 1);
      ctx.lineWidth = 1;
      if (hostile) {
        ctx.setLineDash([1, 5]);
        ctx.strokeStyle = tint(RED_RGB, 0.34);
      } else {
        ctx.setLineDash([]);
        ctx.strokeStyle = tint(WHITE_RGB, 0.13);
      }
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  drawInteractions(P, now) {
    const snapshot = this.snapshot;
    const fraction = Math.min(1, (now - this.lastSnapshotAt) / this.interval);
    const tickNow = snapshot.tick + fraction;
    const detailed = this.projection.zoom >= DETAIL_ZOOM;
    for (const item of snapshot.interactions) {
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
      const view = { a, b, item, progress, finished, age, fade, now, detailed };
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
    const at = this.curve(a, b, 18 + this.span(a, b) * 0.05, 0);
    const concluded = item.status === "Succeeded";
    const failed = item.status === "Cancelled";
    ctx.lineWidth = 1;
    ctx.setLineDash(concluded ? [] : MODE_DASH.None);
    this.trace(P, at, 0, finished ? progress : 1);
    ctx.strokeStyle = tint(failed ? RED_RGB : WHITE_RGB, (concluded ? 0.85 : 0.5) * fade);
    ctx.stroke();
    ctx.setLineDash([]);
    if (!finished) {
      this.marker(P, at, progress, "hollow", WHITE_RGB, 1, 4);
    } else if (concluded) {
      this.marker(P, at, 0.5, "diamond", WHITE_RGB, fade, 4);
    } else {
      this.marker(P, at, progress, "cross", RED_RGB, fade, 5);
    }
  }

  drawShipment(P, view) {
    const ctx = this.ctx;
    const { a, b, item, progress, finished, fade, detailed } = view;
    const distance = this.span(a, b);
    const air = item.mode === "Air";
    const naval = item.mode === "Naval";
    const at = this.curve(a, b, air ? 26 + distance * 0.1 : 0, naval ? distance * 0.05 : 0);
    const lost = item.status === "Intercepted";
    const rgb = lost ? RED_RGB : WHITE_RGB;
    if (!finished && progress < 1) {
      ctx.setLineDash([]);
      ctx.lineWidth = 1;
      this.trace(P, at, progress, 1);
      ctx.strokeStyle = tint(WHITE_RGB, 0.14);
      ctx.stroke();
    }
    ctx.setLineDash(MODE_DASH[item.mode] || []);
    ctx.lineWidth = 1.3;
    this.trace(P, at, 0, progress);
    ctx.strokeStyle = tint(rgb, 0.8 * fade);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    if (lost && finished) {
      this.marker(P, at, progress, "cross", RED_RGB, fade, 5);
      return;
    }
    const p = this.marker(P, at, progress, "square", rgb, fade, 3.2);
    if (detailed) {
      ctx.font = FONT;
      ctx.fillStyle = tint(rgb, fade);
      ctx.fillText(String(Math.round(item.cargo)), p.x + 6, p.y - 5);
    }
  }

  drawClash(P, view) {
    const ctx = this.ctx;
    const { a, b, finished, fade, now } = view;
    const at = this.curve(a, b, 0, 0);
    ctx.setLineDash([14, 4]);
    ctx.lineWidth = 1.2;
    ctx.strokeStyle = tint(RED_RGB, 0.8 * fade);
    this.trace(P, at, 0, 0.5);
    ctx.stroke();
    this.trace(P, at, 1, 0.5);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    const point = at(0.5);
    const p = P(point.x, point.y, point.z);
    if (finished) {
      this.glyph("cross", p.x, p.y, 6, RED_RGB, fade);
      return;
    }
    this.glyph("bracket", p.x, p.y, 9 + Math.sin(now / 160) * 2, RED_RGB, 1);
    this.glyph("dot", p.x, p.y, 1.8, RED_RGB, 1);
  }

  drawMissile(P, view) {
    const ctx = this.ctx;
    const { a, b, item, progress, finished, age, fade, now } = view;
    const at = this.curve(a, b, 70 + this.span(a, b) * 0.4, 0);
    const hit = item.status === "Succeeded";
    const targetZ = this.terrain.elevation(b.x, b.y);
    if (!finished) {
      ctx.setLineDash([2, 5]);
      ctx.lineWidth = 1;
      this.trace(P, at, progress, 1);
      ctx.strokeStyle = tint(RED_RGB, 0.26);
      ctx.stroke();
      ctx.setLineDash([]);
      if (progress > 0.55) {
        const warn = (now / 700) % 1;
        this.ring(P, b.x, b.y, 10 + warn * 30, targetZ);
        ctx.strokeStyle = tint(RED_RGB, 0.5 * (1 - warn));
        ctx.stroke();
      }
    }
    ctx.lineWidth = 1.5;
    this.trace(P, at, 0, progress);
    ctx.strokeStyle = tint(RED_RGB, 0.9 * fade);
    ctx.stroke();
    ctx.lineWidth = 1;
    if (!finished) {
      const point = at(progress);
      const p = P(point.x, point.y, point.z);
      ctx.save();
      ctx.shadowColor = RED;
      ctx.shadowBlur = 12;
      this.glyph("dot", p.x, p.y, 3, RED_RGB, 1);
      ctx.restore();
    } else if (hit) {
      this.ring(P, b.x, b.y, 10 + age * 9, targetZ);
      ctx.strokeStyle = tint(RED_RGB, 0.9 * fade);
      ctx.lineWidth = 1.5;
      ctx.stroke();
      ctx.lineWidth = 1;
      const p = P(b.x, b.y, targetZ);
      this.glyph("bracket", p.x, p.y, 10, RED_RGB, fade);
    } else {
      this.marker(P, at, progress, "cross", RED_RGB, fade, 6);
    }
  }

  drawUnits(P, now) {
    const ctx = this.ctx;
    ctx.font = FONT;
    const detailed = this.projection.zoom >= DETAIL_ZOOM;
    for (const entry of this.units.values()) {
      const k = Math.min(1, (now - entry.start) / this.interval);
      entry.x = entry.fromX + (entry.toX - entry.fromX) * k;
      entry.y = entry.fromY + (entry.toY - entry.fromY) * k;
      const unit = entry.data;
      const selected = this.isSelected("unit", unit.id);
      const tone = this.toneOf(unit.owner);
      const ground = this.terrain.elevation(entry.x, entry.y);
      const lift = unit.kind === "Air" ? 35 : 3;
      const base = P(entry.x, entry.y, ground);
      const p = P(entry.x, entry.y, ground + lift);
      const r = Math.max(4, 6 * p.k);

      if (unit.status === "Destroyed") {
        this.glyph("cross", base.x, base.y, r * 0.8, RED_RGB, 0.7);
        continue;
      }

      if (entry.trail.length > 0) {
        ctx.beginPath();
        entry.trail.forEach((point, i) => {
          const q = P(point.x, point.y, this.terrain.elevation(point.x, point.y));
          if (i === 0) {
            ctx.moveTo(q.x, q.y);
          } else {
            ctx.lineTo(q.x, q.y);
          }
        });
        ctx.lineTo(base.x, base.y);
        ctx.strokeStyle = selected ? "rgba(255,43,43,0.8)" : "rgba(236,236,233,0.3)";
        ctx.lineWidth = 1;
        ctx.stroke();
      }

      if (unit.destinationX != null && unit.destinationY != null) {
        const d = P(unit.destinationX, unit.destinationY, this.terrain.elevation(unit.destinationX, unit.destinationY));
        ctx.setLineDash([3, 5]);
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(d.x, d.y);
        ctx.strokeStyle = selected ? RED : "rgba(154,157,161,0.7)";
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.beginPath();
        ctx.moveTo(d.x - 4, d.y);
        ctx.lineTo(d.x + 4, d.y);
        ctx.moveTo(d.x, d.y - 4);
        ctx.lineTo(d.x, d.y + 4);
        ctx.stroke();
      }

      if (lift > 3) {
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(p.x, p.y);
        ctx.strokeStyle = "rgba(236,236,233,0.25)";
        ctx.stroke();
      }

      ctx.strokeStyle = selected ? RED : tone;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, unit.kind, p.x, p.y, r);
      ctx.lineWidth = 1;

      const ratio = Math.min(1, unit.strength / (entry.max || 1));
      ctx.fillStyle = "rgba(236,236,233,0.2)";
      ctx.fillRect(p.x - r, p.y + r + 3, r * 2, 2);
      ctx.fillStyle = ratio < 0.3 ? RED : tone;
      ctx.fillRect(p.x - r, p.y + r + 3, r * 2 * ratio, 2);

      if (selected || detailed) {
        ctx.fillStyle = selected ? RED : MID;
        ctx.fillText(`${unit.owner}-${unit.id}`, p.x + r + 4, p.y + 3);
      }
    }
  }

  drawCountries(P, now) {
    const ctx = this.ctx;
    ctx.font = FONT;
    const detailed = this.projection.zoom >= DETAIL_ZOOM;
    for (const country of this.snapshot.countries) {
      const z = this.terrain.elevation(country.x, country.y);
      const base = P(country.x, country.y, z);
      const selected = this.isSelected("country", country.id);
      const tone = BLOC_TONE[country.bloc] || INK;

      if (country.isModel) {
        const top = P(country.x, country.y, z + 150);
        const gradient = ctx.createLinearGradient(base.x, base.y, top.x, top.y);
        gradient.addColorStop(0, "rgba(255,43,43,0.9)");
        gradient.addColorStop(1, "rgba(255,43,43,0)");
        ctx.strokeStyle = gradient;
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(top.x, top.y);
        ctx.stroke();
        for (let n = 0; n < 3; n++) {
          const phase = (now / 2800 + n / 3) % 1;
          this.ring(P, country.x, country.y, 8 + phase * 70, z);
          ctx.strokeStyle = `rgba(236,236,233,${0.55 * (1 - phase)})`;
          ctx.stroke();
        }
        ctx.save();
        ctx.shadowColor = RED;
        ctx.shadowBlur = 16;
        ctx.fillStyle = RED;
        ctx.beginPath();
        ctx.arc(base.x, base.y, selected ? 6 : 4.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      } else {
        ctx.fillStyle = selected ? RED : tone;
        ctx.beginPath();
        ctx.arc(base.x, base.y, selected ? 4.5 : 3, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = selected ? RED : "rgba(236,236,233,0.35)";
        ctx.beginPath();
        ctx.arc(base.x, base.y, 6.5, 0, Math.PI * 2);
        ctx.stroke();
      }

      const showName = country.isModel || detailed || selected;
      ctx.fillStyle = INK;
      ctx.fillText(showName ? country.name : country.id, base.x + 11, base.y - 7);
      if (showName) {
        ctx.fillStyle = MID;
        ctx.fillText(`S ${Math.round(country.supplies)}  A ${Math.round(country.ammunition)}`, base.x + 11, base.y + 6);
      }
    }
  }

  drawReticle(P, now) {
    if (!this.selected) {
      return;
    }
    const position = this.positionOf(this.selected);
    if (!position) {
      return;
    }
    const z = this.terrain.elevation(position.x, position.y) + 3;
    const p = P(position.x, position.y, z);
    this.glyph("bracket", p.x, p.y, 16 + Math.sin(now / 300) * 1.5, RED_RGB, 1);
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

  pick(clientX, clientY) {
    if (!this.snapshot) {
      return;
    }
    const rect = this.canvas.getBoundingClientRect();
    const x = clientX - rect.left;
    const y = clientY - rect.top;
    const candidates = [];
    for (const entry of this.units.values()) {
      candidates.push({ type: "unit", id: entry.data.id, x: entry.x, y: entry.y });
    }
    for (const sensor of this.snapshot.sensors) {
      candidates.push({ type: "sensor", id: sensor.id, x: sensor.x, y: sensor.y });
    }
    for (const country of this.snapshot.countries) {
      candidates.push({ type: "country", id: country.id, x: country.x, y: country.y });
    }
    let best = null;
    let bestDistance = 18;
    for (const candidate of candidates) {
      const p = this.projection.project(candidate.x, candidate.y, this.terrain.elevation(candidate.x, candidate.y));
      const distance = Math.hypot(p.x - x, p.y - y);
      if (distance < bestDistance) {
        bestDistance = distance;
        best = { type: candidate.type, id: candidate.id };
      }
    }
    this.selected = best;
    this.onSelect(best);
  }
}
