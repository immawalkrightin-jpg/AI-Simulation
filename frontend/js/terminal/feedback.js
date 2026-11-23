export class FeedbackLink {
  constructor(config) {
    this.config = config;
  }

  async request(path, options = {}) {
    const headers = { "X-Access-Key": this.config.groundTruthKey };
    if (options.body) {
      headers["Content-Type"] = "application/json";
    }
    const response = await fetch(this.config.apiBase + path, { ...options, headers });
    const text = await response.text();
    let payload = null;
    try {
      payload = text ? JSON.parse(text) : null;
    } catch {
      payload = null;
    }
    if (!response.ok) {
      const message = response.status === 401 ? "access key rejected" : payload?.error ?? `HTTP ${response.status}`;
      const error = new Error(message);
      error.status = response.status;
      throw error;
    }
    return payload;
  }

  categories() {
    return this.request("/api/feedback/categories");
  }

  recent(count = 10) {
    return this.request(`/api/feedback/corrections?count=${count}`);
  }

  submit(report) {
    return this.request("/api/feedback/corrections", {
      method: "POST",
      body: JSON.stringify(report)
    });
  }
}
