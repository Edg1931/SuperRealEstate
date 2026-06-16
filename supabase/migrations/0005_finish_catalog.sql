-- SuperRealEstate: turn the materials catalog into a finish catalog that
-- AI-recognized surface finishes can resolve to (brand/product/color/buy link),
-- and add the per_gallon unit for paint.

alter table public.materials
    add column if not exists brand        text,
    add column if not exists product_code text,  -- e.g. "SW 7029"
    add column if not exists color_hex    text,
    add column if not exists buy_url      text;

-- Allow paint to be priced per gallon (the inline check from 0001 is
-- auto-named materials_unit_check).
alter table public.materials drop constraint if exists materials_unit_check;
alter table public.materials
    add constraint materials_unit_check
    check (unit in ('per_sqft','per_sqm','per_linft','each','per_gallon'));

create index if not exists idx_materials_brand on public.materials (brand);
create index if not exists idx_materials_product_code on public.materials (product_code);

-- Seed a few real-world named finishes so recognition → product resolution works.
insert into public.materials (name, category, unit, price_per_unit, brand, product_code, color_hex, source) values
    ('Agreeable Gray',  'Paint',    'per_gallon', 45.00, 'Sherwin-Williams', 'SW 7029', '#D1CBC1', 'placeholder'),
    ('Repose Gray',     'Paint',    'per_gallon', 45.00, 'Sherwin-Williams', 'SW 7015', '#CCC9C0', 'placeholder'),
    ('Alabaster',       'Paint',    'per_gallon', 47.00, 'Sherwin-Williams', 'SW 7008', '#EDEAE0', 'placeholder'),
    ('Swiss Coffee',    'Paint',    'per_gallon', 38.00, 'Behr',             '12',      '#EAE3D3', 'placeholder')
on conflict do nothing;
