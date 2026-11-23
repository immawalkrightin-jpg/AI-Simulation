import test from "node:test";
import assert from "node:assert/strict";
import { blocLabel, carrierLabel, dossierFor, kindLabel, percent, routeText, tooltipFor } from "../js/telemetry/describe.js";

const snapshot = {
  countries: [
    { id: "USSR", name: "Soviet Union", bloc: "East", isModel: true, supplies: 800, ammunition: 600 },
    { id: "POL", name: "Poland", bloc: "East", isModel: false, supplies: 300, ammunition: 200 },
    { id: "USA", name: "United States", bloc: "West", isModel: false, supplies: 900, ammunition: 700 }
  ],
  units: [
    { id: 1, owner: "USSR", kind: "Infantry", status: "Idle", strength: 80 },
    { id: 2, owner: "USSR", kind: "Armor", status: "Moving", strength: 100 },
    { id: 3, owner: "USSR", kind: "Armor", status: "Destroyed", strength: 0 },
    { id: 4, owner: "POL", kind: "Air", status: "Idle", strength: 60 }
  ],
  sensors: [{ id: 9, kind: "Radar", owner: "USSR", range: 42.4 }],
  relations: [
    { first: "USSR", second: "POL", value: 0.6, state: "Allied" },
    { first: "USSR", second: "USA", value: -0.75, state: "Hostile" },
    { first: "POL", second: "USA", value: -0.4, state: "Tense" }
  ],
  interactions: [
    { id: 5, kind: "SupplyShipment", source: "USSR", target: "POL", mode: "Naval", status: "Active", cargo: 64.4, subject: "supplies", endTick: null, carrier: "CargoShip" },
    { id: 6, kind: "Clash", source: "USA", target: "USSR", mode: "None", status: "Active", cargo: 0, subject: "x", endTick: null },
    { id: 7, kind: "Negotiation", source: "POL", target: "USA", mode: "None", status: "Succeeded", cargo: 0, subject: "trade quotas", endTick: 40 }
  ]
};

test("labels: kinds, carriers and blocs read as words", () => {
  assert.equal(kindLabel("Armor"), "Armor");
  assert.equal(kindLabel("SupplyConvoy"), "Supply convoy");
  assert.equal(kindLabel("Air"), "Air unit");
  assert.equal(kindLabel("Zeppelin"), "Unknown unit");
  assert.equal(carrierLabel("CargoShip"), "Cargo ship");
  assert.equal(blocLabel("NonAligned"), "Non-aligned");
  assert.equal(percent(30, 120), "25%");
  assert.equal(percent(5, 0), "n/a");
});

test("routeText uses country names", () => {
  assert.equal(routeText(snapshot, snapshot.interactions[0]), "Soviet Union to Poland");
});

test("tooltip: unit", () => {
  const tip = tooltipFor(snapshot, { type: "unit", id: 2, max: 100 });
  assert.equal(tip.title, "Armor USSR-2");
  assert.deepEqual(tip.rows, [["Owner", "USSR"], ["Status", "Moving"], ["Strength", "100 (100%)"]]);
});

test("tooltip: sensor", () => {
  const tip = tooltipFor(snapshot, { type: "sensor", id: 9 });
  assert.equal(tip.title, "Radar USSR-9");
  assert.deepEqual(tip.rows, [["Owner", "USSR"], ["Range", "42"]]);
});

test("tooltip: shipment shows carrier, mode, cargo, route and status", () => {
  const tip = tooltipFor(snapshot, { type: "shipment", id: 5, carrier: "CargoShip" });
  assert.deepEqual(tip.rows, [
    ["Carrier", "Cargo ship"],
    ["Mode", "Naval"],
    ["Cargo", "64"],
    ["Route", "Soviet Union to Poland"],
    ["Status", "Active"]
  ]);
});

test("tooltip: shipment without any carrier says unknown, never a guess", () => {
  const stripped = { ...snapshot, interactions: [{ ...snapshot.interactions[0], carrier: undefined }] };
  const tip = tooltipFor(stripped, { type: "shipment", id: 5 });
  assert.equal(tip.rows[0][1], "unknown");
});

test("tooltip: cluster counts its members by kind", () => {
  const tip = tooltipFor(snapshot, { type: "cluster", id: "c1", ids: [1, 2, 3] });
  assert.equal(tip.title, "3 units");
  assert.deepEqual(tip.rows.find((r) => r[0] === "Armor"), ["Armor", "2"]);
  assert.deepEqual(tip.rows.find((r) => r[0] === "Infantry"), ["Infantry", "1"]);
});

test("tooltip: country, and nothing for unknown or missing targets", () => {
  assert.equal(tooltipFor(snapshot, { type: "country", id: "POL" }).title, "Poland");
  assert.equal(tooltipFor(snapshot, { type: "unit", id: 999 }), null);
  assert.equal(tooltipFor(snapshot, null), null);
  assert.equal(tooltipFor(null, { type: "unit", id: 1 }), null);
});

test("dossier: labelled bars, grouped units, operations and relations", () => {
  const peaks = new Map();
  const dossier = dossierFor(snapshot, "USSR", peaks);
  assert.equal(dossier.name, "Soviet Union");
  assert.equal(dossier.bloc, "East bloc");
  assert.equal(dossier.supplies.value, 800);
  assert.equal(dossier.supplies.ratio, 1);
  assert.deepEqual(
    dossier.units.map((u) => [u.label, u.total, u.active, u.moving, u.destroyed]),
    [["Armor", 2, 1, 1, 1], ["Infantry", 1, 1, 0, 0]]
  );
  assert.equal(dossier.operations.length, 2);
  const shipment = dossier.operations.find((o) => o.kind === "Supply shipment");
  assert.equal(shipment.direction, "out");
  assert.equal(shipment.detail, "Cargo ship 64");
  assert.equal(dossier.operations.find((o) => o.kind === "Clash").hostile, true);
  assert.deepEqual(dossier.hostile.map((r) => r.other), ["USA"]);
  assert.deepEqual(dossier.closest.map((r) => r.other), ["POL"]);
});

test("dossier: bars are relative to the running peak", () => {
  const peaks = new Map();
  dossierFor(snapshot, "USSR", peaks);
  const lower = { ...snapshot, countries: snapshot.countries.map((c) => (c.id === "USSR" ? { ...c, supplies: 400 } : c)) };
  assert.equal(dossierFor(lower, "USSR", peaks).supplies.ratio, 0.5);
});

test("dossier: unknown country", () => {
  assert.equal(dossierFor(snapshot, "XXX", new Map()), null);
});
