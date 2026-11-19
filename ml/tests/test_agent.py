from pathlib import Path

import pytest

from ml.central_intelligence.agent import Agent
from ml.environment.client import AgentClient, ApiError
from ml.tests.fake_api import FakeApi

ML_ROOT = Path(__file__).resolve().parents[1]


@pytest.fixture
def api():
    fake = FakeApi()
    yield fake
    fake.close()


def feed_moving_contact(api, start_tick, ticks, x0, y0, vx, vy):
    for i in range(ticks):
        tick = start_tick + i
        api.add_observation(x0 + vx * i, y0 + vy * i, tick)


def test_agent_places_points_for_a_persistent_contact(api):
    agent = Agent(AgentClient(api.base, "ai"))
    agent.bootstrap()
    feed_moving_contact(api, 0, 6, 1500, 800, 0, 0)
    api.tick = 5
    agent.step()
    assert "trk-T1" in api.points
    assert api.points["trk-T1"]["kind"] == "Tracking"


def test_agent_escalates_a_contact_approaching_its_sensor(api):
    agent = Agent(AgentClient(api.base, "ai"))
    agent.bootstrap()
    feed_moving_contact(api, 0, 8, 160, 100, -4, 0)
    api.tick = 7
    agent.step()
    kinds = {p["kind"] for p in api.points.values()}
    assert "SuspectedTarget" in kinds or "ExpectedActivity" in kinds


def test_agent_removes_points_when_the_contact_is_lost(api):
    agent = Agent(AgentClient(api.base, "ai"))
    agent.bootstrap()
    feed_moving_contact(api, 0, 5, 1500, 800, 0, 0)
    api.tick = 4
    agent.step()
    assert api.points
    api.tick = 100
    agent.step()
    assert api.points == {}


def test_bootstrap_clears_stale_agent_points_but_keeps_foreign_ones(api):
    api.points["trk-old"] = {"id": "trk-old", "kind": "Tracking", "confidence": "Estimated",
                             "x": 1, "y": 1, "radius": 1, "label": "", "path": []}
    api.points["operator-note"] = {"id": "operator-note", "kind": "Surveillance", "confidence": "Estimated",
                                   "x": 1, "y": 1, "radius": 1, "label": "", "path": []}
    Agent(AgentClient(api.base, "ai")).bootstrap()
    assert set(api.points) == {"operator-note"}


def test_agent_recovers_when_the_server_restarts(api):
    agent = Agent(AgentClient(api.base, "ai"))
    agent.bootstrap()
    feed_moving_contact(api, 0, 5, 1500, 800, 0, 0)
    api.tick = 4
    agent.step()
    api.observations.clear()
    api.sequence = 0
    api.points.clear()
    api.tick = 0
    feed_moving_contact(api, 0, 5, 1500, 800, 0, 0)
    agent.step()
    assert agent.cursor == api.sequence
    assert "trk-T1" in api.points


def test_wrong_key_is_rejected(api):
    with pytest.raises(ApiError) as error:
        AgentClient(api.base, "wrong").assets()
    assert error.value.status == 401


def test_agent_client_has_no_operator_capabilities():
    for name in ("step", "pause", "resume", "status", "snapshot"):
        assert not hasattr(AgentClient, name)


def test_agent_only_calls_ai_channel_endpoints(api):
    agent = Agent(AgentClient(api.base, "ai"))
    agent.bootstrap()
    feed_moving_contact(api, 0, 5, 1500, 800, 0, 0)
    api.tick = 4
    agent.step()
    for _, path in api.requests:
        assert path.startswith("/api/ai/") or path.startswith("/api/observations")


def test_agent_code_never_references_the_operator_channel():
    forbidden = ("operator_client", "OperatorClient", "ground-truth", "/api/simulation")
    for folder in ("agents", "central_intelligence"):
        for source in (ML_ROOT / folder).rglob("*.py"):
            text = source.read_text(encoding="utf-8")
            for token in forbidden:
                assert token not in text, f"{token} found in {source}"
