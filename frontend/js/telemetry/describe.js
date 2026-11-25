import { symbolName } from "../units/symbols.js";
import { chainOf, legIndexAt, progressOf, routeLegs } from "../maps/route.js";

const KIND_LABELS = {
  Infantry: "Infantry",
  Tank: "Tank",
  ArmoredCar: "Armored car",
  Artillery: "Artillery",
  Truck: "Truck",
  Warship: "Warship",
  CargoShip: "Cargo ship",
  Fighter: "Warplane",
  Transport: "Cargo plane",
  Helicopter: "Helicopter",
  Unknown: "Unknown unit"
};

const CARRIER_LABELS = { Truck: "Truck", CargoShip: "Cargo ship", Transport: "Transport plane", CargoPlane: "Cargo plane", Helicopter: "Helicopter" };
const BLOC_LABELS = { East: "East bloc", West: "West bloc", NonAligned: "Non-aligned" };
const OPERATION_LABELS = { Negotiation: "Negotiation", SupplyShipment: "Supply shipment", Clash: "Clash", MissileStrike: "Missile strike" };
const RUNG_LABELS = { Calm: "Calm", Tension: "Tension", Incident: "Incident", Clash: "Clash", Missile: "Missile" };
const RUNG_ORDER = ["Calm", "Tension", "Incident", "Clash", "Missile"];

export const LOW_SHARE = 0.3;

export const kindLabel = (kind) => KIND_LABELS[symbolName(kind)] || "Unknown unit";
export const carrierLabel = (carrier) => CARRIER_LABELS[carrier] || carrier;
export const blocLabel = (bloc) => BLOC_LABELS[bloc] || bloc;
export const operationLabel = (kind) => OPERATION_LABELS[kind] || kind;
export const rungLabel = (rung) => RUNG_LABELS[rung] || rung;

export function frontsFor(snapshot, id) {
  return (snapshot.fronts || [])
    .filter((f) => f.first === id || f.second === id)
    .map((f) => ({
      name: f.name,
      other: f.first === id ? f.second : f.first,
      rung: rungLabel(f.rung),
      level: Math.max(0, RUNG_ORDER.indexOf(f.rung)),
      ceiling: rungLabel(f.ceiling)
    }))
    .sort((a, b) => b.level - a.level || a.name.localeCompare(b.name));
}

export function percent(value, max) {
  if (!max || max <= 0) {
    return "n/a";
  }
  return `${Math.round(Math.min(1, Math.max(0, value / max)) * 100)}%`;
}

export function routeText(snapshot, item) {
  const name = (id) => {
    const country = snapshot.countries.find((c) => c.id === id);
    return country ? country.name : id;
  };
  return `${name(item.source)} to ${name(item.target)}`;
}

export function chainText(legs) {
  return chainOf(legs).map(carrierLabel).join(" > ");
}

export function legText(snapshot, item) {
  const legs = routeLegs(item);
  if (!legs) {
    return null;
  }
  return `${legIndexAt(legs, progressOf(item, snapshot.tick)) + 1} of ${legs.length}`;
}

export function tooltipFor(snapshot, hit) {
  if (!snapshot || !hit) {
    return null;
  }
  if (hit.type === "unit") {
    const unit = snapshot.units.find((u) => u.id === hit.id);
    if (!unit) {
      return null;
    }
    return {
      title: `${kindLabel(unit.kind)} ${unit.owner}-${unit.id}`,
      rows: [
        ["Owner", unit.owner],
        ["Status", unit.status],
        ["Strength", `${Math.round(unit.strength)}${hit.max ? ` (${percent(unit.strength, hit.max)})` : ""}`]
      ]
    };
  }
  if (hit.type === "sensor") {
    const sensor = snapshot.sensors.find((s) => s.id === hit.id);
    if (!sensor) {
      return null;
    }
    return {
      title: `${sensor.kind} ${sensor.owner}-${sensor.id}`,
      rows: [
        ["Owner", sensor.owner],
        ["Range", String(Math.round(sensor.range))]
      ]
    };
  }
  if (hit.type === "shipment") {
    const item = snapshot.interactions.find((i) => i.id === hit.id);
    if (!item) {
      return null;
    }
    const legs = routeLegs(item);
    return {
      title: "Supply shipment",
      rows: [
        ["Carrier", carrierLabel(hit.carrier || item.carrier) || "unknown"],
        ...(legs ? [["Leg", legText(snapshot, item)], ["Chain", chainText(legs)]] : []),
        ["Mode", item.mode],
        ["Cargo", String(Math.round(item.cargo))],
        ["Route", routeText(snapshot, item)],
        ["Status", item.status],
        ...(item.cause ? [["Cause", item.cause]] : []),
        ...(item.hunter ? [["Hunted by", item.hunter]] : [])
      ]
    };
  }
  if (hit.type === "cluster") {
    const counts = new Map();
    for (const id of hit.ids) {
      const unit = snapshot.units.find((u) => u.id === id);
      if (unit) {
        const label = kindLabel(unit.kind);
        counts.set(label, (counts.get(label) || 0) + 1);
      }
    }
    return {
      title: `${hit.ids.length} units`,
      rows: [...counts.entries()].map(([label, count]) => [label, String(count)]).concat([["Action", "Click to spread"]])
    };
  }
  if (hit.type === "country") {
    const country = snapshot.countries.find((c) => c.id === hit.id);
    if (!country) {
      return null;
    }
    return { title: country.name, rows: [["Bloc", blocLabel(country.bloc)], ["Action", "Click to open"]] };
  }
  return null;
}

