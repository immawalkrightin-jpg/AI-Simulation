from __future__ import annotations

from dataclasses import dataclass

from ml.environment.operator_client import OperatorClient

MAX_STEP = 1000


@dataclass(frozen=True)
class EnvState:
    seed: int
    tick: int
    horizon: int

    @property
    def done(self) -> bool:
        return self.tick >= self.horizon


class SimulationEnv:
    def __init__(self, operator: OperatorClient, horizon: int = 1000, ticks_per_step: int = 1):
        if horizon < 1:
            raise ValueError("horizon must be at least 1")
        if ticks_per_step < 1:
            raise ValueError("ticks_per_step must be at least 1")
        self.operator = operator
        self.horizon = horizon
        self.ticks_per_step = ticks_per_step
        self.seed: int | None = None
        self.tick = 0

    def reset(self, seed: int | None = None) -> EnvState:
        self.operator.pause()
        status = self.operator.reset(seed)
        self.seed = int(status["seed"])
        self.tick = int(status["tick"])
        return self.state()

    def advance(self, ticks: int | None = None) -> EnvState:
        if self.seed is None:
            raise RuntimeError("reset must be called before advance")
        requested = self.ticks_per_step if ticks is None else ticks
        if requested < 1:
            raise ValueError("ticks must be at least 1")
        remaining = min(requested, self.horizon - self.tick)
        while remaining > 0:
            chunk = min(remaining, MAX_STEP)
            status = self.operator.step(chunk)
            self.tick = int(status["tick"])
            remaining -= chunk
        return self.state()

    def state(self) -> EnvState:
        if self.seed is None:
            raise RuntimeError("reset must be called before reading state")
        return EnvState(self.seed, self.tick, self.horizon)
