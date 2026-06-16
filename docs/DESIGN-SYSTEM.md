# SuperRealEstate — Spatial Design System

> **Design philosophy: the home is the canvas.** The real space is the
> interface. Our UI is calm, glanceable, and content-forward — it appears where
> you look, says the one thing that matters, and gets out of the way. This is a
> trust product (real estate, money, advice), so it reads premium and honest,
> never gimmicky.

Optimized for **video-passthrough headsets** (Apple Vision Pro, Samsung Galaxy
XR) with **eye tracking + pinch**, and gracefully down to phone/tablet.

## The aesthetic — "Spatial Glass"

- **Frosted translucent panels** (low opacity, subtle background blur) so the
  room shows through — UI floats *in* the space, not pasted *over* it.
- **Soft depth shadows + a faint edge halo** to separate panels from busy
  passthrough backgrounds without heavy chrome.
- **Generous negative space**, rounded corners, one idea per panel.
- **Warm-neutral base + a single confident accent.** Quiet by default; color
  carries meaning (positive / caution / advisory), never decoration.
- **Never pure white** (#FFFFFF blooms on passthrough optics) — use a near-white
  at reduced luminance. The palette **adapts to ambient luminance** (lighter in
  dark rooms, denser in bright ones) for constant legibility.

## Color roles

| Role | Use | Note |
|---|---|---|
| `Surface` | Panel fill (frosted) | ~70% opacity over blur |
| `OnSurface` | Primary text/icons | near-white @ ~92% lum (dark UI) / near-black (light UI) |
| `Accent` | Primary action, selection, focus | one hue; colorblind-safe; never the *only* signal |
| `Positive` | "fits", savings, good news | paired with an icon |
| `Caution` | over-budget, tight clearance | paired with an icon |
| `Advisory` | condition/code/load-bearing/finish guesses | distinct outline + ⚠ — ties to responsible-AI guardrails |

Advisory styling is a **first-class** role: anything estimated/uncertain
(brand guesses, defect flags, load-bearing) renders with the advisory treatment
and a disclaimer, never as plain fact.

## Typography

- Humanist sans, high x-height, for glanceability.
- **Type scale measured in angular size (degrees), not pixels** — text stays
  legible at any panel depth. Body cap-height ≥ ~0.45°; never below ~0.35°.
- Weight + size for hierarchy; avoid long lines (≤ ~28° width / ~40 chars).

## Layout & anchoring

Choose the *frame* deliberately — wrong anchoring is the #1 cause of AR discomfort.

| Anchor | Use for | Avoid |
|---|---|---|
| **World-locked** | Labels/tags on the thing they describe (price on a wall, plant card on a plant, dimension on a floor edge) | — |
| **Surface-locked** | Detail cards floating just off a surface, billboarded to face the user | drifting/seams (re-register each frame) |
| **Wrist/hand-locked** | The tool palette / radial menu, summoned at the hand (gaze at palm on Vision Pro; wrist on Quest) | persistent visibility |
| **Head-locked** | *Only* a tiny persistent status (recording dot, session peers) | panels/menus — causes fatigue & sim-sickness |

**Comfort zone:** keep interactive content in the central ~30–40° cone, depth
**0.5–2 m (sweet spot ~1–1.5 m)**, slightly **below** eye line. Never place
content closer than ~0.4 m (vergence–accommodation discomfort) or lock it to the
far periphery.

## Eye-tracking interaction model

**Primary: gaze to target → pinch to select** (the Vision Pro model). Looking
is *aiming and previewing*; pinch is *committing*.

- **Hit targets ≥ ~2° visual angle, ≥ ~1° spacing.** Expand the hit area beyond
  the visual bounds. Subtle gaze-hover feedback: a soft glow + 1.03× scale over
  ~120 ms. Never move layout on hover (no reflow).
- **Gaze-reveal, gaze-dismiss:** look at a tagged object → its info card fades
  in; look away → it recedes. This is how the space stays uncluttered while
  "everything explains itself."
- **No Midas touch:** gaze *never* triggers a destructive or costly action.
  Selection requires pinch, controller, or voice confirm.
- **Dwell is an accessibility fallback, not the default** — 600–1000 ms with a
  clear radial progress ring, for hands-free/assistive use only.
- **Voice is a parallel modality:** "what's this?", "measure this room", "how
  much mulch?", "remove this wall", "show it in walnut."
- **Confirmation** for irreversible/expensive actions (delete a layout, place an
  order): a calm two-option panel, accent on the safe default.

## Component catalog

Each maps to the same data the app already produces; views are platform-specific.

| Component | Anchor | Interaction | Content |
|---|---|---|---|
| **Insight card** (`SceneInsight`) | surface/world-locked to the subject | gaze-reveal; pinch to pin/expand | headline + one relayable line; advisory styling when flagged |
| **Measurement readout** | world-locked to the edge/area | glance | dimension, area; tap to switch units |
| **Surface finish chip** (`SurfaceFinding`) | on the wall/floor | gaze → swatch; pinch → re-finish / "shop this look" | brand·color·$, advisory |
| **Radial tool menu** | wrist/hand | gaze a wedge + pinch | Measure · Identify · Finish · Stage · Remove wall · Notes |
| **Budget HUD** | wrist or a docked corner panel | glance | running renovation/furnishing total; shared in session |
| **Vendor/catalog tray** | surface-locked shelf | gaze browse + pinch to place | thumbnails, price, fit badge |
| **Before/after toggle** | near the edited surface/wall | pinch / voice | one control for finish & wall-removal portals |
| **Plant / vegetation card** | world-locked to the plant | gaze-reveal | species, care, toxicity, mature size, cost |
| **Spatial annotation / pin** | world-locked | pinch to drop; voice to dictate | attributed to a session peer |

## Motion

Gentle and physical: 150–300 ms ease-out; panels fade + scale in at their depth;
no hard snaps or fast slides (sim-sickness). Always honor **Reduce Motion**.

## Multi-user (shared sessions)

- Each participant has a **private HUD** (their tools, their gaze targets).
- **Shared annotations are world-locked and attributed** with a peer color + tiny
  avatar; the agent can enter **Presenter** mode to spotlight an item for all.
- One person's gaze is never shown to others by default (privacy); opt-in
  "show my pointer."

## Accessibility (non-negotiable)

- Dynamic **type scale** (user-set), honored everywhere (angular sizing helps).
- **Colorblind-safe** accent; meaning never by color alone (always icon/label).
- **Voice control** + **dwell mode** as full alternatives to pinch.
- **Handedness** setting; captions for any audio; **high-contrast** mode;
  no flashing (photosensitivity).

## Per-device adaptation

| Device | Primary input | Notes |
|---|---|---|
| **Apple Vision Pro** | gaze + pinch | bounded volumes for tools, unbounded for world overlays; honor system materials |
| **Samsung Galaxy XR (Android XR)** | gaze + pinch / controller ray | same component set via OpenXR |
| **Phone / tablet** | touch + on-screen AR ray | gaze-reveal → tap-reveal; radial menu → bottom sheet |

Single design language, three input mappings — the components and tokens are
shared; only the input/affordance layer changes. Tokens live in
`Assets/Scripts/UI` so every view reads one source of truth.
