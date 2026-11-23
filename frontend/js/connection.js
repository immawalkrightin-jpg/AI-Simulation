export class GroundTruthLink {
  constructor(config, handlers) {
    this.config = config;
    this.handlers = handlers;
    this.hub = null;
    this.timer = null;
    this.polling = false;
  }

  async request(path, method = "GET") {
    const response = await fetch(this.config.apiBase + path, {
      method,
      headers: { "X-Access-Key": this.config.groundTruthKey }
    });
    if (!response.ok) {
      throw new Error(String(response.status));
    }
    return response.json();
  }

  // Reports the clock state: paused flag for the buttons, the full status (speed) for the speed control.
  publish(status) {
    this.handlers.onPaused(status.paused);
    if (this.handlers.onStatus) {
      this.handlers.onStatus(status);
    }
  }

  async start() {
    this.handlers.onLink("connecting");
    try {
      this.handlers.onSnapshot(await this.request("/api/ground-truth/snapshot"));
      this.publish(await this.request("/api/simulation/status"));
      await this.openHub();
      this.handlers.onLink("live");
      this.poll();
    } catch {
      this.handlers.onLink("offline");
      this.retry();
    }
  }

  retry() {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.start(), 3000);
  }

  async openHub() {
    if (this.hub) {
      await this.hub.stop().catch(() => {});
    }
    const hub = new signalR.HubConnectionBuilder()
      .withUrl(this.config.apiBase + "/hubs/ground-truth", {
        accessTokenFactory: () => this.config.groundTruthKey
      })
      .withAutomaticReconnect()
      .build();
    hub.on("snapshot", (snapshot) => this.handlers.onSnapshot(snapshot));
    hub.onreconnecting(() => this.handlers.onLink("connecting"));
    hub.onreconnected(() => this.handlers.onLink("live"));
    hub.onclose(() => {
      this.handlers.onLink("offline");
      this.retry();
    });
    await hub.start();
    this.hub = hub;
  }

  poll() {
    if (this.polling) {
      return;
    }
    this.polling = true;
    setInterval(async () => {
      try {
        this.publish(await this.request("/api/simulation/status"));
      } catch {
        return;
      }
    }, 3000);
  }

  // Query string for a simulation action: step takes a count, speed takes a multiplier.
  static query(action, value) {
    if (action === "step") {
      return `?count=${value}`;
    }
    if (action === "speed") {
      return `?value=${value}`;
    }
    return "";
  }

  async control(action, value) {
    const status = await this.request(`/api/simulation/${action}${GroundTruthLink.query(action, value)}`, "POST");
    this.publish(status);
  }
}
