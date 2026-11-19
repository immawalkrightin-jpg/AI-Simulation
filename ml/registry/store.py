from __future__ import annotations

import hashlib
import json
import math
import os
import re
from dataclasses import asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

from ml.registry.records import (
    CORRECTION_CATEGORIES,
    MISSING,
    Comparison,
    Evaluation,
    HumanCorrection,
    ModelVersion,
    TrainingRun,
)

_KINDS = {
    "models": ("v", re.compile(r"^v(\d{4,})$")),
    "runs": ("run-", re.compile(r"^run-(\d{4,})$")),
    "evaluations": ("eval-", re.compile(r"^eval-(\d{4,})$")),
    "corrections": ("cor-", re.compile(r"^cor-(\d{4,})$")),
}
_OPERATION_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9_-]{0,39}$")
_MAX_DESCRIPTION = 2000
DEFAULT_ROOT = Path(__file__).resolve().parents[1] / "experiments" / "registry"


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _plain_dict(value, name: str) -> dict:
    if not isinstance(value, dict):
        raise TypeError(f"{name} must be a dict")
    try:
        return json.loads(json.dumps(value, allow_nan=False))
    except TypeError:
        raise TypeError(f"{name} must be JSON serialisable") from None
    except ValueError:
        raise ValueError(f"{name} must not contain NaN or infinity") from None


def _int_list(values, name: str) -> list:
    items = list(values)
    for item in items:
        if isinstance(item, bool) or not isinstance(item, int):
            raise ValueError(f"{name} must contain integers")
    return items


