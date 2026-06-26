# Supabase backend

Schema and seed data for SuperRealEstate.

## Tables

- `materials` — shared, manually-priced materials catalog (readable by any
  authenticated user). No official Lowe's/Home Depot pricing API exists, so
  prices are seeded and meant to be edited.
- `properties` — owned by a user (RLS: owner-only).
- `rooms` — belong to a property; store the raw floor outline and the derived
  measurements.
- `room_estimate_items` — a material applied to a surface of a room, with the
  computed subtotal.

Row Level Security is enabled on every table.

## Apply

**Hosted project (recommended for prototyping):**
```bash
# link once, then push migrations + seed
supabase link --project-ref <your-project-ref>
supabase db push                # applies migrations/0001_init.sql
psql "$DATABASE_URL" -f supabase/seed.sql   # or run seed.sql in the SQL editor
```

**Local stack:**
```bash
supabase start
supabase db reset               # applies migrations, then seed.sql automatically
```

> This repo intentionally does **not** auto-create a cloud Supabase project.
> Create/link your own (free tier is fine), then apply the migration above.

## Edge Functions

Third-party APIs and the AI model are proxied through Edge Functions so their
keys stay server-side:

- **`scene-insights`** (implemented, `functions/scene-insights/`) → calls Claude
  (`claude-opus-4-8`) with a captured frame + context and returns structured
  `SceneInsight[]`. Deploy and configure:
  ```bash
  supabase secrets set ANTHROPIC_API_KEY=sk-ant-...
  supabase functions deploy scene-insights
  ```
- **`plant-id`** (implemented, `functions/plant-id/`) → identifies vegetation
  from a frame, returns `PlantIdentification[]`. Same deploy/secret as above
  (`supabase functions deploy plant-id`). Upgrade path: front with Pl@ntNet/
  Plant.id for species accuracy, enrich with Claude.
- **`voice-agent`** (implemented, `functions/voice-agent/`) → turns a spoken
  transcript (+ optional measurements/location/snapshot) into a spoken reply +
  an app action (measure, identify plant, estimate material, remove wall, …).
  `supabase functions deploy voice-agent`. Device handles speech I/O.
- `comps` → RentCast (later)
- `parcels` → Regrid (later)

Set their keys as Supabase function secrets (never in the Unity client). See
`.env.example` for the client-side values the app needs.
