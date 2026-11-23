from __future__ import annotations

import math
from dataclasses import dataclass

DEFAULT_MATCH_DISTANCE = 60.0
TRACK_KIND = "Tracking"
DESTROYED = "Destroyed"
CONFIRMED = "Confirmed"


@dataclass(frozen=True)
class StepScore:
    tick: int
    detectable: int
    tracks: int
    matched_tracks: int
    matched_detectable: int
    friendly_tracks: int
    confirmed_tracks: int
    confirmed_matched: int
    error_sum: float

    @property
    def false_tracks(self) -> int:
        return self.tracks - self.matched_tracks

    @property
    def missed_units(self) -> int:
        return self.detectable - self.matched_detectable

    @property
    def recall(self) -> float:
        return 1.0 if self.detectable == 0 else self.matched_detectable / self.detectable

    @property
    def precision(self) -> float:
        return 1.0 if self.tracks == 0 else self.matched_tracks / self.tracks

    @property
    def mean_position_error(self) -> float:
        return 0.0 if self.matched_tracks == 0 else self.error_sum / self.matched_tracks

    @property
    def confirmed_precision(self) -> float:
        return 1.0 if self.confirmed_tracks == 0 else self.confirmed_matched / self.confirmed_tracks


def score_picture(snapshot: dict, points: list[dict], match_distance: float = DEFAULT_MATCH_DISTANCE) -> StepScore:
    if match_distance <= 0:
        raise ValueError("match_distance must be positive")
    model_ids = {country["id"] for country in snapshot.get("countries", []) if country.get("isModel")}
    alive = [unit for unit in snapshot.get("units", []) if unit.get("status") != DESTROYED]
    units_by_id = {unit["id"]: unit for unit in alive}
    sensors = snapshot.get("sensors", [])
    own_sensors = [sensor for sensor in sensors if sensor.get("owner") in model_ids]
    active = own_sensors or sensors
    detectable = {
        unit["id"]
        for unit in alive
        if any(math.hypot(unit["x"] - s["x"], unit["y"] - s["y"]) <= s["range"] for s in active)
    }
    tracks = [point for point in points if point.get("kind") == TRACK_KIND]
    tracks_by_id = {track["id"]: track for track in tracks}

    pairs = []
    for track in tracks:
        for unit in alive:
            distance = math.hypot(track["x"] - unit["x"], track["y"] - unit["y"])
            if distance <= match_distance:
                pairs.append((distance, unit["id"], track["id"]))
    pairs.sort()

    used_units: set = set()
    used_tracks: set = set()
    matches = []
    for distance, unit_id, track_id in pairs:
        if unit_id in used_units or track_id in used_tracks:
            continue
        used_units.add(unit_id)
        used_tracks.add(track_id)
        matches.append((distance, unit_id, track_id))

    return StepScore(
        tick=int(snapshot.get("tick", 0)),
        detectable=len(detectable),
        tracks=len(tracks),
        matched_tracks=len(matches),
        matched_detectable=sum(1 for _, unit_id, _ in matches if unit_id in detectable),
        friendly_tracks=sum(1 for _, unit_id, _ in matches if units_by_id[unit_id].get("owner") in model_ids),
        confirmed_tracks=sum(1 for track in tracks if track.get("confidence") == CONFIRMED),
        confirmed_matched=sum(1 for _, _, track_id in matches if tracks_by_id[track_id].get("confidence") == CONFIRMED),
        error_sum=sum(distance for distance, _, _ in matches),
    )
