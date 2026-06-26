# Cross-platform co-location (shared anchored sessions)

How a Vision Pro, a Galaxy XR, and a phone all see the **same** virtual furniture
and renovated walls pinned to the **same** spot in the real room.

## The idea

Author/store all shared content **relative to one anchor**, not in absolute world
coordinates. The host anchors a session origin; each participant resolves that
same physical anchor into its own local pose and renders content anchor-relative.
Because everyone references the same real anchor, everything lines up — no
per-device world frames to reconcile.

```
Host                                Participants (any platform)
────                                ───────────────────────────
pick origin pose                    receive SharedAnchorPayload
create anchor  ──► anchorId         resolve anchorId → local Pose
publish SharedAnchorPayload  ─────► build AnchorFrame
                                    render placements via AnchorFrame.ToWorld(...)
```

## The cross-platform catch (and the fix)

ARKit (Apple) and ARCore (Google) anchors **don't interoperate directly**. So:

- **ARCore Cloud Anchors** is the portable path — they host/resolve on **iOS and
  Android** (incl. Android XR), so one anchor works across a Vision Pro–adjacent
  iPhone, a Galaxy XR, and Android phones.
- **OpenXR persistent/spatial anchors** cover Android XR ↔ Android XR.
- The `SharedAnchorPayload` advertises a `Provider` (+ an optional fallback), and
  each device resolves the one it can. `SessionCoLocator.ResolveAsync` tries the
  primary, then the fallback.

> visionOS note: Apple's own shared world anchors stay within the Apple
> ecosystem. For a mixed Apple+Android session, route co-location through Cloud
> Anchors (resolve the same Cloud Anchor on the Vision Pro's ARKit session).

## In the repo

- `Collaboration.ISpatialAnchorService` — platform anchor create/resolve (impl
  per platform: ARKit / ARCore Cloud / OpenXR).
- `Collaboration.SharedAnchorPayload` — the portable token stored on the session
  (`AnchorProvider`, anchor id, optional fallback).
- `Collaboration.SessionCoLocator` — host `PublishOriginAsync`; participant
  `ResolveAsync` → `AnchorFrame` (with fallback). Tested with a fake anchor svc.
- `Collaboration.AnchorFrame` — pure math to convert anchor-relative
  positions/placements to each device's world. Tested (round-trip, origin maps to
  anchor).

## Flow with the rest of the app

1. Host starts a `SharedSession`; `SessionCoLocator.PublishOriginAsync` →
   `SharedAnchorPayload` saved on the session (Supabase).
2. Each joiner loads the payload, `ResolveAsync` → `AnchorFrame`.
3. Staging `Placement`s and renovation edits are stored **anchor-relative**;
   `AnchorFrame.ToWorldPlacement` renders them locally.
4. Realtime placement edits (`ISharedSessionService`) broadcast anchor-relative
   coords, so a move looks identical on every device.

## In-editor / on-device work

- Concrete `ISpatialAnchorService` per platform (ARCore Cloud Anchors SDK; OpenXR
  spatial anchors; ARKit).
- Hosting/resolving has real latency + failure modes — surface "aligning…" and
  "couldn't align, rescan" states (see POLISH-REVIEW).
