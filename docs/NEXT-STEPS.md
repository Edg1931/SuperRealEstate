# What to work on next (prioritized)

We have a broad, mostly-tested foundation. The highest-leverage work now is
turning it into something you can *see running* — first on the web, then on the
Galaxy XR. Ordered by impact.

## 1. Assemble a minimal runnable AR scene (Galaxy XR first) ⭐
The single thing that turns all the tested logic into "put it on your head and
walk a room." Wire one scene: XR Origin + AR Session + AR Plane Manager +
`RoomMeasureController` + `SpatialPointerInput` + `XrSessionBootstrap` +
`StagedSceneRenderer`, enable the Android XR OpenXR features
(`docs/AndroidXR-Setup.md`), and build. Mostly in-Editor.
*I can build:* a runtime **scene-bootstrap** script that assembles the AR rig +
components in code, so you press Play instead of wiring by hand.

## 2. Go live so you can view on every device
- **Provision Supabase**: apply migrations `0001`–`0009`, run `seed.sql`,
  `supabase secrets set ANTHROPIC_API_KEY=...`, deploy the Edge Functions.
- **Deploy the web companion to Vercel** (Root Directory = `web`).
Then the catalog + AI features are live and viewable on phone/tablet/computer.
*I can do this with you* (or deploy via the Vercel integration given your keys).

## 3. The phone/desktop design surface
The web/desktop UI that lets a client design a renovation/staging and **writes the
locked payload** (`docs/PROJECT-PAYLOAD-CONTRACT.md`). This is what makes the
"design once → auto-stage in AR" loop real end to end. Web-buildable.

## 4. Verify the third-party API integrations
RentCast (comps/valuation), Regrid (parcels), CubiCasa + Matterport (import) —
the endpoint paths/fields are flagged "VERIFY" in the Edge Functions. Confirm
against the live API docs and adjust the field mappings. *I can do this* once you
have the provider accounts/keys.

## 5. Real assets + polish
Furniture prefabs/materials (swap the placeholders via `StagedSceneRenderer`'s
prefab map), the overlay label styling, and the loading/empty/error states +
onboarding (already built — wire them into the UI).

## 6. Hardening follow-ups
Per-user **rate limiting** on the Edge Functions (gateway/counter table — the one
security item that can't live in a function), and invite expiry/rotation for
sessions.

---

### Suggested immediate next move
Let me build the **runtime scene-bootstrap** (item 1) so you have a concrete
Galaxy XR starting point to press Play on, and in parallel I can help you stand up
**Supabase + Vercel** (item 2) so the web view works on all your devices today.
