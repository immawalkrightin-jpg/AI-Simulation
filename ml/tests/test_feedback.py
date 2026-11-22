import json
import re
from pathlib import Path

import pytest

from ml.feedback.cli import main
from ml.feedback.inbox import import_inbox, read_inbox
from ml.registry.records import CORRECTION_CATEGORIES
from ml.registry.store import Registry

ML_ROOT = Path(__file__).resolve().parents[1]
FEEDBACK_STORE = ML_ROOT.parent / "simulation" / "Simulation.API" / "Services" / "FeedbackStore.cs"


def entry(source_id="a1", operation="alpha", number=12, category="friendly_fire", description="Hit own convoy.", tick=340, seed=42):
    return {
        "id": source_id,
        "receivedAt": "2026-01-01T00:00:00+00:00",
        "operation": operation,
        "number": number,
        "category": category,
        "description": description,
        "tick": tick,
        "seed": seed,
    }


def write_inbox(path, items, raw=()):
    lines = [json.dumps(item) for item in items] + list(raw)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


@pytest.fixture
def registry(tmp_path):
    return Registry(tmp_path / "registry")


@pytest.fixture
def inbox(tmp_path):
    return tmp_path / "corrections.jsonl"


def test_entries_become_registry_corrections_with_source_and_context(registry, inbox):
    write_inbox(inbox, [entry("a1"), entry("a2", operation="bravo", number=3, category="target_missed", description="Short by 400.")])
    result = import_inbox(registry, inbox)
    assert len(result.imported) == 2
    first, second = registry.corrections()
    assert (first.operation, first.number, first.category) == ("alpha", 12, "friendly_fire")
    assert first.source_id == "a1"
    assert first.context == {"seed": 42, "tick": 340, "received_at": "2026-01-01T00:00:00+00:00"}
    assert (second.operation, second.number, second.source_id) == ("bravo", 3, "a2")


def test_importing_twice_does_not_duplicate(registry, inbox):
    write_inbox(inbox, [entry("a1"), entry("a2")])
    import_inbox(registry, inbox)
    again = import_inbox(registry, inbox)
    assert again.imported == []
    assert again.already_imported == 2
    assert len(registry.corrections()) == 2


def test_later_entries_are_imported_incrementally(registry, inbox):
    write_inbox(inbox, [entry("a1")])
    import_inbox(registry, inbox)
    write_inbox(inbox, [entry("a1"), entry("a2"), entry("a3")])
    result = import_inbox(registry, inbox)
    assert [c.source_id for c in result.imported] == ["a2", "a3"]
    assert result.already_imported == 1
    assert len(registry.corrections()) == 3


def test_a_duplicate_id_inside_one_inbox_is_imported_once(registry, inbox):
    write_inbox(inbox, [entry("a1"), entry("a1")])
    result = import_inbox(registry, inbox)
    assert len(result.imported) == 1
    assert result.already_imported == 1


def test_bad_entries_are_rejected_without_blocking_good_ones(registry, inbox):
    items = [
        entry("good-1"),
        entry("bad-category", category="nonsense"),
        entry("bad-number", number=-4),
        entry("bad-text", description="   "),
        {"operation": "alpha", "number": 1, "category": "target_missed", "description": "no id"},
        entry("good-2"),
    ]
    write_inbox(inbox, items, raw=["{broken", "[1, 2]", ""])
    result = import_inbox(registry, inbox)
    assert [c.source_id for c in result.imported] == ["good-1", "good-2"]
    assert {source for source, _ in result.rejected} == {"bad-category", "bad-number", "bad-text", None}
    assert result.malformed == 2


def test_missing_inbox_imports_nothing(registry, tmp_path):
    result = import_inbox(registry, tmp_path / "absent.jsonl")
    assert (result.imported, result.already_imported, result.rejected, result.malformed) == ([], 0, [], 0)
    assert read_inbox(tmp_path / "absent.jsonl") == ([], 0)


def test_model_version_is_stamped_and_must_exist(registry, inbox):
    write_inbox(inbox, [entry("a1")])
    with pytest.raises(KeyError):
        import_inbox(registry, inbox, model_version="v0009")
    assert registry.corrections() == []
    model = registry.register_model({"lr": 0.1})
    import_inbox(registry, inbox, model_version=model.version)
    assert registry.corrections()[0].model_version == model.version


def test_imported_corrections_stay_pending_until_a_run_uses_them(registry, inbox):
    write_inbox(inbox, [entry("a1"), entry("a2")])
    import_inbox(registry, inbox)
    pending = registry.pending_corrections()
    assert len(pending) == 2
    base = registry.register_model({"lr": 0.1})
    update = registry.register_model({"lr": 0.05}, parent=base.version)
    registry.record_run(update.version, 10, [1], base_version=base.version, correction_ids=[pending[0].correction_id])
    assert [c.source_id for c in registry.pending_corrections()] == ["a2"]


def test_registry_rejects_a_repeated_or_oversized_source_id(registry):
    registry.record_correction("alpha", 1, "target_missed", "text", source_id="s1")
    with pytest.raises(ValueError):
        registry.record_correction("alpha", 2, "target_missed", "text", source_id="s1")
    with pytest.raises(ValueError):
        registry.record_correction("alpha", 2, "target_missed", "text", source_id="x" * 65)
    with pytest.raises(ValueError):
        registry.record_correction("alpha", 2, "target_missed", "text", source_id="")
    assert len(registry.corrections()) == 1


def test_corrections_written_before_source_ids_still_load(registry):
    record = registry.record_correction("alpha", 1, "target_missed", "text")
    path = registry.root / "corrections" / f"{record.correction_id}.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    del data["source_id"]
    del data["context"]
    path.write_text(json.dumps(data), encoding="utf-8")
    loaded = registry.correction(record.correction_id)
    assert loaded.source_id is None
    assert loaded.context == {}


def test_command_line_import_reports_and_sets_exit_codes(tmp_path, capsys):
    inbox = tmp_path / "corrections.jsonl"
    root = tmp_path / "registry"
    write_inbox(inbox, [entry("a1"), entry("a2")])
    assert main(["--inbox", str(inbox), "--registry", str(root)]) == 0
    assert "imported 2, already imported 0" in capsys.readouterr().out
    assert main(["--inbox", str(inbox), "--registry", str(root)]) == 0
    assert "imported 0, already imported 2" in capsys.readouterr().out
    write_inbox(inbox, [entry("a1"), entry("a2"), entry("bad", category="nonsense")])
    assert main(["--inbox", str(inbox), "--registry", str(root)]) == 1
    assert "rejected bad" in capsys.readouterr().out
    assert main(["--inbox", str(inbox), "--registry", str(root), "--model-version", "v0009"]) == 2


def test_categories_match_the_csharp_api():
    text = FEEDBACK_STORE.read_text(encoding="utf-8")
    block = re.search(r"Categories\s*=\s*new\[\]\s*\{(.*?)\}", text, re.S)
    assert block is not None
    assert set(re.findall(r'"([a-z_]+)"', block.group(1))) == set(CORRECTION_CATEGORIES)


def test_feedback_code_is_independent_of_the_simulation_and_agent():
    for source in (ML_ROOT / "feedback").rglob("*.py"):
        text = source.read_text(encoding="utf-8")
        for token in ("ml.environment", "ml.central_intelligence", "ml.training", "/api/", "ground-truth"):
            assert token not in text, f"{token} found in {source}"
