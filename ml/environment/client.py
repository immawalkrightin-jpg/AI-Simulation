from __future__ import annotations

import json
import urllib.error
import urllib.request


class ApiError(Exception):
    def __init__(self, status: int, body: str):
        super().__init__(f"HTTP {status}: {body}")
        self.status = status
        self.body = body


class HttpClient:
    def __init__(self, base: str, key: str, timeout: float = 10.0):
        self.base = base.rstrip("/")
        self.key = key
        self.timeout = timeout

    def request(self, method: str, path: str, body: dict | None = None):
        data = None if body is None else json.dumps(body).encode("utf-8")
        request = urllib.request.Request(self.base + path, data=data, method=method)
        request.add_header("X-Access-Key", self.key)
        if data is not None:
            request.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                text = response.read().decode("utf-8")
        except urllib.error.HTTPError as error:
            raise ApiError(error.code, error.read().decode("utf-8", "replace")) from None
        return json.loads(text) if text else None


class AgentClient(HttpClient):
    def assets(self) -> dict:
        return self.request("GET", "/api/ai/assets")

    def observations(self, after: int = 0) -> dict:
        return self.request("GET", f"/api/observations?after={int(after)}")

    def points(self) -> dict:
        return self.request("GET", "/api/ai/points")

    def put_point(
        self,
        point_id: str,
        kind: str,
        confidence: str,
        x: float,
        y: float,
        radius: float = 0.0,
        label: str = "",
        path: list[float] | None = None,
    ) -> dict:
        body = {
            "kind": kind,
            "confidence": confidence,
            "x": x,
            "y": y,
            "radius": radius,
            "label": label,
            "path": list(path or []),
        }
        return self.request("PUT", f"/api/ai/points/{point_id}", body)

    def delete_point(self, point_id: str) -> dict:
        return self.request("DELETE", f"/api/ai/points/{point_id}")
