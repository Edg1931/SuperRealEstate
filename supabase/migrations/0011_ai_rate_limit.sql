-- SuperRealEstate: per-user AI rate limiting (cost + abuse control).
--
-- Now that the AI Edge Functions send the signed-in user's JWT (not the anon
-- key), we can attribute and cap paid model calls per user. A tiny per-user,
-- per-day counter incremented by a SECURITY DEFINER function the functions call
-- with the service role. RLS is on with NO policies, so only the service role /
-- definer function can read or write it.

create table if not exists public.ai_usage (
    user_id uuid not null references auth.users (id) on delete cascade,
    day     date not null default (now() at time zone 'utc')::date,
    count   integer not null default 0,
    primary key (user_id, day)
);

alter table public.ai_usage enable row level security;
-- (No policies — the table is service-role / definer-only.)

create index if not exists idx_ai_usage_day on public.ai_usage (day);

-- ---------------------------------------------------------------------------
-- Increment today's count for a user and report whether they're still under the
-- limit. Returns TRUE when allowed (count <= limit after increment), FALSE when
-- the call puts them over. Called by Edge Functions with the service role.
-- ---------------------------------------------------------------------------
create or replace function public.increment_ai_usage(p_user uuid, p_limit integer)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
    v_count integer;
begin
    insert into public.ai_usage (user_id, day, count)
    values (p_user, (now() at time zone 'utc')::date, 1)
    on conflict (user_id, day) do update set count = public.ai_usage.count + 1
    returning count into v_count;

    return v_count <= p_limit;
end;
$$;

revoke all on function public.increment_ai_usage(uuid, integer) from public;
grant execute on function public.increment_ai_usage(uuid, integer) to service_role;