class Registry:
    def __init__(self, root, clock: Callable[[], str] | None = None):
        self.root = Path(root)
        self.clock = clock or _utc_now

    def _ids(self, kind: str) -> list[str]:
        directory = self.root / kind
        if not directory.is_dir():
            return []
        pattern = _KINDS[kind][1]
        found = []
        for path in directory.glob("*.json"):
            match = pattern.fullmatch(path.stem)
            if match:
                found.append((int(match.group(1)), path.stem))
        return [name for _, name in sorted(found)]

    def _next_id(self, kind: str) -> str:
        prefix, pattern = _KINDS[kind]
        ids = self._ids(kind)
        number = int(pattern.fullmatch(ids[-1]).group(1)) + 1 if ids else 1
        return f"{prefix}{number:04d}"

    def _write(self, kind: str, record_id: str, record):
        directory = self.root / kind
        directory.mkdir(parents=True, exist_ok=True)
        target = directory / f"{record_id}.json"
        if target.exists():
            raise FileExistsError(str(target))
        temporary = directory / f"{record_id}.json.tmp"
        temporary.write_text(json.dumps(asdict(record), indent=2, sort_keys=True) + "\n", encoding="utf-8")
        os.replace(temporary, target)
        return record

    def _read(self, kind: str, record_id: str) -> dict:
        if not isinstance(record_id, str) or _KINDS[kind][1].fullmatch(record_id) is None:
            raise ValueError(f"invalid {kind} id")
        target = self.root / kind / f"{record_id}.json"
        if not target.is_file():
            raise KeyError(record_id)
        return json.loads(target.read_text(encoding="utf-8"))

    def model(self, version: str) -> ModelVersion:
        return ModelVersion(**self._read("models", version))

    def run(self, run_id: str) -> TrainingRun:
        return TrainingRun(**self._read("runs", run_id))

    def evaluation(self, eval_id: str) -> Evaluation:
        return Evaluation(**self._read("evaluations", eval_id))

    def correction(self, correction_id: str) -> HumanCorrection:
        return HumanCorrection(**self._read("corrections", correction_id))

    def models(self) -> list[ModelVersion]:
        return [self.model(i) for i in self._ids("models")]

    def runs(self) -> list[TrainingRun]:
        return [self.run(i) for i in self._ids("runs")]

    def evaluations(self, version: str | None = None) -> list[Evaluation]:
        items = [self.evaluation(i) for i in self._ids("evaluations")]
        return items if version is None else [e for e in items if e.version == version]

    def corrections(self) -> list[HumanCorrection]:
        return [self.correction(i) for i in self._ids("corrections")]

    def register_model(
        self,
        parameters: dict,
        parent: str | None = None,
        weights_path=None,
        notes: str = "",
    ) -> ModelVersion:
        clean = _plain_dict(parameters, "parameters")
        if parent is not None:
            self._read("models", parent)
        stored_path = None
        digest = None
        if weights_path is not None:
            path = Path(weights_path)
            if not path.is_file():
                raise FileNotFoundError(str(path))
            stored_path = str(path)
            digest = _sha256(path)
        record = ModelVersion(self._next_id("models"), self.clock(), parent, clean, stored_path, digest, notes)
        return self._write("models", record.version, record)

    def verify_weights(self, version: str) -> bool:
        record = self.model(version)
        if record.weights_path is None or record.weights_sha256 is None:
            raise ValueError("no weights recorded for this version")
        path = Path(record.weights_path)
        return path.is_file() and _sha256(path) == record.weights_sha256

    def record_run(
        self,
        output_version: str,
        iterations: int,
        seeds,
        base_version: str | None = None,
        parameters: dict | None = None,
        data_refs=(),
        correction_ids=(),
        notes: str = "",
    ) -> TrainingRun:
        self._read("models", output_version)
        if base_version is not None:
            self._read("models", base_version)
            if base_version == output_version:
                raise ValueError("a run must produce a different version than its base")
        if isinstance(iterations, bool) or not isinstance(iterations, int) or iterations < 1:
            raise ValueError("iterations must be a positive integer")
        corrections = list(correction_ids)
        if len(set(corrections)) != len(corrections):
            raise ValueError("correction ids must be unique")
        for correction_id in corrections:
            self._read("corrections", correction_id)
        record = TrainingRun(
            self._next_id("runs"),
            self.clock(),
            base_version,
            output_version,
            iterations,
            _int_list(seeds, "seeds"),
            _plain_dict(parameters or {}, "parameters"),
            [str(ref) for ref in data_refs],
            corrections,
            notes,
        )
        return self._write("runs", record.run_id, record)

    def record_evaluation(
        self,
        version: str,
        seeds,
        metrics: dict,
        run_id: str | None = None,
        notes: str = "",
    ) -> Evaluation:
        self._read("models", version)
        if run_id is not None:
            self._read("runs", run_id)
        if not isinstance(metrics, dict) or not metrics:
            raise ValueError("metrics must be a non-empty dict")
        clean = {}
        for name, value in metrics.items():
            if not isinstance(name, str) or not name:
                raise ValueError("metric names must be non-empty strings")
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
                raise ValueError(f"metric {name} must be a finite number")
            clean[name] = float(value)
        record = Evaluation(
            self._next_id("evaluations"),
            self.clock(),
            version,
            run_id,
            _int_list(seeds, "seeds"),
            clean,
            notes,
        )
        return self._write("evaluations", record.eval_id, record)

    def record_correction(
        self,
        operation: str,
        number: int,
        category: str,
        description: str,
        model_version: str | None = None,
    ) -> HumanCorrection:
        if not isinstance(operation, str) or _OPERATION_NAME.fullmatch(operation) is None:
            raise ValueError("operation must be a name of letters, digits, dashes or underscores")
        if isinstance(number, bool) or not isinstance(number, int) or number < 0:
            raise ValueError("number must be a non-negative integer")
        if category not in CORRECTION_CATEGORIES:
            raise ValueError("unknown correction category")
        text = description.strip() if isinstance(description, str) else ""
        if not text or len(text) > _MAX_DESCRIPTION:
            raise ValueError("description must be 1 to 2000 characters")
        if model_version is not None:
            self._read("models", model_version)
        record = HumanCorrection(
            self._next_id("corrections"),
            self.clock(),
            operation,
            number,
            category,
            text,
            model_version,
        )
        return self._write("corrections", record.correction_id, record)

    def pending_corrections(self) -> list[HumanCorrection]:
        used = {correction_id for run in self.runs() for correction_id in run.correction_ids}
        return [c for c in self.corrections() if c.correction_id not in used]

    def latest_evaluation(self, version: str) -> Evaluation | None:
        items = self.evaluations(version)
        return items[-1] if items else None

    def compare(self, base: str, other: str) -> Comparison:
        first_model = self.model(base)
        second_model = self.model(other)
        first = self.latest_evaluation(base)
        second = self.latest_evaluation(other)
        if first is None or second is None:
            raise ValueError("both versions need at least one evaluation to be compared")
        old = first_model.parameters
        new = second_model.parameters
        changes = {
            key: (old.get(key, MISSING), new.get(key, MISSING))
            for key in sorted(set(old) | set(new))
            if old.get(key, MISSING) != new.get(key, MISSING)
        }
        shared = sorted(set(first.metrics) & set(second.metrics))
        return Comparison(
            base,
            other,
            changes,
            {name: second.metrics[name] - first.metrics[name] for name in shared},
            sorted(set(first.metrics) - set(second.metrics)),
            sorted(set(second.metrics) - set(first.metrics)),
        )