export function powerFor(snapshot, id) {
  const country = snapshot.countries.find((c) => c.id === id);
  if (!country || typeof country.power !== "number") {
    return null;
  }
  const ranked = snapshot.countries
    .filter((c) => typeof c.power === "number")
    .sort((a, b) => b.power - a.power || a.id.localeCompare(b.id));
  return { value: country.power, rank: ranked.findIndex((c) => c.id === id) + 1, of: ranked.length };
}

function shipmentDetail(snapshot, item) {
  const base = `${carrierLabel(item.carrier) || item.mode} ${Math.round(item.cargo)}`;
  const leg = legText(snapshot, item);
  const text = leg ? `${base}, leg ${leg}` : base;
  return item.hunter ? `${text}, hunted by ${item.hunter}` : text;
}

export function dossierFor(snapshot, id, peaks) {
  const country = snapshot.countries.find((c) => c.id === id);
  if (!country) {
    return null;
  }
  const own = snapshot.units.filter((u) => u.owner === id);
  const groups = new Map();
  for (const unit of own) {
    const label = kindLabel(unit.kind);
    const entry = groups.get(label) || { label, total: 0, active: 0, moving: 0, destroyed: 0 };
    entry.total++;
    if (unit.status === "Destroyed") {
      entry.destroyed++;
    } else {
      entry.active++;
      if (unit.status === "Moving") {
        entry.moving++;
      }
    }
    groups.set(label, entry);
  }
  const operations = snapshot.interactions
    .filter((i) => i.endTick == null && (i.source === id || i.target === id))
    .map((i) => ({
      kind: operationLabel(i.kind),
      hostile: i.kind === "Clash" || i.kind === "MissileStrike",
      direction: i.source === id ? "out" : "in",
      other: i.source === id ? i.target : i.source,
      detail: i.kind === "SupplyShipment" ? shipmentDetail(snapshot, i) : i.kind === "Negotiation" ? i.subject : ""
    }));
  const relations = snapshot.relations
    .filter((r) => r.first === id || r.second === id)
    .map((r) => ({ other: r.first === id ? r.second : r.first, value: r.value, state: r.state }))
    .sort((a, b) => a.value - b.value);
  const peak = (key, value) => Math.max(peaks.get(key) || 0, value);
  const suppliesPeak = peak(`${id}:supplies`, country.supplies);
  const ammunitionPeak = peak(`${id}:ammunition`, country.ammunition);
  peaks.set(`${id}:supplies`, suppliesPeak);
  peaks.set(`${id}:ammunition`, ammunitionPeak);
  return {
    id,
    name: country.name,
    bloc: blocLabel(country.bloc),
    isModel: country.isModel,
    power: powerFor(snapshot, id),
    supplies: { value: country.supplies, ratio: suppliesPeak > 0 ? country.supplies / suppliesPeak : 0 },
    ammunition: { value: country.ammunition, ratio: ammunitionPeak > 0 ? country.ammunition / ammunitionPeak : 0 },
    units: [...groups.values()].sort((a, b) => a.label.localeCompare(b.label)),
    operations,
    fronts: frontsFor(snapshot, id),
    hostile: relations.slice(0, 3).filter((r) => r.value < 0),
    closest: relations.slice(-3).reverse().filter((r) => r.value > 0)
  };
}
