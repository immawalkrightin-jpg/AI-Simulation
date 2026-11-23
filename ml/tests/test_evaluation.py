from pathlib import Path

import pytest

from ml.central_intelligence.agent import Agent
from ml.environment.client import AgentClient, ApiError
from ml.environment.operator_client import OperatorClient
from ml.environment.simulation_env import SimulationEnv
from ml.evaluation.evaluator import EpisodeEvaluator, TruthClient
from ml.evaluation.runner import aggregate, evaluate_version
from ml.evaluation.scoring import score_picture
from ml.registry.store import Registry
from ml.tests.fake_api import FakeApi
from ml.training.episode import run_episode

ML_ROOT = Path(__file__).resolve().parents[1]


def unit(unit_id, x, y, owner="OPP", status="Idle"):
    return {"id": unit_id, "owner": owner, "kind": "Armor", "x": x, "y": y, "status": status}


def track(point_id, x, y, confidence="Estimated", kind="Tracking"):
    return {"id": point_id, "kind": kind, "confidence": confidence, "x": x, "y": y}


def snapshot(units, sensors=None, tick=10):
    default = [{"id": 1, "owner": "MDL", "x": 0.0, "y": 0.0, "range": 500.0}]
    return {
        "tick": tick,
        "countries": [{"id": "MDL", "isModel": True}, {"id": "OPP", "isModel": False}],
        "units": units,
        "sensors": default if sensors is None else sensors,
    }


def test_a_perfect_picture_scores_full_marks():
    score = score_picture(snapshot([unit(1, 100, 0), unit(2, 200, 0)]), [track("trk-1", 101, 0), track("trk-2", 200, 1)])
    assert (score.recall, score.precision) == (1.0, 1.0)
    assert score.mean_position_error == pytest.approx(1.0)
    assert (score.false_tracks, score.missed_units, score.friendly_tracks) == (0, 0, 0)
    assert score.tick == 10


def test_an_empty_world_and_empty_map_are_not_penalised():
    score = score_picture(snapshot([]), [])
    assert (score.recall, score.precision, score.mean_position_error, score.confirmed_precision) == (1.0, 1.0, 0.0, 1.0)


def test_missing_a_reachable_unit_lowers_recall():
    score = score_picture(snapshot([unit(1, 100, 0), unit(2, 200, 0)]), [track("trk-1", 100, 0)])
    assert score.recall == 0.5
    assert score.missed_units == 1
    assert score.precision == 1.0


def test_a_track_with_no_unit_behind_it_lowers_precision():
    score = score_picture(snapshot([unit(1, 100, 0)]), [track("trk-1", 100, 0), track("trk-2", 400, 400)])
    assert score.precision == 0.5
    assert score.false_tracks == 1
    assert score.recall == 1.0


def test_destroyed_units_are_neither_targets_nor_valid_matches():
    units = [unit(1, 100, 0), unit(2, 200, 0, status="Destroyed")]
    score = score_picture(snapshot(units), [track("trk-a", 100, 0), track("trk-b", 200, 0)])
    assert score.detectable == 1
    assert score.recall == 1.0
    assert score.precision == 0.5
    assert score.false_tracks == 1


def test_units_beyond_every_sensor_are_not_expected_to_be_found():
    units = [unit(1, 100, 0), unit(2, 900, 0)]
    quiet = score_picture(snapshot(units), [track("trk-1", 100, 0)])
    assert quiet.detectable == 1
    assert quiet.recall == 1.0
    both = score_picture(snapshot(units), [track("trk-1", 100, 0), track("trk-2", 900, 0)])
    assert both.matched_tracks == 2
    assert both.precision == 1.0
    assert both.recall == 1.0


def test_only_the_models_sensors_define_what_is_reachable_when_it_has_some():
    sensors = [
        {"id": 1, "owner": "MDL", "x": 0.0, "y": 0.0, "range": 50.0},
        {"id": 2, "owner": "OPP", "x": 900.0, "y": 0.0, "range": 500.0},
    ]
    score = score_picture(snapshot([unit(1, 900, 0)], sensors=sensors), [])
    assert score.detectable == 0


def test_without_model_sensors_every_sensor_counts():
    sensors = [{"id": 1, "owner": "OPP", "x": 0.0, "y": 0.0, "range": 500.0}]
    assert score_picture(snapshot([unit(1, 100, 0)], sensors=sensors), []).detectable == 1
    assert score_picture(snapshot([unit(1, 100, 0)], sensors=[]), []).detectable == 0


def test_tracks_on_own_units_are_counted_as_friendly_confusion():
    units = [unit(1, 50, 0, owner="MDL"), unit(2, 100, 0)]
    score = score_picture(snapshot(units), [track("trk-1", 50, 0), track("trk-2", 100, 0)])
    assert score.friendly_tracks == 1
    assert score.matched_tracks == 2


