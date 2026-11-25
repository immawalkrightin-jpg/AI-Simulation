// Small canvas glyphs and the line language, shared by the map renderer and the symbol manual, so the
// manual can never drift from what the map draws.

import { BACKING, FONT_BOLD, INK, INK_RGB, RED_RGB, rgba } from "../theme.js";

// Below this share of its peak strength a unit counts as low: the bar loses its fill and gets a tick.
export const LOW_STRENGTH = 0.3;

// Dash patterns, in pixels. Ground, Naval, Air and None are shipment and negotiation modes.
export const DASH = {
  Ground: [],
  Naval: [2, 5],
  Air: [12, 6],
  None: [6, 6],
  Hostile: [1, 5],
  Clash: [14, 4],
  Missile: [2, 5],
  Sensor: [4, 6],
  Destination: [3, 5],
  Broken: [3, 3],
  Foreign: [3, 3],
  Hidden: [2, 2],
  Zone: [6, 4],
  Held: [9, 3]
};

// Flat markers: diamond, hollow, cross, bracket, handoff, dot. The caller sets the dash.
export function glyph(ctx, shape, x, y, r, rgb, alpha) {
  ctx.strokeStyle = rgba(rgb, alpha);
  ctx.fillStyle = rgba(rgb, alpha);
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
    case "cross":
      ctx.moveTo(x - r, y - r);
      ctx.lineTo(x + r, y + r);
      ctx.moveTo(x + r, y - r);
      ctx.lineTo(x - r, y + r);
      ctx.stroke();
      break;
    case "handoff":
      ctx.arc(x, y, r * 0.62, 0, Math.PI * 2);
      ctx.moveTo(x - r, y);
      ctx.lineTo(x - r * 0.62, y);
      ctx.moveTo(x + r * 0.62, y);
      ctx.lineTo(x + r, y);
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

// Dark disc under a symbol so lines behind it do not cross it.
export function backing(ctx, x, y, r) {
  ctx.fillStyle = BACKING;
  ctx.beginPath();
  ctx.arc(x, y, r + 2, 0, Math.PI * 2);
  ctx.fill();
}

// Strength: an outlined bar as wide as the symbol, 4 px tall. Below LOW_STRENGTH the fill dims and a tick
// marks the low line. Never red: red is reserved for the hero and for hostile events.
export function strengthBar(ctx, x, y, r, ratio) {
  const width = r * 2;
  const low = ratio < LOW_STRENGTH;
  ctx.lineWidth = 1;
  ctx.setLineDash([]);
  ctx.strokeStyle = rgba(INK_RGB, 0.55);
  ctx.strokeRect(x - r + 0.5, y + 0.5, width, 4);
  ctx.fillStyle = rgba(INK_RGB, low ? 0.45 : 0.9);
  ctx.fillRect(x - r + 1, y + 1, Math.max(0, (width - 1) * Math.min(1, Math.max(0, ratio))), 3);
  if (low) {
    ctx.beginPath();
    ctx.moveTo(x - r + 0.5 + width * LOW_STRENGTH, y - 2);
    ctx.lineTo(x - r + 0.5 + width * LOW_STRENGTH, y + 7);
    ctx.strokeStyle = INK;
    ctx.stroke();
  }
}

// A short tick ahead of a moving symbol. `heading` is radians, 0 = up the screen.
export function headingTick(ctx, x, y, r, heading) {
  const dx = Math.sin(heading);
  const dy = -Math.cos(heading);
  ctx.beginPath();
  ctx.moveTo(x + dx * (r + 3), y + dy * (r + 3));
  ctx.lineTo(x + dx * (r + 8), y + dy * (r + 8));
  ctx.strokeStyle = INK;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.lineWidth = 1;
}

// Several symbols too close to tell apart: one disc with a count.
export function clusterBadge(ctx, x, y, count) {
  backing(ctx, x, y, 10);
  ctx.strokeStyle = INK;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([]);
  ctx.beginPath();
  ctx.arc(x, y, 10, 0, Math.PI * 2);
  ctx.stroke();
  ctx.font = FONT_BOLD;
  ctx.fillStyle = INK;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(String(count), x, y + 0.5);
  ctx.textAlign = "left";
  ctx.textBaseline = "alphabetic";
  ctx.lineWidth = 1;
}

// Dashed ring around a foreign unit that sits inside the focused country.
export function foreignRing(ctx, x, y, r) {
  ctx.setLineDash(DASH.Foreign);
  ctx.strokeStyle = rgba(INK_RGB, 0.8);
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.arc(x, y, r + 5, 0, Math.PI * 2);
  ctx.stroke();
  ctx.setLineDash([]);
}

export function hoverRing(ctx, x, y, r) {
  ctx.strokeStyle = rgba(INK_RGB, 0.75);
  ctx.lineWidth = 1;
  ctx.setLineDash([]);
  ctx.beginPath();
  ctx.arc(x, y, r + 4, 0, Math.PI * 2);
  ctx.stroke();
}

export const HERO_RGB = RED_RGB;
