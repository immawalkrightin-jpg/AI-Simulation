from __future__ import annotations

import re

OPERATION_PATTERN = re.compile(r"^\s*operating\s+([A-Za-z][A-Za-z0-9_-]{0,39})\s+(\d{1,9})\s*$")


def parse_operation(text: str) -> tuple[str, int]:
    match = OPERATION_PATTERN.match(text)
    if match is None:
        raise ValueError("expected: operating [name] [number]")
    return match.group(1), int(match.group(2))
