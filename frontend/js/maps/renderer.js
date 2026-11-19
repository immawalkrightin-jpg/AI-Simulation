import { Projection } from "./projection.js";
import { buildTerrain } from "./terrain.js";
import { drawSymbol } from "../units/symbols.js";

const INK = "#ecece9";
const MID = "#9a9da1";
const RED = "#ff2b2b";
const TONES = ["#f2f2f0", "#9ea2a6", "#cfd2d4", "#7d8185"];
const HOSTILE = /hostile|tense|war|deteriorat|conflict|rival/i;
const FONT = '11px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
const TRAIL_LENGTH = 24;

export class MapRenderer {
  constructor(canvas, onSelect) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.onSelect = onSelect;
    this.projection = new Projection();
    this.snapshot = null;
    this.terrain = null;
    this.units = new Map();
    this.tones = new Map();
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
        this.projection.zoom = Math.min(4, Math.max(0.5, zoom));
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

  setSnapshot(snapshot) {
    const now = performance.now();
    if (!this.terrain) {
      this.projection.setWorld(snapshot.width, snapshot.height);
      this.terrain = buildTerrain(snapshot);
      snapshot.countries.forEach((country, index) => this.tones.set(country.id, TONES[index % TONES.length]));
    }
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
    this.drawContours(P);
    this.drawSensors(P, now);
    this.drawRelations(P, now);
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

  drawContours(P) {
    const ctx = this.ctx;
    const terrain = this.terrain;
    for (const level of terrain.levels) {
      const z = terrain.levelElevation(level.value);
      const coast = level.value === terrain.seaLevel;
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
      ctx.shadowBlur = 0;
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

  drawRelations(P, now) {
    const ctx = this.ctx;
    const byId = new Map(this.snapshot.countries.map((c) => [c.id, c]));
    this.snapshot.relations.forEach((relation, index) => {
      const a = byId.get(relation.first);
      const b = byId.get(relation.second);
      if (!a || !b) {
        return;
      }
      const hostile = HOSTILE.test(relation.state);
      const za = this.terrain.elevation(a.x, a.y);
      const zb = this.terrain.elevation(b.x, b.y);
      const lift = 60 + Math.hypot(a.x - b.x, a.y - b.y) * 0.18;
      const at = (t) =>
        P(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, za + (zb - za) * t + Math.sin(Math.PI * t) * lift);
      ctx.beginPath();
      for (let i = 0; i <= 48; i++) {
        const p = at(i / 48);
        if (i === 0) {
          ctx.moveTo(p.x, p.y);
        } else {
          ctx.lineTo(p.x, p.y);
        }
      }
      ctx.strokeStyle = hostile ? "rgba(255,43,43,0.7)" : "rgba(236,236,233,0.45)";
      ctx.lineWidth = 1;
      ctx.stroke();
      const head = at(((now / 5000) + index * 0.31) % 1);
      ctx.fillStyle = hostile ? RED : INK;
      ctx.beginPath();
      ctx.arc(head.x, head.y, 2.2, 0, Math.PI * 2);
      ctx.fill();
    });
  }

  drawUnits(P, now) {
    const ctx = this.ctx;
    ctx.font = FONT;
    for (const entry of this.units.values()) {
      const k = Math.min(1, (now - entry.start) / this.interval);
      entry.x = entry.fromX + (entry.toX - entry.fromX) * k;
      entry.y = entry.fromY + (entry.toY - entry.fromY) * k;
      const unit = entry.data;
      const selected = this.isSelected("unit", unit.id);
      const tone = this.tones.get(unit.owner) || INK;
      const ground = this.terrain.elevation(entry.x, entry.y);
      const lift = unit.kind === "Air" ? 35 : 3;
      const base = P(entry.x, entry.y, ground);
      const p = P(entry.x, entry.y, ground + lift);

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

      const r = Math.max(5, 7 * p.k * 1.2);
      ctx.strokeStyle = selected ? RED : tone;
      ctx.lineWidth = 1.5;
      drawSymbol(ctx, unit.kind, p.x, p.y, r);
      ctx.lineWidth = 1;

      const ratio = Math.min(1, unit.strength / (entry.max || 1));
      ctx.fillStyle = "rgba(236,236,233,0.2)";
      ctx.fillRect(p.x - r, p.y + r + 3, r * 2, 2);
      ctx.fillStyle = ratio < 0.3 ? RED : tone;
      ctx.fillRect(p.x - r, p.y + r + 3, r * 2 * ratio, 2);

      ctx.fillStyle = selected ? RED : MID;
      ctx.fillText(`${unit.owner}-${unit.id}`, p.x + r + 4, p.y + 3);
    }
  }

  drawCountries(P, now) {
    const ctx = this.ctx;
    ctx.font = FONT;
    for (const country of this.snapshot.countries) {
      const z = this.terrain.elevation(country.x, country.y);
      const base = P(country.x, country.y, z);
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

      const selected = this.isSelected("country", country.id);
      ctx.shadowColor = RED;
      ctx.shadowBlur = 16;
      ctx.fillStyle = RED;
      ctx.beginPath();
      ctx.arc(base.x, base.y, selected ? 6 : 4.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.shadowBlur = 0;

      ctx.fillStyle = INK;
      ctx.fillText(country.name, base.x + 12, base.y - 8);
      ctx.fillStyle = MID;
      ctx.fillText(`S ${Math.round(country.supplies)}  A ${Math.round(country.ammunition)}`, base.x + 12, base.y + 5);
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
    const size = 16 + Math.sin(now / 300) * 1.5;
    const ctx = this.ctx;
    ctx.strokeStyle = RED;
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
      ctx.moveTo(p.x + sx * size, p.y + sy * (size - 7));
      ctx.lineTo(p.x + sx * size, p.y + sy * size);
      ctx.lineTo(p.x + sx * (size - 7), p.y + sy * size);
    }
    ctx.stroke();
    ctx.lineWidth = 1;
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
