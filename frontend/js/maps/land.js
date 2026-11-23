export function buildLand(map, P) {
  const all = new Path2D();
  const parts = [];
  for (const entity of map.entities) {
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

export function landLabels(map) {
  const labels = [];
  for (const entity of map.entities) {
    if (entity.id === "" || entity.rings.length === 0) {
      continue;
    }
    const ring = entity.rings.reduce((best, current) => (current.length > best.length ? current : best));
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (let i = 0; i < ring.length; i += 2) {
      minX = Math.min(minX, ring[i]);
      maxX = Math.max(maxX, ring[i]);
      minY = Math.min(minY, ring[i + 1]);
      maxY = Math.max(maxY, ring[i + 1]);
    }
    labels.push({ id: entity.id, x: (minX + maxX) / 2, y: (minY + maxY) / 2 });
  }
  return labels;
}
