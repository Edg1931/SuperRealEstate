-- SuperRealEstate: furniture capture jobs (photogrammetry / object capture).
--
-- A client photographs a piece of their own furniture on the phone; we
-- reconstruct a to-scale 3D model and add it to their `furniture_assets`
-- library so it can be staged in a home during a walkthrough. This table tracks
-- the in-progress capture/reconstruction job; on success it links to the
-- resulting furniture_assets row. Photos live in Storage (a private per-user
-- bucket); only their count + the final model_url are kept here.

create table if not exists public.furniture_captures (
    id                 uuid primary key default gen_random_uuid(),
    owner_id           uuid not null references auth.users (id) on delete cascade,
    name               text not null default 'Furniture',
    method             text check (method in
                       ('object_capture','photogrammetry','lidar','gaussian_splat','manual')),
    status             text not null default 'capturing'
                       check (status in ('capturing','processing','ready','failed')),
    photo_count        integer not null default 0,
    photo_prefix       text,   -- Storage path prefix the photos uploaded under

    -- Metric bounds (meters) measured from AR at capture time → true-scale fit.
    width_m            numeric(6,3),
    depth_m            numeric(6,3),
    height_m           numeric(6,3),

    model_url          text,   -- glTF/USDZ in storage when ready
    thumbnail_url      text,
    furniture_asset_id uuid references public.furniture_assets (id) on delete set null,
    error              text,
    created_at         timestamptz not null default now(),
    updated_at         timestamptz not null default now()
);

create index if not exists idx_furniture_captures_owner on public.furniture_captures (owner_id);
create index if not exists idx_furniture_captures_status on public.furniture_captures (status);

alter table public.furniture_captures enable row level security;

-- Owner has full control of their capture jobs.
drop policy if exists furniture_captures_owner on public.furniture_captures;
create policy furniture_captures_owner on public.furniture_captures
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());