def test_one_unit_can_be_matched_by_only_one_track():
    score = score_picture(snapshot([unit(1, 100, 0)]), [track("a", 100, 5), track("b", 100, 10)])
    assert score.matched_tracks == 1
    assert score.false_tracks == 1
    assert score.error_sum == pytest.approx(5.0)


def test_only_tracking_points_are_scored():
    points = [
        track("trk-1", 100, 0),
        track("prd-1", 400, 400, kind="ExpectedActivity"),
        track("tgt-1", 400, 400, kind="SuspectedTarget"),
    ]
    score = score_picture(snapshot([unit(1, 100, 0)]), points)
    assert score.tracks == 1
    assert score.precision == 1.0


def test_the_match_distance_is_inclusive():
    world = snapshot([unit(1, 0, 0)])
    assert score_picture(world, [track("a", 60, 0)]).matched_tracks == 1
    assert score_picture(world, [track("a", 60.01, 0)]).matched_tracks == 0
    assert score_picture(world, [track("a", 60.01, 0)], match_distance=70).matched_tracks == 1


def test_match_distance_must_be_positive():
    with pytest.raises(ValueError):
        score_picture(snapshot([]), [], match_distance=0)


def test_confirmed_tracks_are_scored_separately():
    points = [
        track("a", 100, 0, confidence="Confirmed"),
        track("b", 400, 400, confidence="Confirmed"),
        track("c", 450, 450, confidence="Estimated"),
    ]
    score = score_picture(snapshot([unit(1, 100, 0)]), points)
    assert score.confirmed_tracks == 2
    assert score.confirmed_precision == 0.5


def test_equal_distances_are_resolved_by_unit_id():
    units = [unit(7, 10, 0), unit(3, 0, 10, owner="MDL")]
    score = score_picture(snapshot(units), [track("trk-1", 0, 0)])
    assert score.matched_tracks == 1
    assert score.friendly_tracks == 1


@pytest.fixture
def api():
    fake = FakeApi()
    yield fake
    fake.close()


def truth_client(api):
    return TruthClient(api.base, "gt")


def make_env(api, horizon=12):
    return SimulationEnv(OperatorClient(api.base, "gt"), horizon=horizon)


def make_agent(api):
    return Agent(AgentClient(api.base, "ai"))


def contact_in_reach(api, tick):
    api.add_observation(300, 300, tick)


def test_the_truth_client_needs_the_operator_key(api):
    with pytest.raises(ApiError) as error:
        TruthClient(api.base, "ai").snapshot()
    assert error.value.status == 401
    assert truth_client(api).snapshot()["tick"] == 0


def test_the_evaluator_accumulates_steps_and_summarises_them(api):
    api.units = [unit(1, 300, 300)]
    api.tick = 5
    evaluator = EpisodeEvaluator(truth_client(api))
    evaluator.observe([track("trk-1", 300, 300)])
    evaluator.observe([])
    summary = evaluator.summary()
    assert summary["steps_evaluated"] == 2.0
    assert summary["mean_recall"] == 0.5
    assert summary["final_recall"] == 0.0
    assert summary["mean_precision"] == 1.0
    assert summary["mean_position_error"] == 0.0
    assert summary["mean_missed_units"] == 0.5
    assert all(isinstance(value, float) for value in summary.values())


def test_the_evaluator_resets_and_refuses_an_empty_summary(api):
    evaluator = EpisodeEvaluator(truth_client(api))
    with pytest.raises(ValueError):
        evaluator.summary()
    evaluator.observe([])
    evaluator.reset()
    with pytest.raises(ValueError):
        evaluator.summary()


def test_position_error_averages_only_steps_that_had_matches(api):
    api.units = [unit(1, 300, 300)]
    evaluator = EpisodeEvaluator(truth_client(api))
    evaluator.observe([track("trk-1", 306, 300)])
    evaluator.observe([])
    assert evaluator.summary()["mean_position_error"] == pytest.approx(6.0)


def test_an_episode_reports_metrics_for_a_contact_the_agent_learns_to_track(api):
    api.units = [unit(1, 300, 300)]
    api.step_hook = contact_in_reach
    summary = run_episode(make_env(api), make_agent(api), 5, EpisodeEvaluator(truth_client(api)))
    assert summary.metrics["steps_evaluated"] == 12.0
    assert summary.metrics["final_recall"] == 1.0
    assert summary.metrics["final_precision"] == 1.0
    assert 0.0 < summary.metrics["mean_recall"] < 1.0
    assert summary.metrics["mean_false_tracks"] == 0.0


def test_a_phantom_contact_is_reported_as_false_tracks(api):
    api.units = []
    api.step_hook = contact_in_reach
    summary = run_episode(make_env(api), make_agent(api), 5, EpisodeEvaluator(truth_client(api)))
    assert summary.metrics["final_precision"] == 0.0
    assert summary.metrics["mean_false_tracks"] > 0.0


