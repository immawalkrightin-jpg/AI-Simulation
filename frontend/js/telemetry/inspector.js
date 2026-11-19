import { el, rows, fixed } from "./dom.js";

export class Inspector {
  constructor(root) {
    this.root = root;
  }

  hide() {
    this.root.hidden = true;
  }

  show(snapshot, selection) {
    const found = this.find(snapshot, selection);
    if (!found) {
      this.hide();
      return;
    }
    const list = el("dl", "rows");
    rows(list, found.fields);
    this.root.replaceChildren(el("h1", "", found.title), list);
    this.root.hidden = false;
  }

  find(snapshot, selection) {
    if (selection.type === "unit") {
      const u = snapshot.units.find((item) => item.id === selection.id);
      if (!u) {
        return null;
      }
      const destination = u.destinationX == null ? "none" : `${fixed(u.destinationX)}, ${fixed(u.destinationY)}`;
      return {
        title: `${u.owner} ${u.kind} ${u.id}`,
        fields: [
          ["Status", u.status],
          ["Position", `${fixed(u.x)}, ${fixed(u.y)}`],
          ["Destination", destination],
          ["Speed", fixed(u.speed, 2)],
          ["Strength", fixed(u.strength)]
        ]
      };
    }
    if (selection.type === "sensor") {
      const s = snapshot.sensors.find((item) => item.id === selection.id);
      return s
        ? {
            title: `${s.owner} ${s.kind} ${s.id}`,
            fields: [
              ["Position", `${fixed(s.x)}, ${fixed(s.y)}`],
              ["Range", fixed(s.range)]
            ]
          }
        : null;
    }
    const c = snapshot.countries.find((item) => item.id === selection.id);
    return c
      ? {
          title: c.name,
          fields: [
            ["Capital", `${fixed(c.x)}, ${fixed(c.y)}`],
            ["Supplies", fixed(c.supplies, 0)],
            ["Ammunition", fixed(c.ammunition, 0)]
          ]
        }
      : null;
  }
}
