# Removing walls in AR — feasibility & plan

> "When it comes to augmented reality I want the impossible to become possible."

This documents how we show a **wall removed** — seeing the adjacent space where a
wall used to be — and is honest about what's easy, what's hard, and why our setup
makes it far more achievable than the "unsolved research" reputation suggests.

## The reframe: compositing, not inpainting

The genuinely-unsolved research problem is **live diminished reality**: take a
single live camera feed and *hallucinate* plausible geometry behind an object
you have **never seen**, in real time. Hard, because you're inventing the unknown.

**We don't have that problem.** We already have **3D scans of both rooms**
(RoomPlan / Polycam / Matterport). We know exactly what's behind the wall — it's
the adjacent room, already captured. So "remove the wall" becomes **portal
compositing**: render the *known* adjacent capture in the wall's place. No
guessing.

## Why passthrough headsets make it work

Vision Pro and Galaxy XR are **video passthrough** (you see a camera feed, not
real photons through glass). That's the unlock:

1. The real wall is **opaque**. Place a virtual surface — textured with the
   scanned adjacent room — **exactly over the wall's location**. The real wall
   sits *behind* it, so the user simply sees room-B instead of the wall.
2. Use the headset's **scene depth** so real foreground (furniture, a person)
   correctly **occludes** the portal — it doesn't draw over things in front.
3. Use the **scanned wall geometry as the stencil/aperture** so the opening
   matches the real wall's silhouette (this repo: `WallApertureBuilder`).

This is occlude-and-replace, not erase-and-inpaint. It's the same reason a
virtual monitor can hide a real one on Vision Pro today.

## See-through ≠ walk-through (the honest limit)

- **See the open concept (occupied home): very feasible.** Stand in room-A and
  look at where the wall would go; the portal shows room-B at 1:1.
- **Walk through the removed wall: blocked by physics + safety.** The real wall
  is still there — you can't pass through it, and we must not encourage walking
  into it. True walk-through only works in an **empty shell / new construction**
  (no wall yet) — which is exactly a flagship use case (Pillar 3b/7).

So: ship "see it gone" first; reserve "walk through it" for empty/under-
construction spaces.

## The hard parts (and mitigations)

| Challenge | Mitigation |
|---|---|
| **Edge seams** at floor/ceiling/corners (glimpse of real wall) | Tight anchoring to a shared spatial anchor; stencil to the scanned wall quad with a slight feather/overlap; align the portal plane to the detected real wall plane each frame. |
| **Lighting / color mismatch** (scanned B vs. live A) | White-balance + exposure-match the captured texture to the live feed; relight; let users color-correct. |
| **Tracking drift** → portal slides off the wall | Re-register to the wall plane via scene mesh; anchor-relative rendering, not world-absolute. |
| **Latency** of passthrough vs. virtual render | Keep the portal a simple textured mesh / splat; render anchor-locked so it moves with the head correctly. |
| **Platform passthrough limits** | visionOS: occlude the wall with virtual content (allowed) rather than sampling arbitrary camera pixels (restricted by Apple's privacy model). Android XR / Quest: more latitude for stencil + depth compositing via scene mesh. |

## Capture source fits the job

- **Apple RoomPlan / scene mesh** → a clean wall plane to anchor the aperture to.
- **Polycam / Gaussian splatting** → photoreal capture of the adjacent room;
  **splats are ideal** for a convincing portal (view-dependent, photoreal, cheap
  to render) vs. a decimated mesh.
- **Matterport** → a navigable twin to source the adjacent-room imagery.

## Phased plan

- **P0 — Flat portal:** show a single captured photo/panorama of room-B on the
  wall quad. Convincing head-on; weak parallax. Fastest proof.
- **P1 — Textured-mesh portal:** render the scanned room-B mesh behind the
  aperture with correct parallax as you move; depth-occlude real foreground.
- **P2 — Gaussian-splat portal:** swap the mesh for a splat of room-B for
  photoreal, view-dependent realism.
- **P3 — Multi-room + walk-through:** in empty shells / new construction, remove
  multiple walls and physically walk the open plan (no safety conflict).
- **P4 — Live blend (research):** edge inpainting where the scan is incomplete,
  real-time relighting — the genuinely hard frontier; optional.

## What's in the repo now

- `Renovation.WallApertureBuilder` — computes the wall's world-space quad
  (corners, center, normal, area) the portal renders into. Pure + tested.
- `Renovation.RemovedWallPortal` — models a removed wall as a portal into a
  scanned capture, with `PortalRenderMode` (opaque vs. stencil) and a
  `PhysicalWallStillPresent` flag (see-through vs. walk-through).
- `Renovation.IPortalRenderer` — the AR-layer contract (`ShowPortalAsync` /
  `HidePortalAsync`); platform-specific implementations are in-editor work.

The math and model are here; the passthrough rendering (stencil + depth occlusion
+ splat/mesh draw) is the Unity/PolySpatial/OpenXR implementation, built per
device against this contract.
