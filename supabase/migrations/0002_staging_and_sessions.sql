-- SuperRealEstate: virtual staging + shared multi-device sessions.
--
-- Adds:
--   furniture_assets    -- a user's scanned/owned 3D furniture library
--   staging_layouts     -- a named arrangement for a room
--   staging_placements  -- a furniture asset positioned within a layout
--   sessions            -- a shared walkthrough (host + participants)
--   session_participants-- who is in a session, on what device, in what role
--
-- Sharing model: everyone in a session sees the same layout + the furniture
-- placed in it, regardless of device (Vision Pro / Galaxy XR / phone / tablet).

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------------
-- Furniture library (owned by a user; can be shared into sessions)
-- ---------------------------------------------------------------------------
create table if not exists public.furniture_assets (
    id             uuid primary key default gen_random_uuid(),
    owner_id       uuid not null references auth.users (id) on delete cascade,
    name           text not null,
    category       text,                        -- sofa, table, bed, chair, ...
    width_m        numeric(6,3),                -- real-world bounding box
    depth_m        numeric(6,3),
    height_m       numeric(6,3),
    model_url      text,                        -- glTF/USDZ in storage
    thumbnail_url  text,
    capture_method text check (capture_method in
                   ('object_capture','photogrammetry','lidar','gaussian_splat','catalog','manual')),
    created_at     timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Staging layouts + placements (an arrangement of furniture in a room)
-- ---------------------------------------------------------------------------
create table if not exists public.staging_layouts (
    id         uuid primary key default gen_random_uuid(),
    room_id    uuid references public.rooms (id) on delete cascade,
    name       text not null default 'Layout',
    created_by uuid not null references auth.users (id) on delete cascade,
    created_at timestamptz not null default now()
);

create table if not exists public.staging_placements (
    id                uuid primary key default gen_random_uuid(),
    layout_id         uuid not null references public.staging_layouts (id) on delete cascade,
    furniture_asset_id uuid references public.furniture_assets (id) on delete set null,
    pos_x             numeric(8,4) not null default 0,
    pos_y             numeric(8,4) not null default 0,
    pos_z             numeric(8,4) not null default 0,
    rot_y_deg         numeric(7,3) not null default 0,  -- yaw on the floor
    scale             numeric(6,4) not null default 1,
    anchor_id         text,                              -- shared spatial anchor
    updated_at        timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Shared sessions (a co-located / remote multi-device walkthrough)
-- ---------------------------------------------------------------------------
create table if not exists public.sessions (
    id                uuid primary key default gen_random_uuid(),
    host_id           uuid not null references auth.users (id) on delete cascade,
    property_id       uuid references public.properties (id) on delete set null,
    layout_id         uuid references public.staging_layouts (id) on delete set null,
    spatial_anchor_id text,                              -- cross-device co-location
    status            text not null default 'active'
                      check (status in ('active','paused','ended')),
    created_at        timestamptz not null default now()
);

create table if not exists public.session_participants (
    id          uuid primary key default gen_random_uuid(),
    session_id  uuid not null references public.sessions (id) on delete cascade,
    user_id     uuid not null references auth.users (id) on delete cascade,
    role        text not null default 'buyer'
                check (role in ('host','agent','buyer','guest')),
    device_kind text check (device_kind in
                ('vision_pro','android_xr','ios_phone','android_phone','tablet','web')),
    joined_at   timestamptz not null default now(),
    unique (session_id, user_id)
);

create index if not exists idx_furniture_owner on public.furniture_assets (owner_id);
create index if not exists idx_layouts_room on public.staging_layouts (room_id);
create index if not exists idx_placements_layout on public.staging_placements (layout_id);
create index if not exists idx_sessions_host on public.sessions (host_id);
create index if not exists idx_participants_session on public.session_participants (session_id);
create index if not exists idx_participants_user on public.session_participants (user_id);

-- ---------------------------------------------------------------------------
-- Helper: is the current user a participant of a session?
-- SECURITY DEFINER so it can read session_participants without tripping RLS
-- recursion when used inside policies on that same chain.
-- ---------------------------------------------------------------------------
create or replace function public.is_session_participant(_session_id uuid)
returns boolean
language sql
security definer
set search_path = public
as $$
    select exists (
        select 1 from public.session_participants sp
        where sp.session_id = _session_id and sp.user_id = auth.uid()
    );
$$;

-- ---------------------------------------------------------------------------
-- Row Level Security
-- ---------------------------------------------------------------------------
alter table public.furniture_assets     enable row level security;
alter table public.staging_layouts      enable row level security;
alter table public.staging_placements   enable row level security;
alter table public.sessions             enable row level security;
alter table public.session_participants enable row level security;

-- Furniture: owner has full control; participants of a session whose layout
-- uses the asset may read it (so shared staging shows everyone's furniture).
drop policy if exists furniture_owner on public.furniture_assets;
create policy furniture_owner on public.furniture_assets
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists furniture_shared_read on public.furniture_assets;
create policy furniture_shared_read on public.furniture_assets
    for select to authenticated
    using (exists (
        select 1
        from public.staging_placements pl
        join public.staging_layouts l on l.id = pl.layout_id
        join public.sessions s on s.layout_id = l.id
        where pl.furniture_asset_id = furniture_assets.id
          and public.is_session_participant(s.id)
    ));

-- Layouts: creator full control; session participants may read/edit a shared layout.
drop policy if exists layouts_creator on public.staging_layouts;
create policy layouts_creator on public.staging_layouts
    for all to authenticated
    using (created_by = auth.uid())
    with check (created_by = auth.uid());

drop policy if exists layouts_shared on public.staging_layouts;
create policy layouts_shared on public.staging_layouts
    for select to authenticated
    using (exists (
        select 1 from public.sessions s
        where s.layout_id = staging_layouts.id
          and public.is_session_participant(s.id)
    ));

-- Placements: editable by anyone who can access the parent layout (shared editing).
drop policy if exists placements_access on public.staging_placements;
create policy placements_access on public.staging_placements
    for all to authenticated
    using (exists (
        select 1 from public.staging_layouts l
        where l.id = staging_placements.layout_id
          and (l.created_by = auth.uid()
               or exists (select 1 from public.sessions s
                          where s.layout_id = l.id and public.is_session_participant(s.id)))
    ))
    with check (exists (
        select 1 from public.staging_layouts l
        where l.id = staging_placements.layout_id
          and (l.created_by = auth.uid()
               or exists (select 1 from public.sessions s
                          where s.layout_id = l.id and public.is_session_participant(s.id)))
    ));

-- Sessions: host full control; participants may read.
drop policy if exists sessions_host on public.sessions;
create policy sessions_host on public.sessions
    for all to authenticated
    using (host_id = auth.uid())
    with check (host_id = auth.uid());

drop policy if exists sessions_participant_read on public.sessions;
create policy sessions_participant_read on public.sessions
    for select to authenticated
    using (public.is_session_participant(id));

-- Participants: a user can see the roster of sessions they're in, and can add
-- (join) / remove (leave) their own row. (Host-driven invites can be added later.)
drop policy if exists participants_read on public.session_participants;
create policy participants_read on public.session_participants
    for select to authenticated
    using (public.is_session_participant(session_id));

drop policy if exists participants_self_insert on public.session_participants;
create policy participants_self_insert on public.session_participants
    for insert to authenticated
    with check (user_id = auth.uid());

drop policy if exists participants_self_delete on public.session_participants;
create policy participants_self_delete on public.session_participants
    for delete to authenticated
    using (user_id = auth.uid());
