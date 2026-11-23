export const CLUSTER_PIXELS = 18;

export function clusterPoints(points, threshold = CLUSTER_PIXELS) {
  const parent = points.map((_, index) => index);
  const find = (index) => {
    let root = index;
    while (parent[root] !== root) {
      root = parent[root];
    }
    let walker = index;
    while (parent[walker] !== root) {
      const next = parent[walker];
      parent[walker] = root;
      walker = next;
    }
    return root;
  };
  for (let i = 0; i < points.length; i++) {
    for (let j = i + 1; j < points.length; j++) {
      if (Math.hypot(points[i].x - points[j].x, points[i].y - points[j].y) < threshold) {
        const a = find(i);
        const b = find(j);
        if (a !== b) {
          parent[Math.max(a, b)] = Math.min(a, b);
        }
      }
    }
  }
  const groups = new Map();
  points.forEach((point, index) => {
    const root = find(index);
    if (!groups.has(root)) {
      groups.set(root, []);
    }
    groups.get(root).push(point);
  });
  const result = [];
  for (const members of groups.values()) {
    const x = members.reduce((sum, m) => sum + m.x, 0) / members.length;
    const y = members.reduce((sum, m) => sum + m.y, 0) / members.length;
    const ordered = [...members].sort((a, b) => String(a.id).localeCompare(String(b.id), undefined, { numeric: true }));
    result.push({ key: `c${ordered[0].id}`, members: ordered, x, y });
  }
  return result.sort((a, b) => a.key.localeCompare(b.key, undefined, { numeric: true }));
}

export function spreadRadius(count) {
  return Math.max(30, 14 + count * 7);
}

export function spreadMembers(cluster, radius = spreadRadius(cluster.members.length)) {
  const count = cluster.members.length;
  return cluster.members.map((member, index) => {
    const angle = -Math.PI / 2 + (index / count) * Math.PI * 2;
    return {
      member,
      x: cluster.x + Math.cos(angle) * radius,
      y: cluster.y + Math.sin(angle) * radius
    };
  });
}

export function layoutUnits(points, expanded, threshold = CLUSTER_PIXELS) {
  const singles = [];
  const badges = [];
  const spreads = [];
  for (const cluster of clusterPoints(points, threshold)) {
    if (cluster.members.length === 1) {
      singles.push(cluster.members[0]);
    } else if (expanded.has(cluster.key)) {
      spreads.push({ cluster, slots: spreadMembers(cluster) });
    } else {
      badges.push(cluster);
    }
  }
  return { singles, badges, spreads };
}
