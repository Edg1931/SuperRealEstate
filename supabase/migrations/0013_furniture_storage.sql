-- 0013: Private `furniture` Storage bucket + owner-scoped object policies.
--
-- Capture photos upload to  furniture/{userId}/{captureId}/photo_###.jpg  and
-- the reconstruction worker writes models under the same per-user prefix.
-- Without these policies the "private per-user" guarantee only existed in
-- comments; with them, a signed-in user can only touch objects whose first
-- path folder is their own auth.uid(). The reconstruction worker uses the
-- service role, which bypasses RLS by design (its photo prefix is validated
-- server-side in the furniture-capture Edge Function).

insert into storage.buckets (id, name, public)
values ('furniture', 'furniture', false)
on conflict (id) do nothing;

drop policy if exists furniture_objects_select on storage.objects;
create policy furniture_objects_select on storage.objects
  for select to authenticated
  using (bucket_id = 'furniture' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists furniture_objects_insert on storage.objects;
create policy furniture_objects_insert on storage.objects
  for insert to authenticated
  with check (bucket_id = 'furniture' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists furniture_objects_update on storage.objects;
create policy furniture_objects_update on storage.objects
  for update to authenticated
  using (bucket_id = 'furniture' and (storage.foldername(name))[1] = auth.uid()::text)
  with check (bucket_id = 'furniture' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists furniture_objects_delete on storage.objects;
create policy furniture_objects_delete on storage.objects
  for delete to authenticated
  using (bucket_id = 'furniture' and (storage.foldername(name))[1] = auth.uid()::text);
