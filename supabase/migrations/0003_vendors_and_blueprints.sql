-- SuperRealEstate: vendor catalogs + blueprint authoring.
--
-- Adds:
--   vendors               -- a furniture/fixture vendor (importable catalog)
--   vendor_catalog_items  -- a purchasable 3D asset from a vendor (SKU + price)
--   blueprints            -- a floor plan with real-world scale, authored on PC
--
-- Extends:
--   staging_layouts       -- may be tied to a blueprint
--   staging_placements    -- may reference a vendor catalog item (vs. own
--                            furniture), and may carry blueprint-plane coords
--                            for desktop authoring before on-site registration.

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------------
-- Vendors + their catalogs (a shared marketplace, readable by all users)
-- ---------------------------------------------------------------------------
create table if not exists public.vendors (
    id         uuid primary key default gen_random_uuid(),
    name       text not null,
    website    text,
    logo_url   text,
    created_at timestamptz not null default now()
);

create table if not exists public.vendor_catalog_items (
    id            uuid primary key default gen_random_uuid(),
    vendor_id     uuid not null references public.vendors (id) on delete cascade,
    sku           text,
    name          text not null,
    category      text,                       -- sofa, table, range, cabinet, ...
    width_m       numeric(6,3),               -- real-world bounding box
    depth_m       numeric(6,3),
    height_m      numeric(6,3),
    model_url     text,                       -- glTF/USDZ
    thumbnail_url text,
    price         numeric(12,2),
    currency      text not null default 'USD',
    created_at    timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Blueprints (a floor plan with a real-world scale; authored at a desktop)
-- ---------------------------------------------------------------------------
create table if not exists public.blueprints (
    id              uuid primary key default gen_random_uuid(),
    owner_id        uuid not null references auth.users (id) on delete cascade,
    property_id     uuid references public.properties (id) on delete set null,
    name            text not null default 'Blueprint',
    image_url       text,                     -- the plan image in storage
    plan_width      numeric(10,3),            -- plan-space extents (plan units)
    plan_height     numeric(10,3),
    meters_per_unit numeric(10,5) not null default 1, -- plan unit -> meters
    created_at      timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Extend staging tables
-- ---------------------------------------------------------------------------
alter table public.staging_layouts
    add column if not exists blueprint_id uuid references public.blueprints (id) on delete set null;

alter table public.staging_placements
    add column if not exists catalog_item_id uuid references public.vendor_catalog_items (id) on delete set null,
    add column if not exists plan_x       numeric(10,4),   -- blueprint-plane position
    add column if not exists plan_y       numeric(10,4),
    add column if not exists plan_yaw_deg numeric(7,3);

-- A placement is sourced from at most one of: own furniture OR a vendor item.
alter table public.staging_placements
    drop constraint if exists placement_single_source;
alter table public.staging_placements
    add constraint placement_single_source
    check (furniture_asset_id is null or catalog_item_id is null);

create index if not exists idx_catalog_items_vendor on public.vendor_catalog_items (vendor_id);
create index if not exists idx_blueprints_owner on public.blueprints (owner_id);
create index if not exists idx_placements_catalog_item on public.staging_placements (catalog_item_id);

-- ---------------------------------------------------------------------------
-- Row Level Security
-- ---------------------------------------------------------------------------
alter table public.vendors              enable row level security;
alter table public.vendor_catalog_items enable row level security;
alter table public.blueprints           enable row level security;

-- Vendor marketplace: any authenticated user can browse; writes are
-- service-role only (no write policy) — catalogs are curated/seeded.
drop policy if exists vendors_read on public.vendors;
create policy vendors_read on public.vendors
    for select to authenticated using (true);

drop policy if exists catalog_items_read on public.vendor_catalog_items;
create policy catalog_items_read on public.vendor_catalog_items
    for select to authenticated using (true);

-- Blueprints: owner has full control; session participants may read a blueprint
-- referenced by a shared layout (so PC-authored designs appear on-site).
drop policy if exists blueprints_owner on public.blueprints;
create policy blueprints_owner on public.blueprints
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists blueprints_shared on public.blueprints;
create policy blueprints_shared on public.blueprints
    for select to authenticated
    using (exists (
        select 1
        from public.staging_layouts l
        join public.sessions s on s.layout_id = l.id
        where l.blueprint_id = blueprints.id
          and public.is_session_participant(s.id)
    ));
