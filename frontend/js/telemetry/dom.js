export function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) {
    node.className = className;
  }
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}

export function rows(list, pairs) {
  const nodes = [];
  for (const [label, value] of pairs) {
    nodes.push(el("dt", "", label), el("dd", "", String(value)));
  }
  list.replaceChildren(...nodes);
}

export function fixed(value, digits = 1) {
  return Number(value).toFixed(digits);
}
