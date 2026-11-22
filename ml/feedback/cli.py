from __future__ import annotations

import argparse

from ml.feedback.inbox import DEFAULT_INBOX, import_inbox
from ml.registry.store import DEFAULT_ROOT, Registry


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m ml.feedback",
        description="Import operator corrections from the terminal inbox into the model registry.",
    )
    parser.add_argument("--inbox", default=str(DEFAULT_INBOX))
    parser.add_argument("--registry", default=str(DEFAULT_ROOT))
    parser.add_argument("--model-version", default=None)
    arguments = parser.parse_args(argv)

    registry = Registry(arguments.registry)
    try:
        result = import_inbox(registry, arguments.inbox, arguments.model_version)
    except (KeyError, ValueError):
        print(f"unknown model version: {arguments.model_version}")
        return 2
    print(f"imported {len(result.imported)}, already imported {result.already_imported}")
    for source_id, reason in result.rejected:
        print(f"rejected {source_id}: {reason}")
    if result.malformed:
        print(f"skipped {result.malformed} malformed line(s)")
    return 1 if result.rejected or result.malformed else 0
