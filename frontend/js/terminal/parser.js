const OPERATION = /^operating\s+([A-Za-z][A-Za-z0-9_-]{0,39})\s+(\d{1,9})(?:\s+(.*))?$/;

export function parseOperation(line) {
  const match = OPERATION.exec(line.trim());
  if (!match) {
    return null;
  }
  return { name: match[1], number: Number(match[2]), rest: (match[3] ?? "").trim() };
}

export function resolveCategory(text, categories) {
  const value = text.trim().toLowerCase().replace(/[\s-]+/g, "_");
  if (!value) {
    return null;
  }
  const exact = categories.find((category) => category === value);
  if (exact) {
    return exact;
  }
  if (/^\d+$/.test(value)) {
    const index = Number(value);
    return index >= 1 && index <= categories.length ? categories[index - 1] : null;
  }
  const matches = categories.filter((category) => category.startsWith(value));
  return matches.length === 1 ? matches[0] : null;
}

export function splitReport(rest, categories) {
  const text = rest.trim();
  const token = text.split(/\s+/)[0] ?? "";
  return {
    token,
    category: resolveCategory(token, categories),
    description: text.slice(token.length).trim()
  };
}
