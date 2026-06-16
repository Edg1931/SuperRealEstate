-- Seed a starter materials catalog. Prices are placeholder USD figures meant to
-- be edited to match local Lowe's/Home Depot pricing — there is no official
-- retailer pricing API, so keep these current manually.

insert into public.materials (name, category, unit, price_per_unit, source) values
    ('Laminate Flooring',        'Flooring', 'per_sqft', 2.49, 'placeholder'),
    ('Luxury Vinyl Plank (LVP)', 'Flooring', 'per_sqft', 3.29, 'placeholder'),
    ('Engineered Hardwood',      'Flooring', 'per_sqft', 5.99, 'placeholder'),
    ('Porcelain Tile',           'Flooring', 'per_sqft', 3.99, 'placeholder'),
    ('Carpet (mid-grade)',       'Flooring', 'per_sqft', 2.19, 'placeholder'),
    ('Interior Paint (eggshell)','Paint',    'per_sqft', 0.45, 'placeholder'),
    ('Primer',                   'Paint',    'per_sqft', 0.30, 'placeholder'),
    ('Ceiling Paint (flat)',     'Paint',    'per_sqft', 0.40, 'placeholder'),
    ('Drywall (1/2 in)',         'Drywall',  'per_sqft', 0.65, 'placeholder'),
    ('Baseboard Trim',           'Trim',     'per_linft', 1.89, 'placeholder'),
    ('Crown Molding',            'Trim',     'per_linft', 3.49, 'placeholder')
on conflict do nothing;
