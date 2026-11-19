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

  async start() {
    this.handlers.onLink("connecting");
    try {
      this.handlers.onSnapshot(await this.request("/api/ground-truth/snapshot"));
      this.handlers.onPaused((await this.request("/api/simulation/status")).paused);
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
        this.handlers.onPaused((await this.request("/api/simulation/status")).paused);
      } catch {
        return;
      }
    }, 3000);
  }

  async control(action, count) {
    const query = action === "step" ? `?count=${count}` : "";
    const status = await this.request(`/api/simulation/${action}${query}`, "POST");
    this.handlers.onPaused(status.paused);
  }
}
