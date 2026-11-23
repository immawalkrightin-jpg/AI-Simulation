from __future__ import annotations

from ml.environment.client import HttpClient
from ml.evaluation.scoring import DEFAULT_MATCH_DISTANCE, StepScore, score_picture


class TruthClient(HttpClient):
    def snapshot(self) -> dict:
        return self.request("GET", "/api/ground-truth/snapshot")


class EpisodeEvaluator:
    def __init__(self, truth: TruthClient, match_distance: float = DEFAULT_MATCH_DISTANCE):
        self.truth = truth
        self.match_distance = match_distance
        self.steps: list[StepScore] = []

    def reset(self) -> None:
        self.steps.clear()

    def observe(self, points: list[dict]) -> StepScore:
        score = score_picture(self.truth.snapshot(), points, self.match_distance)
        self.steps.append(score)
        return score

    def summary(self) -> dict[str, float]:
        if not self.steps:
            raise ValueError("no steps have been evaluated")
        steps = self.steps
        located = [step.mean_position_error for step in steps if step.matched_tracks > 0]
        return {
            "steps_evaluated": float(len(steps)),
            "mean_recall": _mean([step.recall for step in steps]),
            "mean_precision": _mean([step.precision for step in steps]),
            "final_recall": steps[-1].recall,
            "final_precision": steps[-1].precision,
            "mean_position_error": _mean(located) if located else 0.0,
            "mean_false_tracks": _mean([float(step.false_tracks) for step in steps]),
            "mean_missed_units": _mean([float(step.missed_units) for step in steps]),
            "mean_friendly_tracks": _mean([float(step.friendly_tracks) for step in steps]),
            "mean_confirmed_precision": _mean([step.confirmed_precision for step in steps]),
        }


def _mean(values: list[float]) -> float:
    return sum(values) / len(values)
