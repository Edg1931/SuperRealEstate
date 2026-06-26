-- SuperRealEstate: renovation/staging projects — the record a client's job lives
-- in, tying a blueprint (any source) to its model + design + AR-ready status.

create extension if not exists "pgcrypto";

create table if not exists public.renovation_projects (
    id                 uuid primary key default gen_random_uuid(),
    owner_id           uuid not null references auth.users (id) on delete cascade,
    property_id        uuid references public.properties (id) on delete set null,
    name               text not null default 'Project',
    kind               text check (kind in ('empty_staging','renovation','extension','new_build')),
    origin             text check (origin in
                       ('phone_scan','cubicasa_upload','cubicasa_api','matterport_api','roomplan','manual_desktop')),
    status             text not null default 'draft'
                       check (status in ('draft','designed','ready_for_ar','archived')),
    blueprint_id       uuid references public.blueprints (id) on delete set null,
    building_model_id  uuid references public.building_models (id) on delete set null,
    staging_layout_id  uuid references public.staging_layouts (id) on delete set null,
    renovation_plan_id uuid references public.renovation_plans (id) on delete set null,
    created_at         timestamptz not null default now()
);

create index if not exists idx_projects_owner on public.renovation_projects (owner_id);
create index if not exists idx_projects_property on public.renovation_projects (property_id);

alter table public.renovation_projects enable row level security;

drop policy if exists projects_owner on public.renovation_projects;
create policy projects_owner on public.renovation_projects
    for all to authenticated
    using (owner_id = auth.uid())
    with check (owner_id = auth.uid());
