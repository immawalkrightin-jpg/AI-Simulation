from __future__ import annotations

from dataclasses import dataclass

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


def run_episode(env: SimulationEnv, agent: Agent, seed: int | None = None) -> EpisodeSummary:
    state = env.reset(seed)
    agent.reset()
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
    points = len(agent.client.points().get("points", []))
    return EpisodeSummary(
        seed=state.seed,
        ticks=state.tick,
        agent_steps=steps,
        point_updates=updates,
        point_removals=removals,
        tracks=len(agent.tracker.tracks),
        points=points,
    )
