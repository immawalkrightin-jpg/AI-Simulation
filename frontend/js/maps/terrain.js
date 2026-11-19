const SEA_LEVEL = 0.5;
const RELIEF = 90;
const GRID = 120;
const LEVELS = [0.2, 0.32, 0.42, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0];

const CASES = [
  [], [[3, 2]], [[2, 1]], [[3, 1]], [[0, 1]], [[0, 3], [2, 1]], [[0, 2]], [[0, 3]],
  [[0, 3]], [[0, 2]], [[0, 1], [3, 2]], [[0, 1]], [[3, 1]], [[2, 1]], [[3, 2]], []
];

function hash(ix, iy, seed) {
  let h = Math.imul(ix, 374761393) ^ Math.imul(iy, 668265263) ^ Math.imul(seed + 1, 1442695041);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function smooth(t) {
  return t * t * (3 - 2 * t);
}

function valueNoise(x, y, seed) {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = smooth(x - ix);
  const fy = smooth(y - iy);
  const a = hash(ix, iy, seed);
  const b = hash(ix + 1, iy, seed);
  const c = hash(ix, iy + 1, seed);
  const d = hash(ix + 1, iy + 1, seed);
  return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
}

function fractal(x, y, seed) {
  let sum = 0;
  let amplitude = 0.5;
  let frequency = 1;
  let total = 0;
  for (let octave = 0; octave < 4; octave++) {
    sum += valueNoise(x * frequency, y * frequency, seed + octave * 17) * amplitude;
    total += amplitude;
    amplitude *= 0.5;
    frequency *= 2;
  }
  return sum / total;
}

function march(field, cellWidth, cellHeight, level) {
  const out = [];
  const stride = GRID + 1;
  for (let j = 0; j < GRID; j++) {
    for (let i = 0; i < GRID; i++) {
      const tl = field[j * stride + i];
      const tr = field[j * stride + i + 1];
      const br = field[(j + 1) * stride + i + 1];
      const bl = field[(j + 1) * stride + i];
      const index = (tl >= level ? 8 : 0) | (tr >= level ? 4 : 0) | (br >= level ? 2 : 0) | (bl >= level ? 1 : 0);
      const pairs = CASES[index];
      if (pairs.length === 0) {
        continue;
      }
      const x0 = i * cellWidth;
      const y0 = j * cellHeight;
      const edge = (e) => {
        if (e === 0) {
          return [x0 + ((level - tl) / (tr - tl)) * cellWidth, y0];
        }
        if (e === 1) {
          return [x0 + cellWidth, y0 + ((level - tr) / (br - tr)) * cellHeight];
        }
        if (e === 2) {
          return [x0 + ((level - bl) / (br - bl)) * cellWidth, y0 + cellHeight];
        }
        return [x0, y0 + ((level - tl) / (bl - tl)) * cellHeight];
      };
      for (const [from, to] of pairs) {
        const p = edge(from);
        const q = edge(to);
        out.push(p[0], p[1], q[0], q[1]);
      }
    }
  }
  return out;
}

export function buildTerrain(snapshot, seed = 7) {
  const cellWidth = snapshot.width / GRID;
  const cellHeight = snapshot.height / GRID;
  const blobs = [];
  snapshot.countries.forEach((country, index) => {
    const angle = hash(index, 1, seed) * Math.PI * 2;
    blobs.push({ x: country.x, y: country.y, spread: 125, weight: 0.55 });
    blobs.push({
      x: country.x + Math.cos(angle) * 115,
      y: country.y + Math.sin(angle) * 115,
      spread: 85,
      weight: 0.4
    });
  });

  const stride = GRID + 1;
  const field = new Float32Array(stride * stride);
  for (let j = 0; j <= GRID; j++) {
    for (let i = 0; i <= GRID; i++) {
      const x = i * cellWidth;
      const y = j * cellHeight;
      let value = 0.26 + (fractal(x * 0.006, y * 0.006, seed) - 0.5) * 0.7;
      for (const blob of blobs) {
        const dx = x - blob.x;
        const dy = y - blob.y;
        value += blob.weight * Math.exp(-(dx * dx + dy * dy) / (2 * blob.spread * blob.spread));
      }
      field[j * stride + i] = value;
    }
  }

  const heightAt = (x, y) => {
    const gx = Math.min(GRID - 0.001, Math.max(0, x / cellWidth));
    const gy = Math.min(GRID - 0.001, Math.max(0, y / cellHeight));
    const i = Math.floor(gx);
    const j = Math.floor(gy);
    const fx = gx - i;
    const fy = gy - j;
    const a = field[j * stride + i];
    const b = field[j * stride + i + 1];
    const c = field[(j + 1) * stride + i];
    const d = field[(j + 1) * stride + i + 1];
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
  };

  return {
    seaLevel: SEA_LEVEL,
    levels: LEVELS.map((value) => ({ value, segments: march(field, cellWidth, cellHeight, value) })),
    elevation: (x, y) => Math.max(0, heightAt(x, y) - SEA_LEVEL) * RELIEF,
    levelElevation: (value) => (value >= SEA_LEVEL ? (value - SEA_LEVEL) * RELIEF : (value - SEA_LEVEL) * 20)
  };
}
