-- SuperRealEstate initial schema
-- Tables: materials (shared catalog), properties, rooms, room_estimate_items.
-- Row Level Security: a user only sees their own properties/rooms/estimates;
-- the materials catalog is readable by any authenticated user.

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------------
-- Materials catalog (manually seeded; no official Lowe's/Home Depot pricing API)
-- ---------------------------------------------------------------------------
create table if not exists public.materials (
    id             uuid primary key default gen_random_uuid(),
    name           text        not null,
    category       text        not null,           -- Flooring, Paint, Tile, Trim, ...
    unit           text        not null            -- per_sqft | per_sqm | per_linft | each
                   check (unit in ('per_sqft','per_sqm','per_linft','each')),
    price_per_unit numeric(10,2) not null check (price_per_unit >= 0),
    currency       text        not null default 'USD',
    source         text,                            -- where the price came from
    updated_at     timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Properties (owned by a user)
-- ---------------------------------------------------------------------------
create table if not exists public.properties (
    id         uuid primary key default gen_random_uuid(),
    user_id    uuid        not null references auth.users (id) on delete cascade,
    address    text,
    latitude   double precision,
    longitude  double precision,
    created_at timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Rooms (belong to a property; store both raw outline and derived measurements)
-- ---------------------------------------------------------------------------
create table if not exists public.rooms (
    id                uuid primary key default gen_random_uuid(),
    property_id       uuid        not null references public.properties (id) on delete cascade,
    name              text        not null default 'Room',
    floor_outline     jsonb,                         -- [{x,y,z}, ...] world-space outline
    floor_area_sqm    numeric(10,3),
    wall_area_sqm     numeric(10,3),
    perimeter_m       numeric(10,3),
    ceiling_height_m  numeric(10,3),
    volume_cubic_m    numeric(10,3),
    created_at        timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Cost estimate line items (a material applied to a surface of a room)
-- ---------------------------------------------------------------------------
create table if not exists public.room_estimate_items (
    id           uuid primary key default gen_random_uuid(),
    room_id      uuid        not null references public.rooms (id) on delete cascade,
    surface      text        not null               -- floor | walls | ceiling | trim
                 check (surface in ('floor','walls','ceiling','trim')),
    material_id  uuid        references public.materials (id) on delete set null,
    quantity     numeric(10,3) not null,
    waste_factor numeric(5,3)  not null default 0.10,
    subtotal     numeric(12,2) not null,
    created_at   timestamptz   not null default now()
);

create index if not exists idx_properties_user on public.properties (user_id);
create index if not exists idx_rooms_property on public.rooms (property_id);
create index if not exists idx_estimate_items_room on public.room_estimate_items (room_id);

-- ---------------------------------------------------------------------------
-- Row Level Security
-- ---------------------------------------------------------------------------
alter table public.materials            enable row level security;
alter table public.properties           enable row level security;
alter table public.rooms                enable row level security;
alter table public.room_estimate_items  enable row level security;

-- Catalog: any authenticated user can read.
drop policy if exists materials_read on public.materials;
create policy materials_read on public.materials
    for select to authenticated using (true);

-- Properties: full CRUD limited to the owner.
drop policy if exists properties_owner on public.properties;
create policy properties_owner on public.properties
    for all to authenticated
    using (user_id = auth.uid())
    with check (user_id = auth.uid());

-- Rooms: accessible only through a property the user owns.
drop policy if exists rooms_owner on public.rooms;
create policy rooms_owner on public.rooms
    for all to authenticated
    using (exists (select 1 from public.properties p
                   where p.id = rooms.property_id and p.user_id = auth.uid()))
    with check (exists (select 1 from public.properties p
                        where p.id = rooms.property_id and p.user_id = auth.uid()));

-- Estimate items: accessible only through a room within the user's property.
drop policy if exists estimate_items_owner on public.room_estimate_items;
create policy estimate_items_owner on public.room_estimate_items
    for all to authenticated
    using (exists (select 1
                   from public.rooms r
                   join public.properties p on p.id = r.property_id
                   where r.id = room_estimate_items.room_id and p.user_id = auth.uid()))
    with check (exists (select 1
                        from public.rooms r
                        join public.properties p on p.id = r.property_id
                        where r.id = room_estimate_items.room_id and p.user_id = auth.uid()));
