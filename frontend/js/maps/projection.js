export class Projection {
  constructor() {
    this.worldWidth = 1000;
    this.worldHeight = 1000;
    this.viewWidth = 800;
    this.viewHeight = 600;
    this.zoom = 1;
    this.panX = 0;
    this.panY = 20;
    this.yaw = -0.42;
    this.tilt = 0.52;
    this.scale = 1;
    this.fit();
  }

  setWorld(width, height) {
    this.worldWidth = width;
    this.worldHeight = height;
    this.fit();
  }

  resize(width, height) {
    this.viewWidth = width;
    this.viewHeight = height;
    this.fit();
  }

  fit() {
    const cos = Math.abs(Math.cos(this.yaw));
    const sin = Math.abs(Math.sin(this.yaw));
    const extentX = this.worldWidth * cos + this.worldHeight * sin;
    const extentY = (this.worldWidth * sin + this.worldHeight * cos) * this.tilt;
    this.scale = Math.min(this.viewWidth / extentX, this.viewHeight / extentY) * 0.9;
  }

  project(x, y, z = 0) {
    const dx = x - this.worldWidth / 2;
    const dy = y - this.worldHeight / 2;
    const cos = Math.cos(this.yaw);
    const sin = Math.sin(this.yaw);
    const rx = dx * cos - dy * sin;
    const ry = dx * sin + dy * cos;
    const depth = 1 + ry / (this.worldHeight * 2.6);
    const k = (this.scale * this.zoom) / depth;
    return {
      x: this.viewWidth / 2 + this.panX + rx * k,
      y: this.viewHeight / 2 + this.panY + ry * k * this.tilt - z * k,
      k
    };
  }
}
