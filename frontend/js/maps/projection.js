export class Projection {
  constructor() {
    this.worldWidth = 1000;
    this.worldHeight = 1000;
    this.viewWidth = 800;
    this.viewHeight = 600;
    this.zoom = 1;
    this.panX = 0;
    this.panY = 40;
    this.yaw = -0.62;
    this.tilt = 0.5;
  }

  setWorld(width, height) {
    this.worldWidth = width;
    this.worldHeight = height;
  }

  resize(width, height) {
    this.viewWidth = width;
    this.viewHeight = height;
  }

  project(x, y, z = 0) {
    const dx = x - this.worldWidth / 2;
    const dy = y - this.worldHeight / 2;
    const cos = Math.cos(this.yaw);
    const sin = Math.sin(this.yaw);
    const rx = dx * cos - dy * sin;
    const ry = dx * sin + dy * cos;
    const depth = 1 + ry / (this.worldHeight * 2.6);
    const base = (Math.min(this.viewWidth, this.viewHeight) / this.worldWidth) * 1.15 * this.zoom;
    const k = base / depth;
    return {
      x: this.viewWidth / 2 + this.panX + rx * k,
      y: this.viewHeight / 2 + this.panY + ry * k * this.tilt - z * k,
      k
    };
  }
}
