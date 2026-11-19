import re

from ml.agents.intelligence.tracker import Track
from ml.central_intelligence.intelligence import CentralIntelligence, Priority

ASSETS = {
    "tick": 50, "width": 2000.0, "height": 1000.0,
    "sensors": [{"id": 1, "kind": "Radar", "x": 500.0, "y": 500.0, "range": 400.0}],
}
KINDS = {"ExpectedActivity", "SuspectedTarget", "Surveillance", "ArtilleryEngagement",
         "SupplyRoute", "IntelligenceRequired", "Tracking"}
CONFIDENCES = {"Confirmed", "Estimated", "Suspected", "Predicted"}


def track(track_id="T1", x=500.0, y=500.0, vx=0.0, vy=0.0, hits=5, sigma=3.0, kind="Armor"):
    return Track(
        id=track_id, x=x, y=y, sigma=sigma, first_tick=0, last_tick=50, vx=vx, vy=vy,
        hits=hits, latest_confidence="Estimated", kind_votes={kind: 1} if kind else {})


def test_single_hit_contact_is_only_worth_knowing():
    ci = CentralIntelligence()
    assert ci.assess(track(hits=1, x=1500, y=800), ASSETS, 50).priority == Priority.KNOW
    assert ci.decide([track(hits=1, x=1500, y=800)], ASSETS, 50) == []


def test_persistent_distant_contact_is_monitored_with_a_tracking_point():
    ci = CentralIntelligence()
    actions = ci.decide([track(x=1500, y=800)], ASSETS, 50)
    assert ci.assessments["T1"].priority == Priority.MONITOR
    assert [a.point_id for a in actions] == ["trk-T1"]
    assert actions[0].kind == "Tracking"


def test_contact_closing_on_an_asset_gets_a_prediction():
    ci = CentralIntelligence()
    actions = ci.decide([track(x=500, y=650, vx=0, vy=-5)], ASSETS, 50)
    assert ci.assessments["T1"].priority == Priority.ASSESS
    by_id = {a.point_id: a for a in actions}
    assert set(by_id) == {"trk-T1", "prd-T1"}
    assert by_id["prd-T1"].kind == "ExpectedActivity"
    assert by_id["prd-T1"].confidence == "Predicted"
    assert by_id["prd-T1"].y < by_id["trk-T1"].y


def test_contact_inside_immediate_range_becomes_a_suspected_target():
    ci = CentralIntelligence()
    actions = ci.decide([track(x=520, y=520)], ASSETS, 50)
    assert ci.assessments["T1"].priority == Priority.IMMEDIATE
    assert {"trk-T1", "tgt-T1"} <= {a.point_id for a in actions}
    assert next(a for a in actions if a.point_id == "tgt-T1").kind == "SuspectedTarget"


def test_unchanged_picture_produces_no_repeat_actions():
    ci = CentralIntelligence()
    contact = track(x=1500, y=800)
    assert ci.decide([contact], ASSETS, 50)
    assert ci.decide([contact], ASSETS, 50) == []


def test_lost_track_removes_its_points():
    ci = CentralIntelligence()
    ci.decide([track(x=520, y=520)], ASSETS, 50)
    actions = ci.decide([], ASSETS, 51)
    assert {a.point_id for a in actions} == {"trk-T1", "tgt-T1"}
    assert all(a.op == "delete" for a in actions)
    assert ci.placed == {}


def test_significant_movement_updates_the_point():
    ci = CentralIntelligence()
    ci.decide([track(x=1500, y=800)], ASSETS, 50)
    actions = ci.decide([track(x=1530, y=800)], ASSETS, 50)
    assert [a.op for a in actions] == ["put"]


def test_rejected_points_are_retried():
    ci = CentralIntelligence()
    ci.decide([track(x=1500, y=800)], ASSETS, 50)
    ci.rejected("trk-T1")
    assert [a.point_id for a in ci.decide([track(x=1500, y=800)], ASSETS, 50)] == ["trk-T1"]


def test_every_action_is_valid_for_the_api():
    ci = CentralIntelligence()
    tracks = [
        track("T1", x=-40, y=1200, vx=-9, vy=9),
        track("T2", x=1999, y=999, vx=9, vy=9),
        track("T3", x=510, y=505),
        track("T4", x=900, y=500, vx=-4, vy=0, sigma=900, kind=None),
    ]
    for action in ci.decide(tracks, ASSETS, 50):
        assert re.fullmatch(r"[A-Za-z0-9_-]{1,40}", action.point_id)
        assert action.kind in KINDS
        assert action.confidence in CONFIDENCES
        assert 0 <= action.x <= 2000 and 0 <= action.y <= 1000
        assert 0 <= action.radius <= 500
        assert len(action.label) <= 80
        assert len(action.path) % 2 == 0
        for i in range(0, len(action.path), 2):
            assert 0 <= action.path[i] <= 2000 and 0 <= action.path[i + 1] <= 1000


def test_priority_ordering_matches_urgency():
    assert Priority.KNOW < Priority.MONITOR < Priority.ASSESS < Priority.IMMEDIATE
