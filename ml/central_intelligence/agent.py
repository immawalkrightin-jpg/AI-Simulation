from __future__ import annotations

import logging

from ml.agents.intelligence.tracker import Tracker
from ml.central_intelligence.intelligence import CentralIntelligence, PointAction
from ml.environment.client import AgentClient, ApiError

OWNED_PREFIXES = ("trk-", "prd-", "tgt-")

log = logging.getLogger("agent")


class Agent:
    def __init__(
        self,
        client: AgentClient,
        tracker: Tracker | None = None,
        intelligence: CentralIntelligence | None = None,
    ):
        self.client = client
        self.tracker = tracker or Tracker()
        self.intelligence = intelligence or CentralIntelligence()
        self.cursor = 0
        self.last_tick = 0
        self.assets: dict = {}

    def bootstrap(self) -> None:
        self.reset()
        for point in self.client.points().get("points", []):
            if point["id"].startswith(OWNED_PREFIXES):
                self._delete(point["id"])

    def reset(self) -> None:
        self.cursor = 0
        self.last_tick = 0
        self.tracker.reset()
        self.intelligence.reset()

    def step(self) -> list[PointAction]:
        self.assets = self.client.assets()
        tick = self.assets["tick"]
        page = self.client.observations(self.cursor)
        if tick < self.last_tick or page["latestSequence"] < self.cursor:
            log.warning("server restarted, resynchronising")
            self.bootstrap()
            page = self.client.observations(0)
        self.last_tick = tick
        self.cursor = page["latestSequence"]
        self.tracker.update(page["observations"], tick)
        actions = self.intelligence.decide(list(self.tracker.tracks.values()), self.assets, tick)
        self._apply(actions)
        return actions

    def _apply(self, actions: list[PointAction]) -> None:
        for action in actions:
            if action.op == "delete":
                self._delete(action.point_id)
                continue
            try:
                self.client.put_point(
                    action.point_id,
                    action.kind,
                    action.confidence,
                    action.x,
                    action.y,
                    action.radius,
                    action.label,
                    list(action.path),
                )
            except ApiError as error:
                if error.status != 400:
                    raise
                log.warning("point %s rejected: %s", action.point_id, error.body)
                self.intelligence.rejected(action.point_id)

    def _delete(self, point_id: str) -> None:
        try:
            self.client.delete_point(point_id)
        except ApiError as error:
            if error.status != 404:
                raise
