-- Allow the public web companion to browse the catalog without sign-in.
-- The catalog (materials + vendors) is marketing data, safe to read publicly.
-- Private data (properties, rooms, staging, renovations) stays behind auth.

drop policy if exists materials_public_read on public.materials;
create policy materials_public_read on public.materials
    for select to anon using (true);

drop policy if exists vendors_public_read on public.vendors;
create policy vendors_public_read on public.vendors
    for select to anon using (true);

drop policy if exists catalog_items_public_read on public.vendor_catalog_items;
create policy catalog_items_public_read on public.vendor_catalog_items
    for select to anon using (true);
