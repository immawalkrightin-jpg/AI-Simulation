from pathlib import Path

import pytest

from ml.registry.operation import parse_operation
from ml.registry.records import MISSING
from ml.registry.store import Registry

ML_ROOT = Path(__file__).resolve().parents[1]


class Clock:
    def __init__(self):
        self.count = 0

    def __call__(self):
        self.count += 1
        return f"2026-01-01T00:00:{self.count:02d}+00:00"


@pytest.fixture
def registry(tmp_path):
    return Registry(tmp_path / "registry", Clock())


def test_versions_are_numbered_in_order_and_keep_their_parent(registry):
    first = registry.register_model({"lr": 0.1})
    second = registry.register_model({"lr": 0.05}, parent=first.version)
    assert (first.version, second.version) == ("v0001", "v0002")
    assert second.parent == "v0001"
    assert [m.version for m in registry.models()] == ["v0001", "v0002"]


def test_unknown_parent_is_rejected(registry):
    with pytest.raises(KeyError):
        registry.register_model({}, parent="v0009")


def test_records_survive_a_reload_and_numbering_continues(tmp_path):
    root = tmp_path / "registry"
    Registry(root, Clock()).register_model({"lr": 0.1}, notes="baseline")
    reopened = Registry(root, Clock())
    assert reopened.model("v0001").notes == "baseline"
    assert reopened.register_model({"lr": 0.2}).version == "v0002"


def test_weights_are_hashed_and_tampering_is_detected(registry, tmp_path):
    weights = tmp_path / "weights.bin"
    weights.write_bytes(b"original")
    record = registry.register_model({}, weights_path=weights)
    assert record.weights_sha256 is not None
    assert registry.verify_weights(record.version) is True
    weights.write_bytes(b"tampered")
    assert registry.verify_weights(record.version) is False
    weights.unlink()
    assert registry.verify_weights(record.version) is False


def test_weights_must_exist_and_verification_needs_recorded_weights(registry, tmp_path):
    with pytest.raises(FileNotFoundError):
        registry.register_model({}, weights_path=tmp_path / "missing.bin")
    bare = registry.register_model({})
    with pytest.raises(ValueError):
        registry.verify_weights(bare.version)


def test_parameters_must_be_plain_json(registry):
    with pytest.raises(TypeError):
        registry.register_model({"bad": object()})
    with pytest.raises(ValueError):
        registry.register_model({"bad": float("nan")})
    with pytest.raises(TypeError):
        registry.register_model(["not", "a", "dict"])


def test_runs_link_versions_data_seeds_and_corrections(registry):
    base = registry.register_model({"lr": 0.1})
    update = registry.register_model({"lr": 0.05}, parent=base.version)
    correction = registry.record_correction("alpha", 3, "target_missed", "Shell landed 400 units short.")
    run = registry.record_run(
        update.version,
        iterations=200,
        seeds=[1, 2, 3],
        base_version=base.version,
        parameters={"episodes": 200},
        data_refs=["data/datasets/run-a.jsonl"],
        correction_ids=[correction.correction_id],
        notes="first update",
    )
    loaded = registry.run(run.run_id)
    assert loaded.base_version == "v0001"
    assert loaded.output_version == "v0002"
    assert loaded.seeds == [1, 2, 3]
    assert loaded.data_refs == ["data/datasets/run-a.jsonl"]
    assert loaded.correction_ids == [correction.correction_id]


def test_run_validation(registry):
    model = registry.register_model({})
    with pytest.raises(KeyError):
        registry.record_run("v0009", 1, [1])
    with pytest.raises(ValueError):
        registry.record_run(model.version, 0, [1])
    with pytest.raises(ValueError):
        registry.record_run(model.version, 1, [1], base_version=model.version)
    with pytest.raises(ValueError):
        registry.record_run(model.version, 1, ["one"])
    with pytest.raises(KeyError):
        registry.record_run(model.version, 1, [1], correction_ids=["cor-0009"])


def test_evaluations_keep_only_finite_numbers_and_latest_wins(registry):
    model = registry.register_model({})
    registry.record_evaluation(model.version, [1], {"score": 1})
    second = registry.record_evaluation(model.version, [1, 2], {"score": 2.5})
    assert registry.latest_evaluation(model.version).eval_id == second.eval_id
    assert registry.latest_evaluation(model.version).metrics == {"score": 2.5}
    for bad in ({}, {"score": float("nan")}, {"score": float("inf")}, {"score": True}, {"score": "high"}, {"": 1.0}):
        with pytest.raises(ValueError):
            registry.record_evaluation(model.version, [1], bad)
    with pytest.raises(KeyError):
        registry.record_evaluation("v0009", [1], {"score": 1.0})
    with pytest.raises(KeyError):
        registry.record_evaluation(model.version, [1], {"score": 1.0}, run_id="run-0009")


