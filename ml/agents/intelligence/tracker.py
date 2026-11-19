from __future__ import annotations

import math
from dataclasses import dataclass, field


def _leader(votes: dict) -> str | None:
    if not votes:
        return None
    return max(sorted(votes), key=lambda name: votes[name])


@dataclass
class Track:
    id: str
    x: float
    y: float
    sigma: float
    first_tick: int
    last_tick: int
    vx: float = 0.0
    vy: float = 0.0
    hits: int = 1
    latest_confidence: str = "Suspected"
    kind_votes: dict = field(default_factory=dict)
    owner_votes: dict = field(default_factory=dict)
    sources: set = field(default_factory=set)

    @property
    def kind(self) -> str | None:
        return _leader(self.kind_votes)

    @property
    def owner(self) -> str | None:
        return _leader(self.owner_votes)

    @property
    def speed(self) -> float:
        return math.hypot(self.vx, self.vy)

    @property
    def confidence(self) -> str:
        if self.latest_confidence == "Confirmed":
            return "Confirmed"
        if self.hits >= 2:
            return "Estimated"
        return "Suspected"

    def project(self, tick: int) -> tuple[float, float]:
        dt = max(0, tick - self.last_tick)
        return self.x + self.vx * dt, self.y + self.vy * dt


class Tracker:
    def __init__(
        self,
        gate_sigmas: float = 3.0,
        min_gate: float = 8.0,
        max_speed: float = 30.0,
        process_noise: float = 1.5,
        velocity_gain: float = 0.3,
        max_age: int = 40,
    ):
        self.gate_sigmas = gate_sigmas
        self.min_gate = min_gate
        self.max_speed = max_speed
        self.process_noise = process_noise
        self.velocity_gain = velocity_gain
        self.max_age = max_age
        self.tracks: dict[str, Track] = {}
        self._next = 1

    def reset(self) -> None:
        self.tracks.clear()
        self._next = 1

    def update(self, observations: list[dict], tick: int) -> list[str]:
        for observation in sorted(observations, key=lambda o: (o["observedTick"], o["sequence"])):
            self._absorb(observation)
        return self._prune(tick)

    def _absorb(self, observation: dict) -> None:
        match = self._nearest(observation)
        if match is None:
            self._spawn(observation)
        else:
            self._fuse(match, observation)

    def _nearest(self, observation: dict) -> Track | None:
        tick = observation["observedTick"]
        sigma = max(observation["positionSigma"], 1e-3)
        best: Track | None = None
        best_ratio = 1.0
        for track in self.tracks.values():
            dt = max(0, tick - track.last_tick)
            px, py = track.project(tick)
            allowance = self.max_speed * dt if track.hits < 2 else self.process_noise * dt * self.gate_sigmas
            gate = max(self.min_gate, self.gate_sigmas * math.hypot(track.sigma, sigma)) + allowance
            ratio = math.hypot(observation["x"] - px, observation["y"] - py) / gate
            if ratio <= best_ratio:
                best, best_ratio = track, ratio
        return best

    def _spawn(self, observation: dict) -> None:
        track = Track(
            id=f"T{self._next}",
            x=observation["x"],
            y=observation["y"],
            sigma=max(observation["positionSigma"], 1e-3),
            first_tick=observation["observedTick"],
            last_tick=observation["observedTick"],
        )
        self._next += 1
        self._record(track, observation)
        self.tracks[track.id] = track

    def _fuse(self, track: Track, observation: dict) -> None:
        tick = observation["observedTick"]
        dt = max(0, tick - track.last_tick)
        px, py = track.project(tick)
        predicted_variance = track.sigma ** 2 + (self.process_noise * dt) ** 2
        observed_variance = max(observation["positionSigma"], 1e-3) ** 2
        gain = predicted_variance / (predicted_variance + observed_variance)
        rx = observation["x"] - px
        ry = observation["y"] - py
        track.x = px + gain * rx
        track.y = py + gain * ry
        track.sigma = math.sqrt((1 - gain) * predicted_variance)
        if dt > 0:
            beta = self.velocity_gain * gain
            track.vx += beta * rx / dt
            track.vy += beta * ry / dt
            speed = track.speed
            if speed > self.max_speed:
                track.vx *= self.max_speed / speed
                track.vy *= self.max_speed / speed
        track.last_tick = max(track.last_tick, tick)
        track.hits += 1
        self._record(track, observation)

    @staticmethod
    def _record(track: Track, observation: dict) -> None:
        track.latest_confidence = observation["confidence"]
        track.sources.add(observation["source"])
        kind = observation.get("classifiedKind")
        if kind:
            track.kind_votes[kind] = track.kind_votes.get(kind, 0) + 1
        owner = observation.get("classifiedOwner")
        if owner:
            track.owner_votes[owner] = track.owner_votes.get(owner, 0) + 1

    def _prune(self, tick: int) -> list[str]:
        stale = [track_id for track_id, track in self.tracks.items() if tick - track.last_tick > self.max_age]
        for track_id in stale:
            del self.tracks[track_id]
        return stale
