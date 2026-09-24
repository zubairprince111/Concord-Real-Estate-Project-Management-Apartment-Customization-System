-- USE Concord_Practicum;
GO

-- ============================================================
-- MasterCategories expansion -- broadens the Step 1 seed list to
-- comprehensively cover what's needed to build a residential/
-- commercial building. Idempotent: skips any name that already
-- exists, safe to re-run.
--
-- IsCustomizable judgment call: categories are aesthetic/finish
-- choices a Client would pick (Flooring, Ceiling, Bathroom/Kitchen
-- Fittings, Lighting Fixtures, Glass & Glazing, Landscaping) vs.
-- structural/infrastructure categories the Client doesn't choose
-- (Roofing, Wiring & Cables, HVAC, Insulation, Hardware & Fasteners,
-- Waterproofing, Scaffolding & Formwork, Concrete & Aggregates,
-- Wood & Timber). 'Ceiling' wasn't marked either way in the request;
-- defaulted to customizable (1) as a finish category, matching the
-- pattern above.
-- ============================================================
INSERT INTO MasterCategories (CategoryName, IsCustomizable)
SELECT v.CategoryName, v.IsCustomizable
FROM (VALUES
    ('Flooring', 1),
    ('Roofing', 0),
    ('Ceiling', 1),
    ('Bathroom Fittings', 1),
    ('Kitchen Fittings', 1),
    ('Wiring & Cables', 0),
    ('Lighting Fixtures', 1),
    ('HVAC / Air Conditioning', 0),
    ('Insulation', 0),
    ('Glass & Glazing', 1),
    ('Hardware & Fasteners', 0),
    ('Waterproofing', 0),
    ('Scaffolding & Formwork', 0),
    ('Concrete & Aggregates', 0),
    ('Wood & Timber', 0),
    ('Landscaping', 1)
) AS v(CategoryName, IsCustomizable)
WHERE NOT EXISTS (
    SELECT 1 FROM MasterCategories mc WHERE mc.CategoryName = v.CategoryName
);
GO
