from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from ml.registry.store import Registry

DEFAULT_INBOX = Path(__file__).resolve().parents[2] / "data" / "feedback" / "corrections.jsonl"


@dataclass(frozen=True)
class ImportResult:
    imported: list
    already_imported: int
    rejected: list
    malformed: int


def read_inbox(path) -> tuple[list[dict], int]:
    target = Path(path)
    if not target.is_file():
        return [], 0
    entries: list[dict] = []
    malformed = 0
    for line in target.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            entry = json.loads(line)
        except json.JSONDecodeError:
            malformed += 1
            continue
        if isinstance(entry, dict):
            entries.append(entry)
        else:
            malformed += 1
    return entries, malformed


def _context(entry: dict) -> dict:
    mapping = {"seed": "seed", "tick": "tick", "receivedAt": "received_at"}
    context = {}
    for source, target in mapping.items():
        value = entry.get(source)
        if isinstance(value, (int, str)) and not isinstance(value, bool):
            context[target] = value
    return context


def import_inbox(registry: Registry, inbox_path, model_version: str | None = None) -> ImportResult:
    if model_version is not None:
        registry.model(model_version)
    entries, malformed = read_inbox(inbox_path)
    known = {c.source_id for c in registry.corrections() if c.source_id}
    imported = []
    already = 0
    rejected = []
    for entry in entries:
        source_id = entry.get("id")
        if not isinstance(source_id, str) or not source_id:
            rejected.append((None, "missing id"))
            continue
        if source_id in known:
            already += 1
            continue
        try:
            record = registry.record_correction(
                entry.get("operation"),
                entry.get("number"),
                entry.get("category"),
                entry.get("description"),
                model_version=model_version,
                source_id=source_id,
                context=_context(entry),
            )
        except ValueError as error:
            rejected.append((source_id, str(error)))
            continue
        known.add(source_id)
        imported.append(record)
    return ImportResult(imported, already, rejected, malformed)
