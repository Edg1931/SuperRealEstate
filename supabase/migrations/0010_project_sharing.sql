-- SuperRealEstate: shareable read-only project links.
--
-- Lets an owner publish a client-facing link (no sign-in) to a single project's
-- plan + staging + edits. Security model: a per-project random share token, read
-- ONLY through a SECURITY DEFINER function (no broad RLS opening). Sharing is
-- opt-in and revocable by the owner; turning it off invalidates the link.

create extension if not exists "pgcrypto";

alter table public.renovation_projects
    add column if not exists share_token text unique;

create index if not exists idx_projects_share_token
    on public.renovation_projects (share_token) where share_token is not null;

-- ---------------------------------------------------------------------------
-- Owner: enable sharing → returns a token (idempotent: reuses an existing one).
-- ---------------------------------------------------------------------------
create or replace function public.enable_project_sharing(p_project uuid)
returns text
language plpgsql
security definer
set search_path = public
as $$
declare
    v_token text;
begin
    select share_token into v_token
    from public.renovation_projects
    where id = p_project and owner_id = auth.uid();

    if not found then
        raise exception 'project not found or not owned by caller';
    end if;

    if v_token is null then
        v_token := encode(gen_random_bytes(16), 'hex');
        update public.renovation_projects set share_token = v_token where id = p_project;
    end if;

    return v_token;
end;
$$;

create or replace function public.disable_project_sharing(p_project uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    update public.renovation_projects set share_token = null
    where id = p_project and owner_id = auth.uid();
    if not found then
        raise exception 'project not found or not owned by caller';
    end if;
end;
$$;

-- ---------------------------------------------------------------------------
-- Public: resolve a share token → the project payload (read-only). Returns the
-- name/kind/status + building-model geometry + staging placements + edits as a
-- single JSON object. SECURITY DEFINER so anon can read exactly one shared
-- project (matched by the unguessable token) and nothing else.
-- ---------------------------------------------------------------------------
create or replace function public.get_shared_project(p_token text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_proj public.renovation_projects;
    v_geometry jsonb;
    v_placements jsonb;
    v_edits jsonb;
begin
    if p_token is null or length(p_token) < 8 then
        return null;
    end if;

    select * into v_proj
    from public.renovation_projects
    where share_token = p_token
    limit 1;

    if not found then
        return null;
    end if;

    select geometry into v_geometry
    from public.building_models where id = v_proj.building_model_id;

    select coalesce(jsonb_agg(to_jsonb(p) - 'layout_id'), '[]'::jsonb) into v_placements
    from public.staging_placements p
    where p.layout_id = v_proj.staging_layout_id;

    select edits into v_edits
    from public.renovation_plans where id = v_proj.renovation_plan_id;

    return jsonb_build_object(
        'name', v_proj.name,
        'kind', v_proj.kind,
        'status', v_proj.status,
        'geometry', coalesce(v_geometry, '{}'::jsonb),
        'placements', coalesce(v_placements, '[]'::jsonb),
        'edits', coalesce(v_edits, '[]'::jsonb)
    );
end;
$$;

revoke all on function public.enable_project_sharing(uuid) from public;
revoke all on function public.disable_project_sharing(uuid) from public;
grant execute on function public.enable_project_sharing(uuid) to authenticated;
grant execute on function public.disable_project_sharing(uuid) to authenticated;

-- Read-by-token is intentionally public (the token is the capability).
grant execute on function public.get_shared_project(text) to anon, authenticated;
