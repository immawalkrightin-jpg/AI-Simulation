from __future__ import annotations

import argparse
import logging
import os
import time
import urllib.error

from ml.central_intelligence.agent import Agent
from ml.environment.client import AgentClient, ApiError


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the autonomous intelligence agent.")
    parser.add_argument("--base", default=os.environ.get("SIM_BASE", "http://localhost:5080"))
    parser.add_argument("--key", default=os.environ.get("SIM_AI_KEY", "dev-ai-key"))
    parser.add_argument("--interval", type=float, default=1.0)
    arguments = parser.parse_args()

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    log = logging.getLogger("agent")
    agent = Agent(AgentClient(arguments.base, arguments.key))

    connected = False
    while not connected:
        try:
            agent.bootstrap()
            connected = True
        except (ApiError, urllib.error.URLError, OSError) as error:
            log.warning("waiting for the server: %s", error)
            time.sleep(2)
    log.info("agent online, observing %s", arguments.base)

    try:
        while True:
            try:
                actions = agent.step()
                if actions:
                    puts = sum(1 for a in actions if a.op == "put")
                    log.info(
                        "tick %s: %s tracks, %s points updated, %s removed",
                        agent.assets.get("tick"),
                        len(agent.tracker.tracks),
                        puts,
                        len(actions) - puts,
                    )
            except (ApiError, urllib.error.URLError, OSError) as error:
                log.warning("step failed: %s", error)
            time.sleep(arguments.interval)
    except KeyboardInterrupt:
        log.info("agent stopped")


if __name__ == "__main__":
    main()
