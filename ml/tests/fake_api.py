from __future__ import annotations

import json
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

KINDS = {
    "ExpectedActivity", "SuspectedTarget", "Surveillance", "ArtilleryEngagement",
    "SupplyRoute", "IntelligenceRequired", "Tracking",
}
CONFIDENCES = {"Confirmed", "Estimated", "Suspected", "Predicted"}
ID_PATTERN = re.compile(r"^[A-Za-z0-9_-]{1,40}$")
MAX_STEP = 1000


class FakeApi:
    def __init__(
        self,
        ai_key: str = "ai",
        width: float = 2000.0,
        height: float = 1000.0,
        operator_key: str = "gt",
        default_seed: int = 42,
    ):
        self.ai_key = ai_key
        self.operator_key = operator_key
        self.width = width
        self.height = height
        self.seed = default_seed
        self.paused = False
        self.tick = 0
        self.sensors = [{"id": 1, "kind": "Radar", "x": 100.0, "y": 100.0, "range": 400.0}]
        self.observations: list[dict] = []
        self.sequence = 0
        self.points: dict[str, dict] = {}
        self.version = 0
        self.requests: list[tuple[str, str]] = []
        self.step_hook = None
        api = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):
                return

            def _send(self, status: int, payload=None):
                body = b"" if payload is None else json.dumps(payload).encode("utf-8")
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def _authorised(self) -> bool:
                api.requests.append((self.command, self.path))
                required = api.operator_key if self.path.startswith("/api/simulation") else api.ai_key
                if self.headers.get("X-Access-Key") != required:
                    self._send(401)
                    return False
                return True

            def do_GET(self):
                if not self._authorised():
                    return
                parsed = urlparse(self.path)
                if parsed.path == "/api/ai/assets":
                    self._send(200, {
                        "tick": api.tick, "width": api.width, "height": api.height,
                        "sensors": api.sensors})
                elif parsed.path == "/api/observations":
                    after = int(parse_qs(parsed.query).get("after", ["0"])[0])
                    self._send(200, {
                        "latestSequence": api.sequence,
                        "observations": [o for o in api.observations if o["sequence"] > after]})
                elif parsed.path == "/api/ai/points":
                    self._send(200, {"version": api.version, "points": list(api.points.values())})
                elif parsed.path == "/api/simulation/status":
                    self._send(200, api.status())
                else:
                    self._send(404)

            def do_POST(self):
                if not self._authorised():
                    return
                parsed = urlparse(self.path)
                query = parse_qs(parsed.query)
                if parsed.path == "/api/simulation/pause":
                    api.paused = True
                    self._send(200, api.status())
                elif parsed.path == "/api/simulation/resume":
                    api.paused = False
                    self._send(200, api.status())
                elif parsed.path == "/api/simulation/step":
                    count = int(query.get("count", ["1"])[0])
                    if count < 1 or count > MAX_STEP:
                        self._send(400, {"error": "count out of range"})
                        return
                    api.advance(count)
                    self._send(200, api.status())
                elif parsed.path == "/api/simulation/reset":
                    seed = int(query["seed"][0]) if "seed" in query else api.seed
                    if seed < 0:
                        self._send(400, {"error": "negative seed"})
                        return
                    api.reset(seed)
                    self._send(200, api.status())
                else:
                    self._send(404)

            def do_PUT(self):
                if not self._authorised():
                    return
                point_id = self.path.rsplit("/", 1)[-1]
                length = int(self.headers.get("Content-Length", "0"))
                body = json.loads(self.rfile.read(length) or b"{}")
                error = api.validate(point_id, body)
                if error:
                    self._send(400, {"error": error})
                    return
                api.points[point_id] = {"id": point_id, **body}
                api.version += 1
                self._send(200, {"version": api.version, "points": list(api.points.values())})

            def do_DELETE(self):
                if not self._authorised():
                    return
                point_id = self.path.rsplit("/", 1)[-1]
                if point_id not in api.points:
                    self._send(404)
                    return
                del api.points[point_id]
                api.version += 1
                self._send(200, {"version": api.version, "points": list(api.points.values())})

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    @property
    def base(self) -> str:
        return f"http://127.0.0.1:{self.server.server_address[1]}"

    def status(self) -> dict:
        return {"tick": self.tick, "paused": self.paused, "seed": self.seed}

    def advance(self, count: int) -> None:
        for _ in range(count):
            self.tick += 1
            if self.step_hook is not None:
                self.step_hook(self, self.tick)

    def reset(self, seed: int) -> None:
        self.seed = seed
        self.tick = 0
        self.observations.clear()
        self.sequence = 0
        self.points.clear()
        self.version += 1

    def validate(self, point_id: str, body: dict) -> str | None:
        if not ID_PATTERN.match(point_id):
            return "bad id"
        if body.get("kind") not in KINDS or body.get("confidence") not in CONFIDENCES:
            return "bad kind or confidence"
        if not (0 <= body["x"] <= self.width and 0 <= body["y"] <= self.height):
            return "outside map"
        if not (0 <= body.get("radius", 0) <= 500):
            return "bad radius"
        if len(body.get("label", "")) > 80:
            return "label too long"
        path = body.get("path", [])
        if len(path) % 2 or len(path) > 128:
            return "bad path"
        for i in range(0, len(path), 2):
            if not (0 <= path[i] <= self.width and 0 <= path[i + 1] <= self.height):
                return "path outside map"
        return None

    def add_observation(
        self,
        x: float,
        y: float,
        tick: int,
        sigma: float = 3.0,
        confidence: str = "Estimated",
        kind: str | None = None,
        owner: str | None = None,
        source: str = "Radar",
    ) -> None:
        self.sequence += 1
        self.observations.append({
            "sequence": self.sequence, "sensorId": 1, "source": source,
            "observedTick": tick, "deliveryTick": tick, "x": x, "y": y,
            "positionSigma": sigma, "confidence": confidence,
            "classifiedKind": kind, "classifiedOwner": owner})

    def close(self) -> None:
        self.server.shutdown()
        self.server.server_close()
