from ml.agents.intelligence.tracker import Tracker


def obs(sequence, tick, x, y, sigma=3.0, confidence="Estimated", kind=None, owner=None, source="Radar"):
    return {
        "sequence": sequence, "sensorId": 1, "source": source, "observedTick": tick,
        "deliveryTick": tick, "x": x, "y": y, "positionSigma": sigma,
        "confidence": confidence, "classifiedKind": kind, "classifiedOwner": owner}


def test_consecutive_detections_form_one_track_with_velocity():
    tracker = Tracker()
    for tick in range(10):
        tracker.update([obs(tick + 1, tick, 100 + 4 * tick, 200)], tick)
    assert len(tracker.tracks) == 1
    track = next(iter(tracker.tracks.values()))
    assert track.hits == 10
    assert 2.0 < track.vx < 6.0
    assert abs(track.vy) < 1.0


def test_distant_detections_form_separate_tracks():
    tracker = Tracker()
    tracker.update([obs(1, 0, 100, 100), obs(2, 0, 900, 700)], 0)
    assert len(tracker.tracks) == 2


def test_simultaneous_detections_from_two_sensors_merge():
    tracker = Tracker()
    tracker.update([obs(1, 5, 300, 300, sigma=8), obs(2, 5, 303, 298, sigma=2, source="Optical")], 5)
    assert len(tracker.tracks) == 1
    track = next(iter(tracker.tracks.values()))
    assert track.sigma < 2.0
    assert track.sources == {"Radar", "Optical"}


def test_fusing_shrinks_uncertainty():
    tracker = Tracker()
    tracker.update([obs(1, 0, 100, 100, sigma=10)], 0)
    first = next(iter(tracker.tracks.values())).sigma
    tracker.update([obs(2, 1, 100, 100, sigma=10)], 1)
    assert next(iter(tracker.tracks.values())).sigma < first


def test_stale_tracks_are_dropped():
    tracker = Tracker(max_age=10)
    tracker.update([obs(1, 0, 100, 100)], 0)
    assert tracker.update([], 5) == []
    dropped = tracker.update([], 11)
    assert dropped == ["T1"]
    assert not tracker.tracks


def test_classification_uses_majority_vote():
    tracker = Tracker()
    tracker.update([obs(1, 0, 100, 100, kind="Armor"), obs(2, 1, 100, 100, kind="Armor"),
                    obs(3, 2, 100, 100, kind="Air")], 2)
    track = next(iter(tracker.tracks.values()))
    assert track.kind == "Armor"


def test_confidence_reflects_evidence():
    tracker = Tracker()
    tracker.update([obs(1, 0, 100, 100)], 0)
    track = next(iter(tracker.tracks.values()))
    assert track.confidence == "Suspected"
    tracker.update([obs(2, 1, 100, 100)], 1)
    assert track.confidence == "Estimated"
    tracker.update([obs(3, 2, 100, 100, confidence="Confirmed", source="Optical")], 2)
    assert track.confidence == "Confirmed"


def test_out_of_order_observation_does_not_break_the_track():
    tracker = Tracker()
    tracker.update([obs(2, 5, 120, 100)], 5)
    tracker.update([obs(1, 3, 112, 100)], 5)
    assert len(tracker.tracks) == 1


def test_reset_clears_everything():
    tracker = Tracker()
    tracker.update([obs(1, 0, 100, 100)], 0)
    tracker.reset()
    tracker.update([obs(2, 0, 100, 100)], 0)
    assert list(tracker.tracks) == ["T1"]
