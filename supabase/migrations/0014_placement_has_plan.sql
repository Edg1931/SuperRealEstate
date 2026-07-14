-- 0014: Explicit blueprint-authored flag on staging placements.
--
-- The web design surface authors placements in blueprint-plane coordinates
-- (plan_x / plan_y). Readers previously had to infer "was this plan-authored?"
-- from non-zero plan coords — which misclassifies a placement legitimately
-- snapped to the plan origin (0,0) as a world/anchor placement. Store the
-- flag instead of guessing.

alter table public.staging_placements
  add column if not exists has_plan boolean not null default false;

-- Backfill: anything with real plan coordinates was plan-authored.
update public.staging_placements
   set has_plan = true
 where plan_x is not null
   and (plan_x <> 0 or plan_y <> 0 or plan_yaw_deg <> 0);
