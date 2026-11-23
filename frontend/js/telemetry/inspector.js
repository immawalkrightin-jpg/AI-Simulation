import { el, rows, fixed } from "./dom.js";
import { kindLabel } from "./describe.js";

// Detail panel for a selected unit or sensor. Countries are shown by the dossier instead.
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
        title: `${kindLabel(u.kind)} ${u.owner}-${u.id}`,
        fields: [
          ["Owner", u.owner],
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
            title: `${s.kind} ${s.owner}-${s.id}`,
            fields: [
              ["Owner", s.owner],
              ["Position", `${fixed(s.x)}, ${fixed(s.y)}`],
              ["Range", fixed(s.range)]
            ]
          }
        : null;
    }
    return null;
  }
}
