from __future__ import annotations

from dataclasses import dataclass, field

from ml.central_intelligence.agent import Agent
from ml.environment.simulation_env import SimulationEnv


@dataclass(frozen=True)
class EpisodeSummary:
    seed: int
    ticks: int
    agent_steps: int
    point_updates: int
    point_removals: int
    tracks: int
    points: int
    metrics: dict = field(default_factory=dict)


def run_episode(
    env: SimulationEnv,
    agent: Agent,
    seed: int | None = None,
    evaluator=None,
    evaluate_every: int = 1,
) -> EpisodeSummary:
    if evaluate_every < 1:
        raise ValueError("evaluate_every must be at least 1")
    state = env.reset(seed)
    agent.reset()
    if evaluator is not None:
        evaluator.reset()
    steps = 0
    updates = 0
    removals = 0
    while not state.done:
        state = env.advance()
        actions = agent.step()
        steps += 1
        for action in actions:
            if action.op == "put":
                updates += 1
            else:
                removals += 1
        if evaluator is not None and steps % evaluate_every == 0:
            evaluator.observe(agent.client.points().get("points", []))
    final_points = agent.client.points().get("points", [])
    metrics = {}
    if evaluator is not None:
        if steps % evaluate_every != 0:
            evaluator.observe(final_points)
        metrics = evaluator.summary()
    return EpisodeSummary(
        seed=state.seed,
        ticks=state.tick,
        agent_steps=steps,
        point_updates=updates,
        point_removals=removals,
        tracks=len(agent.tracker.tracks),
        points=len(final_points),
        metrics=metrics,
    )
