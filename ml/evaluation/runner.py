from __future__ import annotations

from ml.registry.records import Evaluation
from ml.registry.store import Registry
from ml.training.episode import run_episode


def aggregate(metric_sets: list[dict]) -> dict[str, float]:
    if not metric_sets:
        raise ValueError("no metrics to aggregate")
    keys = set(metric_sets[0])
    if any(set(metrics) != keys for metrics in metric_sets):
        raise ValueError("metric sets must share the same keys")
    return {key: sum(metrics[key] for metrics in metric_sets) / len(metric_sets) for key in sorted(keys)}


def evaluate_version(
    registry: Registry,
    version: str,
    env,
    agent,
    evaluator,
    seeds,
    run_id: str | None = None,
    notes: str = "",
) -> Evaluation:
    registry.model(version)
    seed_list = list(seeds)
    if not seed_list:
        raise ValueError("at least one seed is required")
    for seed in seed_list:
        if isinstance(seed, bool) or not isinstance(seed, int) or seed < 0:
            raise ValueError("seeds must be non-negative integers")
    summaries = [run_episode(env, agent, seed, evaluator) for seed in seed_list]
    metrics = aggregate([summary.metrics for summary in summaries])
    return registry.record_evaluation(version, seed_list, metrics, run_id=run_id, notes=notes)
