const clamp = (value) => Math.min(1, Math.max(0, value));

export function routeLegs(item) {
  const legs = item && item.route && Array.isArray(item.route.legs) ? item.route.legs : null;
  return legs && legs.length > 0 ? legs : null;
}

export function progressOf(item, tick) {
  const span = Math.max(1, item.dueTick - item.startTick);
  const reference = item.endTick != null ? item.endTick : tick;
  return clamp((reference - item.startTick) / span);
}

export function legIndexAt(legs, progress) {
  const p = clamp(progress);
  let index = 0;
  while (index < legs.length - 1 && p > legs[index].end) {
    index++;
  }
  return index;
}

export function legLength(leg) {
  const points = leg.points;
  let length = 0;
  for (let i = 2; i < points.length; i += 2) {
    length += Math.hypot(points[i] - points[i - 2], points[i + 1] - points[i - 1]);
  }
  return length;
}

export function localProgress(leg, progress) {
  const span = leg.end - leg.start;
  return span <= 1e-12 ? 1 : clamp((clamp(progress) - leg.start) / span);
}

function pointAlong(points, distance) {
  const last = points.length / 2 - 1;
  let remaining = distance;
  for (let i = 1; i <= last; i++) {
    const ax = points[(i - 1) * 2];
    const ay = points[(i - 1) * 2 + 1];
    const bx = points[i * 2];
    const by = points[i * 2 + 1];
    const segment = Math.hypot(bx - ax, by - ay);
    if (remaining <= segment || i === last) {
      const t = segment <= 1e-9 ? 1 : Math.min(1, remaining / segment);
      return { x: ax + (bx - ax) * t, y: ay + (by - ay) * t };
    }
    remaining -= segment;
  }
  return { x: points[0], y: points[1] };
}

export function positionAt(legs, progress) {
  const index = legIndexAt(legs, progress);
  const leg = legs[index];
  const point = pointAlong(leg.points, localProgress(leg, progress) * legLength(leg));
  return { x: point.x, y: point.y, leg: index, carrier: leg.carrier, mode: leg.mode };
}

export function legPath(leg, from, to) {
  const total = legLength(leg);
  const start = clamp(from) * total;
  const end = clamp(to) * total;
  const path = [pointAlong(leg.points, start)];
  let walked = 0;
  for (let i = 2; i < leg.points.length; i += 2) {
    walked += Math.hypot(leg.points[i] - leg.points[i - 2], leg.points[i + 1] - leg.points[i - 1]);
    if (walked > start && walked < end) {
      path.push({ x: leg.points[i], y: leg.points[i + 1] });
    }
  }
  path.push(pointAlong(leg.points, end));
  return path;
}

export function segmentsBetween(legs, from, to) {
  const a = clamp(Math.min(from, to));
  const b = clamp(Math.max(from, to));
  const segments = [];
  legs.forEach((leg, index) => {
    const lo = Math.max(a, leg.start);
    const hi = Math.min(b, leg.end);
    if (hi <= lo) {
      return;
    }
    segments.push({
      leg: index,
      carrier: leg.carrier,
      mode: leg.mode,
      path: legPath(leg, localProgress(leg, lo), localProgress(leg, hi))
    });
  });
  return segments;
}

export function handOffs(legs) {
  return legs.slice(1).map((leg, i) => ({
    x: leg.points[0],
    y: leg.points[1],
    at: leg.start,
    from: legs[i].carrier,
    to: leg.carrier,
    mode: leg.mode
  }));
}

export function chainOf(legs) {
  const chain = [];
  for (const leg of legs) {
    if (chain[chain.length - 1] !== leg.carrier) {
      chain.push(leg.carrier);
    }
  }
  return chain;
}

export function subdivide(path, step) {
  if (path.length < 2) {
    return path;
  }
  const result = [path[0]];
  for (let i = 1; i < path.length; i++) {
    const a = path[i - 1];
    const b = path[i];
    const pieces = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.y - a.y) / step));
    for (let k = 1; k <= pieces; k++) {
      const t = k / pieces;
      result.push({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t });
    }
  }
  return result;
}
