# Progress notes

Covers everything added since Milestone 7. Verification status is stated per item.

## Milestone 8: simulation reset (pushed, tests passed)
- `POST /api/simulation/reset?seed=N` (ground-truth key). Missing seed keeps the current one, negative seed returns 400.
- `SimulationSession` keeps the scenario factory and seed. `SimulationBroadcaster` serializes steps and resets, clears the observation buffer and the AI operational map, and pushes snapshot, `reset` and points events.
- `SimulationStatus` gains an optional `seed`.
- Tests: `ResetTests.cs`.

## Milestone 9: training environment (pushed, tests passed)
- `ml/environment/simulation_env.py`: `SimulationEnv` (reset pauses first, chunked advance, horizon clamp).
- `ml/training/episode.py`: `run_episode`.
- `OperatorClient.reset`. `FakeApi` now fakes the operator endpoints.
- No reward yet. The environment reads no ground truth.

## Milestone 10: model registry (pushed, tests passed)
- `ml/registry/`: versions, training runs, evaluations, human corrections, version comparison, weight hashing.
- Records live in `ml/experiments/registry/` (committed). Weights stay in `ml/models/` (ignored).
- Records are never overwritten. Corrections never modify existing model records.

## Milestone 11: feedback backend and importer (written, NOT yet verified)
- C#: `FeedbackStore`, `FeedbackController`, `FeedbackContracts`. Routes `/api/feedback/{categories,corrections}` require the ground-truth key (AI key is rejected). Reports are appended to `data/feedback/corrections.jsonl`.
- `SimulationOptions.FeedbackPath` overrides the inbox location. `ApiFactory` now points it at a temp folder.
- C# tests: `FeedbackTests.cs`.
- Python: `HumanCorrection` gains `source_id` and `context`. `ml/feedback/` imports the inbox into the registry once per report (`python -m ml.feedback`).
- Python tests: `test_feedback.py`, including a check that the category list matches the C# list.
- `.gitignore`: `data/feedback/*` ignored.

## Milestone 12: terminal UI (written, needs a manual browser check)
- `frontend/js/terminal/`: `parser.js` (parser logic checked with Node), `feedback.js` (API link), `terminal.js` (report dialog, history, recent reports). Wired into `index.html` (HUD button and a panel in the right column), `style.css` and `main.js`. Toggle with the Terminal button or the backtick key.
- The frontend was reworked while this was in progress (HUD, dossier, camera). The terminal was fitted to the current layout.
- One-line reports should use underscores or a number for the category: `operating alpha 12 friendly_fire note` or `operating alpha 12 3 note`. A spaced `friendly fire` splits wrongly.
- Report convention: `operating [name] [number]`, then category and note, or all in one line.

## Milestone 13: ground-truth evaluator (written, NOT yet verified)
- `ml/evaluation/` is the only Python package that reads ground truth. Agent, training, environment, registry and feedback code never mention it, and a test enforces that.
- `scoring.py`: scores the AI's `Tracking` points against real non-destroyed units. Metrics: recall over units its sensors could reach, precision, mean position error, false tracks, missed units, tracks sitting on its own units, and precision of `Confirmed` tracks. Undefined ratios score 1.0.
- `evaluator.py`: `TruthClient` (operator key) and `EpisodeEvaluator` (per-step scores and an episode summary).
- `runner.py`: `evaluate_version` runs a version over a list of seeds and records the averaged metrics in the registry, so versions can be compared.
- `run_episode` takes an optional evaluator and evaluate_every. Summaries gain `metrics`.
- Not covered yet: prediction accuracy (`prd-` points), friendly-fire and casualties, supply outcomes. These need actions the agent does not take yet. There is still no reward function.
- Tests: `ml/tests/test_evaluation.py`.

## Still open
- README and `documentation/ai_agent.md` are not updated.
- Window B has no HTML page and ignores the reset event.
- Ground-truth evaluator (reward and evaluation metrics) is next after the terminal.
- Two xUnit2012 warnings remain in `InteractionEngineTests.cs`.
