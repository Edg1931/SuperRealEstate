-- SuperRealEstate: construction/builder data — parcels + building systems (MEP).
--
--   parcels          -- property-line polygon for a property (site layout/setbacks)
--   building_systems -- electrical/plumbing/HVAC/etc. runs + fixtures for a model

create extension if not exists "pgcrypto";

create table if not exists public.parcels (
    id          uuid primary key default gen_random_uuid(),
    property_id uuid not null references public.properties (id) on delete cascade,
    boundary    jsonb,   -- [{x,y}, ...] plan-meter property line
    created_at  timestamptz not null default now()
);

create table if not exists public.building_systems (
    id                uuid primary key default gen_random_uuid(),
    building_model_id uuid not null references public.building_models (id) on delete cascade,
    system            text check (system in
                      ('electrical','plumbing','hvac','structural','low_voltage','gas','sewer')),
    elements          jsonb not null default '[]'::jsonb,  -- SystemElement[] (runs + fixtures)
    created_at        timestamptz not null default now()
);

create index if not exists idx_parcels_property on public.parcels (property_id);
create index if not exists idx_building_systems_model on public.building_systems (building_model_id);

alter table public.parcels          enable row level security;
alter table public.building_systems enable row level security;

-- Parcels: accessible through a property the user owns.
drop policy if exists parcels_owner on public.parcels;
create policy parcels_owner on public.parcels
    for all to authenticated
    using (exists (select 1 from public.properties p where p.id = parcels.property_id and p.user_id = auth.uid()))
    with check (exists (select 1 from public.properties p where p.id = parcels.property_id and p.user_id = auth.uid()));

-- Building systems: accessible through a building model the user owns.
drop policy if exists building_systems_owner on public.building_systems;
create policy building_systems_owner on public.building_systems
    for all to authenticated
    using (exists (select 1 from public.building_models m where m.id = building_systems.building_model_id and m.owner_id = auth.uid()))
    with check (exists (select 1 from public.building_models m where m.id = building_systems.building_model_id and m.owner_id = auth.uid()));
