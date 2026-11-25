import test from "node:test";
import assert from "node:assert/strict";
import { blocLabel, carrierLabel, chainText, dossierFor, frontsFor, kindLabel, legText, percent, powerFor, rungLabel, routeText, tooltipFor } from "../js/telemetry/describe.js";

const snapshot = {
  countries: [
    { id: "USSR", name: "Soviet Union", bloc: "East", isModel: true, supplies: 800, ammunition: 600, power: 130.5 },
    { id: "POL", name: "Poland", bloc: "East", isModel: false, supplies: 300, ammunition: 200, power: 42.3 },
    { id: "USA", name: "United States", bloc: "West", isModel: false, supplies: 900, ammunition: 700, power: 140 }
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
  assert.equal(kindLabel("Armor"), "Tank");
  assert.equal(kindLabel("SupplyConvoy"), "Truck");
  assert.equal(kindLabel("Air"), "Warplane");
  assert.equal(kindLabel("Naval"), "Warship");
  assert.equal(kindLabel("ArmoredCar"), "Armored car");
  assert.equal(kindLabel("Warplane"), "Warplane");
  assert.equal(kindLabel("Helicopter"), "Helicopter");
  assert.equal(kindLabel("CargoShip"), "Cargo ship");
  assert.equal(kindLabel("CargoPlane"), "Cargo plane");
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
  assert.equal(tip.title, "Tank USSR-2");
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
  assert.deepEqual(tip.rows.find((r) => r[0] === "Tank"), ["Tank", "2"]);
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
    [["Infantry", 1, 1, 0, 0], ["Tank", 2, 1, 1, 1]]
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

test("power: value and rank among the countries that report power", () => {
  assert.deepEqual(powerFor(snapshot, "USA"), { value: 140, rank: 1, of: 3 });
  assert.deepEqual(powerFor(snapshot, "USSR"), { value: 130.5, rank: 2, of: 3 });
  assert.deepEqual(powerFor(snapshot, "POL"), { value: 42.3, rank: 3, of: 3 });
  assert.equal(dossierFor(snapshot, "USSR", new Map()).power.rank, 2);
});

test("power: an engine that does not report it gives null, never a guess", () => {
  const bare = { ...snapshot, countries: snapshot.countries.map(({ power, ...rest }) => rest) };
  assert.equal(powerFor(bare, "USSR"), null);
  assert.equal(dossierFor(bare, "USSR", new Map()).power, null);
  assert.equal(powerFor(snapshot, "XXX"), null);
});

const routed = {
  ...snapshot,
  tick: 150,
  interactions: [
    {
      id: 8,
      kind: "SupplyShipment",
      source: "USSR",
      target: "POL",
      mode: "Naval",
      status: "Active",
      cargo: 50,
      subject: "supplies",
      startTick: 100,
      dueTick: 200,
      endTick: null,
      carrier: "CargoShip",
      route: {
        exposure: 0,
        legs: [
          { carrier: "Truck", mode: "Ground", start: 0, end: 0.2, points: [0, 0, 10, 0] },
          { carrier: "CargoShip", mode: "Naval", start: 0.2, end: 0.8, points: [10, 0, 40, 0] },
          { carrier: "CargoPlane", mode: "Air", start: 0.8, end: 1, points: [40, 0, 60, 0] }
        ]
      }
    }
  ]
};

test("routes: the chain names every carrier in order", () => {
  assert.equal(chainText(routed.interactions[0].route.legs), "Truck > Cargo ship > Cargo plane");
  assert.equal(carrierLabel("CargoPlane"), "Cargo plane");
});

test("routes: the leg follows the tick, and freezes when the shipment has ended", () => {
  assert.equal(legText(routed, routed.interactions[0]), "2 of 3");
  assert.equal(legText({ ...routed, tick: 105 }, routed.interactions[0]), "1 of 3");
  assert.equal(legText({ ...routed, tick: 300 }, routed.interactions[0]), "3 of 3");
  assert.equal(legText({ ...routed, tick: 300 }, { ...routed.interactions[0], endTick: 150 }), "2 of 3");
  assert.equal(legText(snapshot, snapshot.interactions[0]), null);
});

test("routes: the shipment tooltip adds the leg and the chain", () => {
  const tip = tooltipFor(routed, { type: "shipment", id: 8, carrier: "CargoShip" });
  assert.deepEqual(tip.rows, [
    ["Carrier", "Cargo ship"],
    ["Leg", "2 of 3"],
    ["Chain", "Truck > Cargo ship > Cargo plane"],
    ["Mode", "Naval"],
    ["Cargo", "50"],
    ["Route", "Soviet Union to Poland"],
    ["Status", "Active"]
  ]);
});

test("routes: the dossier shows which leg a shipment is on", () => {
  const dossier = dossierFor(routed, "USSR", new Map());
  assert.equal(dossier.operations[0].detail, "Cargo ship 50, leg 2 of 3");
});

const political = {
  ...snapshot,
  fronts: [
    { name: "Korean peninsula", first: "PRK", second: "KOR", rung: "Incident", ceiling: "Missile", reason: "x" },
    { name: "Indochina front", first: "USA", second: "VNM", rung: "Clash", ceiling: "Missile", reason: "y" },
    { name: "Caribbean standoff", first: "USA", second: "CUB", rung: "Tension", ceiling: "Incident", reason: "z" }
  ],
  interactions: [
    { ...snapshot.interactions[0], cause: "Moscow props up its client", hunter: "TUR" }
  ]
};

test("politics: rungs read as words and fronts are listed for a country, worst first", () => {
  assert.equal(rungLabel("Incident"), "Incident");
  assert.deepEqual(
    frontsFor(political, "USA").map((f) => [f.name, f.other, f.rung, f.level]),
    [["Indochina front", "VNM", "Clash", 3], ["Caribbean standoff", "CUB", "Tension", 1]]
  );
  assert.deepEqual(frontsFor(political, "USSR"), []);
  assert.equal(dossierFor(political, "USA", new Map()).fronts.length, 2);
});

test("politics: an engine without fronts gives an empty list", () => {
  assert.deepEqual(frontsFor(snapshot, "USA"), []);
  assert.deepEqual(dossierFor(snapshot, "USA", new Map()).fronts, []);
});

test("politics: the shipment tooltip adds the cause and the hunter only when they exist", () => {
  const tip = tooltipFor(political, { type: "shipment", id: 5, carrier: "CargoShip" });
  assert.deepEqual(tip.rows.slice(-2), [["Cause", "Moscow props up its client"], ["Hunted by", "TUR"]]);
  const plain = tooltipFor(snapshot, { type: "shipment", id: 5, carrier: "CargoShip" });
  assert.equal(plain.rows.some((r) => r[0] === "Cause" || r[0] === "Hunted by"), false);
  assert.equal(dossierFor(political, "USSR", new Map()).operations[0].detail, "Cargo ship 64, hunted by TUR");
});
