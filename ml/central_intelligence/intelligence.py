from __future__ import annotations

import math
from dataclasses import dataclass
from enum import IntEnum

from ml.agents.intelligence.tracker import Track

MAX_LABEL = 80
MAX_RADIUS = 500.0
MAX_POINTS = 200


class Priority(IntEnum):
    KNOW = 1
    MONITOR = 2
    ASSESS = 3
    IMMEDIATE = 4


@dataclass(frozen=True)
class Assessment:
    track_id: str
    priority: Priority
    reason: str


@dataclass(frozen=True)
class PointAction:
    op: str
    point_id: str
    kind: str = ""
    confidence: str = ""
    x: float = 0.0
    y: float = 0.0
    radius: float = 0.0
    label: str = ""
    path: tuple = ()


class CentralIntelligence:
    def __init__(
        self,
        immediate_range: float = 60.0,
        assess_range: float = 200.0,
        monitor_hits: int = 3,
        horizon: int = 10,
        min_closing_speed: float = 0.05,
        move_tolerance: float = 2.0,
    ):
        self.immediate_range = immediate_range
        self.assess_range = assess_range
        self.monitor_hits = monitor_hits
        self.horizon = horizon
        self.min_closing_speed = min_closing_speed
        self.move_tolerance = move_tolerance
        self.placed: dict[str, PointAction] = {}
        self.assessments: dict[str, Assessment] = {}

    def reset(self) -> None:
        self.placed.clear()
        self.assessments.clear()

    def rejected(self, point_id: str) -> None:
        self.placed.pop(point_id, None)

    def assess(self, track: Track, assets: dict, tick: int) -> Assessment:
        x, y = track.project(tick)
        nearest = math.inf
        closing = False
        for sensor in assets.get("sensors", []):
            dx = sensor["x"] - x
            dy = sensor["y"] - y
            distance = math.hypot(dx, dy)
            nearest = min(nearest, distance)
            if distance > 0 and track.speed >= self.min_closing_speed:
                if (track.vx * dx + track.vy * dy) / (track.speed * distance) > 0.3:
                    closing = True
        if track.hits >= 2 and nearest <= self.immediate_range:
            return Assessment(track.id, Priority.IMMEDIATE, "inside immediate range of an own asset")
        if track.hits >= 2 and nearest <= self.assess_range and closing:
            return Assessment(track.id, Priority.ASSESS, "closing on an own asset")
        if track.hits >= self.monitor_hits:
            return Assessment(track.id, Priority.MONITOR, "persistent contact")
        return Assessment(track.id, Priority.KNOW, "unconfirmed contact")

    def decide(self, tracks: list[Track], assets: dict, tick: int) -> list[PointAction]:
        width = float(assets.get("width", 0))
        height = float(assets.get("height", 0))
        self.assessments = {track.id: self.assess(track, assets, tick) for track in tracks}
        ranked = sorted(
            tracks,
            key=lambda t: (-int(self.assessments[t.id].priority), -t.hits, t.id),
        )
        desired: dict[str, PointAction] = {}
        for track in ranked:
            if len(desired) >= MAX_POINTS:
                break
            priority = self.assessments[track.id].priority
            if priority == Priority.KNOW:
                continue
            for action in self._points_for(track, priority, tick, width, height):
                desired[action.point_id] = action
        actions: list[PointAction] = []
        for point_id in sorted(desired):
            action = desired[point_id]
            current = self.placed.get(point_id)
            if current is None or self._differs(current, action):
                actions.append(action)
                self.placed[point_id] = action
        for point_id in sorted(set(self.placed) - set(desired)):
            actions.append(PointAction("delete", point_id))
            del self.placed[point_id]
        return actions

    def _points_for(self, track: Track, priority: Priority, tick: int, width: float, height: float):
        x, y = track.project(tick)
        label = f"{track.id} {track.kind or 'unknown'}"[:MAX_LABEL]
        radius = min(MAX_RADIUS, max(5.0, 2.0 * track.sigma))
        yield PointAction(
            "put", f"trk-{track.id}", "Tracking", track.confidence,
            _clamp(x, width), _clamp(y, height), radius, label)
        if priority >= Priority.ASSESS and track.speed >= self.min_closing_speed:
            fx, fy = track.project(tick + self.horizon)
            fx, fy = _clamp(fx, width), _clamp(fy, height)
            yield PointAction(
                "put", f"prd-{track.id}", "ExpectedActivity", "Predicted",
                fx, fy, radius, f"{track.id} expected"[:MAX_LABEL],
                (_clamp(x, width), _clamp(y, height), fx, fy))
        if priority == Priority.IMMEDIATE:
            yield PointAction(
                "put", f"tgt-{track.id}", "SuspectedTarget", track.confidence,
                _clamp(x, width), _clamp(y, height), radius, f"{track.id} immediate"[:MAX_LABEL])

    def _differs(self, current: PointAction, wanted: PointAction) -> bool:
        return (
            current.kind != wanted.kind
            or current.confidence != wanted.confidence
            or current.label != wanted.label
            or math.hypot(current.x - wanted.x, current.y - wanted.y) > self.move_tolerance
            or abs(current.radius - wanted.radius) > self.move_tolerance
            or self._path_differs(current.path, wanted.path)
        )

    def _path_differs(self, first: tuple, second: tuple) -> bool:
        if len(first) != len(second):
            return True
        return any(abs(a - b) > self.move_tolerance for a, b in zip(first, second))


def _clamp(value: float, upper: float) -> float:
    return min(max(value, 0.0), upper)
