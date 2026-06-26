-- SuperRealEstate: security hardening (see docs/SECURITY-AUDIT.md).
--
-- FIX (high): the original `participants_self_insert` policy let ANY
-- authenticated user insert themselves into ANY session by id, gaining read
-- access to that session's layout/furniture and edit rights on placements
-- (session enumeration → unauthorized access). Replace open self-join with
-- host-managed participants + an invite-code redeem function.

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------------
-- Session invites: the host shares a code; joining requires a valid one.
-- ---------------------------------------------------------------------------
create table if not exists public.session_invites (
    id         uuid primary key default gen_random_uuid(),
    session_id uuid not null references public.sessions (id) on delete cascade,
    code       text not null unique,
    created_by uuid not null references auth.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    expires_at timestamptz
);

create index if not exists idx_session_invites_code on public.session_invites (code);

alter table public.session_invites enable row level security;

-- Only the session's host can create/see/revoke its invites.
drop policy if exists session_invites_host on public.session_invites;
create policy session_invites_host on public.session_invites
    for all to authenticated
    using (exists (select 1 from public.sessions s where s.id = session_invites.session_id and s.host_id = auth.uid()))
    with check (exists (select 1 from public.sessions s where s.id = session_invites.session_id and s.host_id = auth.uid()));

-- ---------------------------------------------------------------------------
-- Redeem an invite → add the caller as a participant. SECURITY DEFINER so it
-- can insert past RLS, but only after validating a live invite code.
-- ---------------------------------------------------------------------------
create or replace function public.join_session(p_code text, p_device_kind text default null)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
    v_session uuid;
begin
    select session_id into v_session
    from public.session_invites
    where code = p_code
      and (expires_at is null or expires_at > now())
    limit 1;

    if v_session is null then
        raise exception 'invalid or expired invite code';
    end if;

    insert into public.session_participants (session_id, user_id, role, device_kind)
    values (v_session, auth.uid(), 'buyer', p_device_kind)
    on conflict (session_id, user_id) do nothing;

    return v_session;
end;
$$;

revoke all on function public.join_session(text, text) from public;
grant execute on function public.join_session(text, text) to authenticated;

-- ---------------------------------------------------------------------------
-- Replace the open self-insert: host can add participants directly; everyone
-- else joins only via join_session(code). Leaving (self-delete) is unchanged.
-- ---------------------------------------------------------------------------
drop policy if exists participants_self_insert on public.session_participants;

drop policy if exists participants_host_insert on public.session_participants;
create policy participants_host_insert on public.session_participants
    for insert to authenticated
    with check (exists (
        select 1 from public.sessions s
        where s.id = session_participants.session_id and s.host_id = auth.uid()
    ));
