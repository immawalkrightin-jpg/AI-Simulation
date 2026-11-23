import { DISPLAY_BOUNDS, FRAMES, displayRect, fromDisplay, toDisplay } from "./geo.js";

// A real camera over a ground plane, working in display space (see geo.js).
//
// The camera orbits `center` at a tilt measured from vertical (0 = top-down). Near ground (bottom of the
// screen) is larger and far ground (top) is smaller. The camera distance scales with the viewport and with
// 1 / zoom, so the near/far scale ratio depends only on the tilt: about 1.3 at the default tilt and about
// 1.5 at the maximum tilt. With yaw = 0 north is up.
//
// Ground point (dx, dy, z), relative to the centre and rotated by yaw to (rx, ry):
//   depth = D - ry * sin(tilt) - z * cos(tilt)
//   k     = zoom * D / depth                      (pixels per display unit at that point)
//   sx    = cx + rx * k
//   sy    = cy + (ry * cos(tilt) - z * sin(tilt)) * k
// z is altitude in display units and is used only for real altitude (aircraft, missile arcs, the hero beam).

export const DEFAULT_TILT = (47 * Math.PI) / 180;
export const MAX_TILT = (60 * Math.PI) / 180;
export const FLAT_TILT = 0;
export const MAX_ZOOM = 14;
const PERSPECTIVE = 4.4; // camera distance as a multiple of viewHeight / zoom
const PAN_MARGIN = 150;

const easeInOut = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);

