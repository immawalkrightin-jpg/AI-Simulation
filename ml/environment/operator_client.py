from __future__ import annotations

from ml.environment.client import HttpClient


class OperatorClient(HttpClient):
    def status(self) -> dict:
        return self.request("GET", "/api/simulation/status")

    def pause(self) -> dict:
        return self.request("POST", "/api/simulation/pause")

    def resume(self) -> dict:
        return self.request("POST", "/api/simulation/resume")

    def step(self, count: int = 1) -> dict:
        return self.request("POST", f"/api/simulation/step?count={int(count)}")
