from pathlib import Path

import pytest

from ml.central_intelligence.agent import Agent
from ml.environment.client import AgentClient, ApiError
from ml.environment.operator_client import OperatorClient
from ml.environment.simulation_env import SimulationEnv
from ml.tests.fake_api import FakeApi
from ml.training.episode import run_episode

ML_ROOT = Path(__file__).resolve().parents[1]


@pytest.fixture
def api():
    fake = FakeApi()
    yield fake
    fake.close()


def make_env(api, horizon=10, ticks_per_step=1):
    return SimulationEnv(OperatorClient(api.base, "gt"), horizon=horizon, ticks_per_step=ticks_per_step)


def make_agent(api):
    return Agent(AgentClient(api.base, "ai"))


def stationary_contact(api, tick):
    api.add_observation(1500, 800, tick)


def test_reset_pauses_before_resetting_and_reports_seed_and_tick(api):
    api.paused = False
    state = make_env(api).reset(7)
    assert (state.seed, state.tick) == (7, 0)
    assert api.paused is True
    posts = [path for method, path in api.requests if method == "POST"]
    assert posts == ["/api/simulation/pause", "/api/simulation/reset?seed=7"]


def test_reset_without_a_seed_keeps_the_servers_seed(api):
    api.seed = 13
    assert make_env(api).reset().seed == 13


def test_advance_steps_by_ticks_per_step_and_never_overshoots_the_horizon(api):
    env = make_env(api, horizon=5, ticks_per_step=2)
    env.reset(1)
    assert [env.advance().tick for _ in range(3)] == [2, 4, 5]
    assert env.state().done
    assert env.advance().tick == 5


def test_advance_splits_large_requests_into_server_sized_steps(api):
    env = make_env(api, horizon=2500)
    env.reset(1)
    assert env.advance(2500).tick == 2500
    steps = [path for method, path in api.requests if path.startswith("/api/simulation/step")]
    assert steps == [
        "/api/simulation/step?count=1000",
        "/api/simulation/step?count=1000",
        "/api/simulation/step?count=500",
    ]


def test_advance_and_state_require_a_reset_first(api):
    env = make_env(api)
    with pytest.raises(RuntimeError):
        env.advance()
    with pytest.raises(RuntimeError):
        env.state()


def test_invalid_environment_arguments_are_rejected(api):
    with pytest.raises(ValueError):
        make_env(api, horizon=0)
    with pytest.raises(ValueError):
        make_env(api, ticks_per_step=0)
    env = make_env(api)
    env.reset(1)
    with pytest.raises(ValueError):
        env.advance(0)


def test_negative_seed_is_rejected_by_the_server(api):
    with pytest.raises(ApiError) as error:
        OperatorClient(api.base, "gt").reset(-1)
    assert error.value.status == 400


def test_operator_endpoints_reject_the_ai_key(api):
    with pytest.raises(ApiError) as error:
        OperatorClient(api.base, "ai").reset(1)
    assert error.value.status == 401


def test_agent_client_cannot_reset_or_step():
    for name in ("reset", "step", "pause", "resume"):
        assert not hasattr(AgentClient, name)


def test_episode_runs_to_the_horizon_and_summarises_the_agents_work(api):
    api.step_hook = stationary_contact
    summary = run_episode(make_env(api, horizon=8), make_agent(api), seed=5)
    assert summary.seed == 5
    assert summary.ticks == 8
    assert summary.agent_steps == 8
    assert summary.tracks >= 1
    assert summary.points >= 1
    assert summary.point_updates >= 1


def test_episodes_start_clean_and_do_not_leak_between_runs(api):
    api.step_hook = stationary_contact
    env = make_env(api, horizon=8)
    agent = make_agent(api)
    run_episode(env, agent, seed=5)
    api.step_hook = None
    second = run_episode(env, agent, seed=6)
    assert second.seed == 6
    assert second.ticks == 8
    assert second.tracks == 0
    assert second.points == 0
    assert second.point_updates == 0


def test_same_seed_and_same_world_give_identical_episodes(api):
    api.step_hook = stationary_contact
    env = make_env(api, horizon=8)
    agent = make_agent(api)
    first = run_episode(env, agent, seed=9)
    second = run_episode(env, agent, seed=9)
    assert first == second


def test_environment_and_training_code_never_read_ground_truth():
    for folder in ("environment", "training"):
        for source in (ML_ROOT / folder).rglob("*.py"):
            text = source.read_text(encoding="utf-8")
            assert "ground-truth" not in text, f"ground-truth found in {source}"