export class Projection {
  constructor() {
    this.viewWidth = 800;
    this.viewHeight = 600;
    this.center = { x: 1000, y: 500 };
    this.zoom = 0.4;
    this.tilt = DEFAULT_TILT;
    this.yaw = 0;
    // Space taken by open panels, so nothing important is centred under one.
    this.inset = { left: 0, right: 0, top: 0, bottom: 0 };
    this.version = 0; // bumped on every camera change, for caches keyed by camera state
    this.flight = null;
    this.grabbed = null;
    this.reducedMotion = typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  // Display-space frames of reference -----------------------------------------------------------------

  resize(width, height) {
    this.viewWidth = width;
    this.viewHeight = height;
    this.clamp();
    this.touch();
  }

  setInset(inset) {
    this.inset = { left: 0, right: 0, top: 0, bottom: 0, ...inset };
    this.touch();
  }

  touch() {
    this.version++;
  }

  get distance() {
    return (PERSPECTIVE * this.viewHeight) / this.zoom;
  }

  // Screen position of the camera centre: the middle of the area left free by the panels.
  get originX() {
    return (this.inset.left + (this.viewWidth - this.inset.right)) / 2;
  }

  get originY() {
    return (this.inset.top + (this.viewHeight - this.inset.bottom)) / 2;
  }

  // Projection ------------------------------------------------------------------------------------------

  // Display coordinates to screen. Returns { x, y, k }.
  project(dx, dy, z = 0) {
    const D = this.distance;
    const cos = Math.cos(this.yaw);
    const sin = Math.sin(this.yaw);
    const px = dx - this.center.x;
    const py = dy - this.center.y;
    const rx = px * cos - py * sin;
    const ry = px * sin + py * cos;
    const ct = Math.cos(this.tilt);
    const st = Math.sin(this.tilt);
    const k = (this.zoom * D) / (D - ry * st - z * ct);
    return { x: this.originX + rx * k, y: this.originY + (ry * ct - z * st) * k, k };
  }

  // Screen to display coordinates on the ground plane (z = 0). Null above the horizon.
  unproject(sx, sy) {
    const D = this.distance;
    const X = sx - this.originX;
    const Y = sy - this.originY;
    const ct = Math.cos(this.tilt);
    const st = Math.sin(this.tilt);
    const denominator = 1 + (Y * st) / (this.zoom * D * ct);
    if (denominator <= 1e-6) {
      return null;
    }
    const ry = Y / (this.zoom * ct) / denominator;
    const k = (this.zoom * D) / (D - ry * st);
    const rx = X / k;
    const cos = Math.cos(this.yaw);
    const sin = Math.sin(this.yaw);
    return { x: this.center.x + rx * cos + ry * sin, y: this.center.y - rx * sin + ry * cos };
  }

  // Engine coordinates to screen, through the display transform.
  projectWorld(x, y, z = 0) {
    const d = toDisplay(x, y);
    return this.project(d.x, d.y, z);
  }

  // Screen to engine coordinates. Null above the horizon.
  unprojectWorld(sx, sy) {
    const d = this.unproject(sx, sy);
    return d ? fromDisplay(d.x, d.y) : null;
  }

  // Limits --------------------------------------------------------------------------------------------

  get minZoom() {
    return this.fitRect(DISPLAY_BOUNDS, 0, this.tilt).zoom * 0.85;
  }

  clamp() {
    const minZoom = this.minZoom;
    this.zoom = Math.min(MAX_ZOOM, Math.max(minZoom, this.zoom));
    this.tilt = Math.min(MAX_TILT, Math.max(0, this.tilt));
    this.center.x = Math.min(DISPLAY_BOUNDS.maxX + PAN_MARGIN, Math.max(DISPLAY_BOUNDS.minX - PAN_MARGIN, this.center.x));
    this.center.y = Math.min(DISPLAY_BOUNDS.maxY + PAN_MARGIN, Math.max(DISPLAY_BOUNDS.minY - PAN_MARGIN, this.center.y));
  }

  // Framing -------------------------------------------------------------------------------------------

  // Camera that fits a display rectangle into the area left free by the panels.
  fitRect(rect, padding = 0.08, tilt = this.tilt) {
    const width = Math.max(1, rect.maxX - rect.minX);
    const height = Math.max(1, rect.maxY - rect.minY);
    const freeWidth = Math.max(80, this.viewWidth - this.inset.left - this.inset.right);
    const freeHeight = Math.max(80, this.viewHeight - this.inset.top - this.inset.bottom);
    const squash = Math.max(0.35, Math.cos(tilt));
    const zoom = Math.min((freeWidth * (1 - padding)) / width, (freeHeight * (1 - padding)) / (height * squash));
    return { center: { x: (rect.minX + rect.maxX) / 2, y: (rect.minY + rect.maxY) / 2 }, zoom };
  }

  // Camera target for a named framing ("world", "theatre").
  frame(name, tilt = this.tilt) {
    const f = FRAMES[name] || FRAMES.theatre;
    const rect = f.clip ? DISPLAY_BOUNDS : displayRect(f.x0, f.y0, f.x1, f.y1);
    const fit = this.fitRect(rect, f.clip ? 0.04 : 0.1, tilt);
    return { center: fit.center, zoom: fit.zoom, tilt, yaw: 0 };
  }

  // Direct control ------------------------------------------------------------------------------------

  set(target) {
    this.cancelFlight();
    this.apply(target);
  }

  apply(target) {
    if (target.center) {
      this.center = { x: target.center.x, y: target.center.y };
    }
    if (target.zoom !== undefined) {
      this.zoom = target.zoom;
    }
    if (target.tilt !== undefined) {
      this.tilt = target.tilt;
    }
    if (target.yaw !== undefined) {
      this.yaw = target.yaw;
    }
    this.clamp();
    this.touch();
  }

  // Multiplies the zoom while keeping the ground point under the screen position fixed.
  zoomAt(sx, sy, factor) {
    this.cancelFlight();
    const anchor = this.unproject(sx, sy);
    this.zoom = Math.min(MAX_ZOOM, Math.max(this.minZoom, this.zoom * factor));
    if (anchor) {
      this.pin(anchor, sx, sy);
    }
    this.clamp();
    this.touch();
  }

  // Moves the centre so that the display point sits under the screen position (a few iterations, because
  // perspective makes the relation non-linear).
  pin(point, sx, sy) {
    for (let i = 0; i < 4; i++) {
      const now = this.unproject(sx, sy);
      if (!now) {
        return;
      }
      this.center.x += point.x - now.x;
      this.center.y += point.y - now.y;
    }
  }

  beginDrag(sx, sy) {
    this.cancelFlight();
    this.grabbed = this.unproject(sx, sy);
  }

  drag(sx, sy) {
    if (!this.grabbed) {
      return;
    }
    this.pin(this.grabbed, sx, sy);
    this.clamp();
    this.touch();
  }

  endDrag() {
    this.grabbed = null;
  }

  // Animated moves ------------------------------------------------------------------------------------

  // target: { center, zoom, tilt, yaw } (any subset). Interrupted by any user input (cancelFlight).
  flyTo(target, ms = 750) {
    const to = {
      center: target.center ? { ...target.center } : { ...this.center },
      zoom: target.zoom ?? this.zoom,
      tilt: target.tilt ?? this.tilt,
      yaw: target.yaw ?? this.yaw
    };
    if (this.reducedMotion || ms <= 0) {
      this.set(to);
      return;
    }
    this.flight = {
      from: { center: { ...this.center }, zoom: this.zoom, tilt: this.tilt, yaw: this.yaw },
      to,
      start: performance.now(),
      ms
    };
  }

  cancelFlight() {
    this.flight = null;
  }

  get flying() {
    return this.flight !== null;
  }

  // Advances an animated move. Call once per frame.
  update(now) {
    const f = this.flight;
    if (!f) {
      return;
    }
    const t = Math.min(1, Math.max(0, (now - f.start) / f.ms));
    const e = easeInOut(t);
    this.center = {
      x: f.from.center.x + (f.to.center.x - f.from.center.x) * e,
      y: f.from.center.y + (f.to.center.y - f.from.center.y) * e
    };
    this.zoom = Math.exp(Math.log(f.from.zoom) + (Math.log(f.to.zoom) - Math.log(f.from.zoom)) * e);
    this.tilt = f.from.tilt + (f.to.tilt - f.from.tilt) * e;
    this.yaw = f.from.yaw + (f.to.yaw - f.from.yaw) * e;
    this.clamp();
    this.touch();
    if (t >= 1) {
      this.flight = null;
    }
  }
}