def test_evaluating_every_n_steps_still_scores_the_final_state(api):
    api.units = [unit(1, 300, 300)]
    api.step_hook = contact_in_reach
    summary = run_episode(make_env(api), make_agent(api), 5, EpisodeEvaluator(truth_client(api)), evaluate_every=5)
    assert summary.metrics["steps_evaluated"] == 3.0
    assert summary.metrics["final_recall"] == 1.0


def test_an_episode_without_an_evaluator_has_no_metrics(api):
    api.step_hook = contact_in_reach
    assert run_episode(make_env(api), make_agent(api), 5).metrics == {}


def test_invalid_evaluation_interval_is_rejected_before_anything_runs(api):
    with pytest.raises(ValueError):
        run_episode(make_env(api), make_agent(api), 5, EpisodeEvaluator(truth_client(api)), evaluate_every=0)
    assert api.requests == []


def test_the_same_seed_and_world_give_identical_metrics(api):
    api.units = [unit(1, 300, 300)]
    api.step_hook = contact_in_reach
    env = make_env(api)
    agent = make_agent(api)
    evaluator = EpisodeEvaluator(truth_client(api))
    assert run_episode(env, agent, 9, evaluator) == run_episode(env, agent, 9, evaluator)


def test_aggregate_averages_each_metric_across_episodes():
    assert aggregate([{"a": 1.0, "b": 4.0}, {"a": 3.0, "b": 8.0}]) == {"a": 2.0, "b": 6.0}
    with pytest.raises(ValueError):
        aggregate([])
    with pytest.raises(ValueError):
        aggregate([{"a": 1.0}, {"b": 1.0}])


def test_a_version_is_evaluated_across_seeds_and_recorded_in_the_registry(api, tmp_path):
    api.units = [unit(1, 300, 300)]
    api.step_hook = contact_in_reach
    registry = Registry(tmp_path / "registry")
    version = registry.register_model({"lr": 0.1})
    result = evaluate_version(
        registry, version.version, make_env(api), make_agent(api), EpisodeEvaluator(truth_client(api)), [1, 2, 3],
        notes="baseline")
    assert result.version == version.version
    assert result.seeds == [1, 2, 3]
    assert result.metrics["steps_evaluated"] == 12.0
    assert result.metrics["final_recall"] == 1.0
    assert registry.latest_evaluation(version.version).eval_id == result.eval_id
    resets = [path for method, path in api.requests if path.startswith("/api/simulation/reset")]
    assert resets == ["/api/simulation/reset?seed=1", "/api/simulation/reset?seed=2", "/api/simulation/reset?seed=3"]


def test_two_versions_can_be_compared_after_evaluation(api, tmp_path):
    api.units = [unit(1, 300, 300)]
    api.step_hook = contact_in_reach
    registry = Registry(tmp_path / "registry")
    first = registry.register_model({"lr": 0.1})
    second = registry.register_model({"lr": 0.05}, parent=first.version)
    for version in (first, second):
        evaluate_version(registry, version.version, make_env(api), make_agent(api), EpisodeEvaluator(truth_client(api)), [1])
    comparison = registry.compare(first.version, second.version)
    assert comparison.parameter_changes == {"lr": (0.1, 0.05)}
    assert comparison.metric_deltas["final_recall"] == 0.0


def test_evaluation_validates_before_running_any_episode(api, tmp_path):
    registry = Registry(tmp_path / "registry")
    version = registry.register_model({})
    arguments = (make_env(api), make_agent(api), EpisodeEvaluator(truth_client(api)))
    with pytest.raises(KeyError):
        evaluate_version(registry, "v0009", *arguments, [1])
    for seeds in ([], [-1], [True], ["3"]):
        with pytest.raises(ValueError):
            evaluate_version(registry, version.version, *arguments, seeds)
    assert api.requests == []
    assert registry.evaluations() == []


def sources(*folders):
    for folder in folders:
        yield from (ML_ROOT / folder).rglob("*.py")


def test_only_the_evaluation_package_reads_ground_truth():
    checked = list(sources("agents", "central_intelligence", "environment", "training", "registry", "feedback"))
    checked.append(ML_ROOT / "run_agent.py")
    assert len(checked) > 5
    for source in checked:
        assert "ground-truth" not in source.read_text(encoding="utf-8"), f"ground-truth found in {source}"
    assert "ground-truth" in (ML_ROOT / "evaluation" / "evaluator.py").read_text(encoding="utf-8")


def test_the_agent_and_training_code_never_import_the_evaluation_package():
    for source in sources("agents", "central_intelligence", "training"):
        assert "ml.evaluation" not in source.read_text(encoding="utf-8"), f"evaluation imported in {source}"
    assert "ml.evaluation" not in (ML_ROOT / "run_agent.py").read_text(encoding="utf-8")


def test_the_evaluation_package_does_not_import_the_agent_code():
    for source in sources("evaluation"):
        text = source.read_text(encoding="utf-8")
        for token in ("ml.central_intelligence", "ml.agents"):
            assert token not in text, f"{token} found in {source}"
