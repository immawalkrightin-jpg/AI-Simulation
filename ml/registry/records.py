from __future__ import annotations

from dataclasses import dataclass

MISSING = "<missing>"

CORRECTION_CATEGORIES = frozenset({
    "target_missed",
    "wrong_target_assessment",
    "friendly_fire",
    "moral_judgement",
    "tactical_mistake",
    "supply_allocation",
    "supply_transport",
})


@dataclass(frozen=True)
class ModelVersion:
    version: str
    created_at: str
    parent: str | None
    parameters: dict
    weights_path: str | None
    weights_sha256: str | None
    notes: str


@dataclass(frozen=True)
class TrainingRun:
    run_id: str
    created_at: str
    base_version: str | None
    output_version: str
    iterations: int
    seeds: list
    parameters: dict
    data_refs: list
    correction_ids: list
    notes: str


@dataclass(frozen=True)
class Evaluation:
    eval_id: str
    created_at: str
    version: str
    run_id: str | None
    seeds: list
    metrics: dict
    notes: str


@dataclass(frozen=True)
class HumanCorrection:
    correction_id: str
    created_at: str
    operation: str
    number: int
    category: str
    description: str
    model_version: str | None


@dataclass(frozen=True)
class Comparison:
    base: str
    other: str
    parameter_changes: dict
    metric_deltas: dict
    base_only_metrics: list
    other_only_metrics: list
