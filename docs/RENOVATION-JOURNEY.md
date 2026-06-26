# The renovation client journey (design once, walk it anywhere)

How we help a client who wants to renovate, stage, or extend — from a blueprint
(however they get one) to standing in the finished space in AR. This is the
Microsoft-Layout idea, made consumer, cross-platform, and renovation-aware.

## The journey

```
1. ACQUIRE a blueprint   →  2. DESIGN it (phone/desktop)  →  3. WALK it in AR
   (any source)              empty-staged / renovated /        Galaxy XR / Vision
                             extended, with live cost          Pro — auto-staged
```

### 1. Acquire a blueprint — any source, one pipeline

A `RenovationProject` records where the plan came from (`BlueprintOrigin`); all
roads lead to the same editable `BuildingModel`:

- **Scan a room with the phone** (CubiCasa-style) → floor plan with measurements.
- **Upload a CubiCasa floor plan**, or **integrate CubiCasa** (their scan-to-plan).
- **Matterport** model → seed rooms from its dimensions (it gives read-only room
  sizes, not editable walls — we extrude from those).
- **Apple RoomPlan** capture (parametric walls/doors/windows).
- **Draw it on the desktop** authoring tool.

(Importers: `Acquisition.CubiCasaImporter` / `MatterportImporter` over Edge
Functions; `Renovation.IBuildingModelImporter` is the shared contract.)

### 2. Design it — on the phone or computer

Calm, precise, no headset required:

- **Empty-room staging**: drop the client's own scanned furniture *or* vendor
  catalog items.
- **Renovation**: remove/move walls, change finishes (paint by the gallon,
  flooring by the box), with **live cost** and **ROI vs. comps**.
- **Extension / new build**: add structure; place the footprint on the lot vs.
  **property lines** and **sun path**; pull the 3D up out of the plan.

The design is saved to the project (a `StagingLayout` + a `RenovationPlan`),
authored in blueprint coordinates.

### 3. Walk it in AR — and it's already staged

Open the AR app on **Galaxy XR** or **Vision Pro**, register the plan on site
(two known points, no markers — `BlueprintTransform`), and the space is **already
staged**: `AutoStager` applies the renovation edits and converts the design into
world-space furniture exactly where it sits. Toggle **before/after**, "remove a
wall" to see through to the scanned adjacent room, and everyone in the session
shares the same anchored scene.

## What we took from Microsoft Layout — and how we go further

Microsoft Layout (HoloLens) nailed one thing: **import a plan on a PC, place it
1:1 on site, walk it.** It was enterprise-only, HoloLens-only, and is
discontinued. We keep the magic and fix the limits:

| Microsoft Layout | SuperRealEstate |
|---|---|
| HoloLens only (discontinued) | **Android XR + Vision Pro + phone** — one codebase, consumer + pro |
| Import a PC blueprint | **Multi-source**: phone scan, CubiCasa, Matterport, RoomPlan, desktop |
| Place/arrange the plan | **Full renovation**: walls, finishes, extensions — not just layout |
| Geometry only | **Live cost + ROI**, finishes priced the right way, vendor catalogs |
| Field/enterprise | **Client + buyer + contractor** share the same model |
| Single device | **Co-located multi-device** sessions (shared anchors) |
| No AI | **AI**: scene insights, finish recognition, voice agent, auto-redesign |
| No site context | **Property lines, setbacks, sun path** on the real lot |

## Innovations we add

- **Design once, walk anywhere**: the design is device-agnostic; any headset
  auto-stages it from the project's model + measurements.
- **Markerless on-site registration** (two ground points) — works on a bare slab
  for new construction, not just inside finished rooms.
- **Wall-removal portals**: see the open-concept *through* the wall, composited
  from the scanned adjacent room (see `docs/Diminished-Reality.md`).
- **Before/after on site**, with cost attached to every change.
- **One project, three audiences**: the homeowner visualizes, the contractor
  verifies (MEP overlay, as-built, clash — see `docs/BUILDER-TOOLS.md`), the
  buyer/agent tours.

## How it maps to the code

- `Projects.RenovationProject` — the job record (origin, kind, status + the ids
  of the blueprint / model / layout / plan). Schema `0008`.
- `Projects.AutoStager` — applies the renovation plan and registers the design to
  the real world (blueprint two-point *or* shared anchor) → a `StagedScene`.
  Tested.
- Reuses: `Acquisition.*` (import), `Renovation.RenovationEngine` (apply edits),
  `Staging.BlueprintTransform` / `Collaboration.AnchorFrame` (registration),
  `MaterialCost` / `FinishQuantity` (cost), `Construction.*` (site/MEP).

## Status

Models, schema, and the auto-staging orchestration are built and tested. The
on-device capture UX, the desktop/phone design surfaces, and the AR rendering are
the in-editor layer (see `ROADMAP.md` Phases 5b/7/16).