def test_latest_evaluation_is_none_without_evaluations(registry):
    assert registry.latest_evaluation(registry.register_model({}).version) is None


def test_comparison_reports_parameter_changes_and_metric_deltas(registry):
    old = registry.register_model({"lr": 0.1, "layers": 2, "dropout": 0.0})
    new = registry.register_model({"lr": 0.05, "layers": 2, "width": 64}, parent=old.version)
    registry.record_evaluation(old.version, [1], {"score": 10.0, "losses": 4.0, "legacy": 1.0})
    registry.record_evaluation(new.version, [1], {"score": 12.5, "losses": 3.0, "fresh": 9.0})
    result = registry.compare(old.version, new.version)
    assert result.parameter_changes == {
        "dropout": (0.0, MISSING),
        "lr": (0.1, 0.05),
        "width": (MISSING, 64),
    }
    assert result.metric_deltas == {"losses": -1.0, "score": 2.5}
    assert result.base_only_metrics == ["legacy"]
    assert result.other_only_metrics == ["fresh"]


def test_comparison_needs_evaluations_on_both_versions(registry):
    first = registry.register_model({})
    second = registry.register_model({})
    registry.record_evaluation(first.version, [1], {"score": 1.0})
    with pytest.raises(ValueError):
        registry.compare(first.version, second.version)


def test_corrections_are_validated(registry):
    for operation in ("", "1abc", "bad name", "x" * 41):
        with pytest.raises(ValueError):
            registry.record_correction(operation, 1, "target_missed", "text")
    for number in (-1, True, "3"):
        with pytest.raises(ValueError):
            registry.record_correction("alpha", number, "target_missed", "text")
    with pytest.raises(ValueError):
        registry.record_correction("alpha", 1, "unknown_category", "text")
    for description in ("", "   ", "x" * 2001):
        with pytest.raises(ValueError):
            registry.record_correction("alpha", 1, "target_missed", description)
    with pytest.raises(KeyError):
        registry.record_correction("alpha", 1, "target_missed", "text", model_version="v0009")


def test_corrections_stay_pending_until_a_run_includes_them(registry):
    model = registry.register_model({})
    first = registry.record_correction("alpha", 1, "friendly_fire", "Hit own supply convoy.", model.version)
    second = registry.record_correction("bravo", 2, "supply_allocation", "Ammunition sent to the wrong depot.")
    assert [c.correction_id for c in registry.pending_corrections()] == [first.correction_id, second.correction_id]
    update = registry.register_model({"lr": 0.01}, parent=model.version)
    registry.record_run(update.version, 10, [1], base_version=model.version, correction_ids=[first.correction_id])
    assert [c.correction_id for c in registry.pending_corrections()] == [second.correction_id]


def test_corrections_and_runs_never_modify_existing_model_records(registry):
    model = registry.register_model({"lr": 0.1})
    path = registry.root / "models" / f"{model.version}.json"
    before = path.read_bytes()
    correction = registry.record_correction("alpha", 1, "tactical_mistake", "Flanked into a minefield.", model.version)
    update = registry.register_model({"lr": 0.1}, parent=model.version)
    registry.record_run(update.version, 5, [1], base_version=model.version, correction_ids=[correction.correction_id])
    registry.record_evaluation(model.version, [1], {"score": 1.0})
    assert path.read_bytes() == before
    assert registry.model(model.version).parameters == {"lr": 0.1}


def test_lookups_reject_malformed_and_unknown_ids(registry):
    registry.register_model({})
    for bad in ("../v0001", "v1", "v0001\n", "", "V0001"):
        with pytest.raises(ValueError):
            registry.model(bad)
    with pytest.raises(KeyError):
        registry.model("v0099")


def test_existing_records_are_never_overwritten(registry):
    record = registry.register_model({})
    with pytest.raises(FileExistsError):
        registry._write("models", record.version, record)


def test_no_temporary_files_are_left_behind(registry):
    registry.register_model({})
    registry.record_correction("alpha", 1, "target_missed", "text")
    assert list(registry.root.rglob("*.tmp")) == []


def test_operation_convention_is_parsed():
    assert parse_operation("operating alpha 12") == ("alpha", 12)
    assert parse_operation("  operating Strike-Group_2   7  ") == ("Strike-Group_2", 7)
    for bad in ("operating alpha", "operating 12", "operate alpha 1", "operating alpha -1", "operating alpha 1 2", ""):
        with pytest.raises(ValueError):
            parse_operation(bad)


def test_registry_is_independent_of_the_simulation_and_its_clients():
    for source in (ML_ROOT / "registry").rglob("*.py"):
        text = source.read_text(encoding="utf-8")
        for token in ("ml.environment", "ml.central_intelligence", "ml.training", "/api/", "ground-truth"):
            assert token not in text, f"{token} found in {source}"
