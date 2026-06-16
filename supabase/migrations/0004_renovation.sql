-- SuperRealEstate: editable building models + renovation plans.
--
--   building_models   -- an editable structural model imported from a capture
--                        (RoomPlan/CubiCasa/Polycam = semantic; Matterport =
--                        seeded from metadata). Geometry stored as JSONB for
--                        flexibility while the schema settles.
--   renovation_plans  -- a base model + a non-destructive edit list (JSONB),
--                        enabling before/after toggling and shareable edits.

create extension if not exists "pgcrypto";

create table if not exists public.building_models (
    id          uuid primary key default gen_random_uuid(),
    owner_id    uuid not null references auth.users (id) on delete cascade,
    property_id uuid references public.properties (id) on delete set null,
    source_type text check (source_type in
                ('roomplan','cubicasa','polycam','matterport','ar_scan','manual')),
    geometry    jsonb,   -- { walls:[...], rooms:[...] } mirroring BuildingModel
    created_at  timestamptz not null default now()
);

create table if not exists public.renovation_plans (
    id                uuid primary key default gen_random_uuid(),
    owner_id          uuid not null references auth.users (id) on delete cascade,
    building_model_id uuid not null references public.building_models (id) on delete cascade,
    name              text not null default 'Renovation',
    edits             jsonb not null default '[]'::jsonb,  -- ordered RenovationEdit list
    created_at        timestamptz not null default now()
);

create index if not exists idx_building_models_owner on public.building_models (owner_id);
create index if not exists idx_renovation_plans_owner on public.renovation_plans (owner_id);
create index if not exists idx_renovation_plans_model on public.renovation_plans (building_model_id);

alter table public.building_models   enable row level security;
alter table public.renovation_plans  enable row level security;

drop policy if exists building_models_owner on public.building_models;
create policy building_models_owner on public.building_models
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());

drop policy if exists renovation_plans_owner on public.renovation_plans;
create policy renovation_plans_owner on public.renovation_plans
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());
