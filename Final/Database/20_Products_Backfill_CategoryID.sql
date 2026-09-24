
-- USE Concord_Practicum;
GO

-- BuildCustomizeViewModel (ClientController) INNER JOINs Products to MasterCategories on
-- CategoryID, so a Product with CategoryID = NULL never shows up in the Customize modal.
-- This script (1) seeds the four sample products on a fresh setup -- with CategoryID set --
-- and (2) backfills CategoryID on any existing Product that still has NULL.
--
-- Safe to re-run: the seed only runs when Products is empty, and the backfill only touches
-- rows where CategoryID IS NULL.

-- 1. Sample stock for a fresh setup (moved here from 01_Sample_Data.sql, which runs before
--    MasterCategories/Products.CategoryID exist and before Supplier/Quantity/Unit were dropped).
IF NOT EXISTS (SELECT 1 FROM Products)
BEGIN
    INSERT INTO Products (VendorID, ProductName, Description, Price, Category, CategoryID, IsApprovedForClientCustomization)
    SELECT NULL, x.ProductName, x.Description, x.Price,
           CASE WHEN mc.IsCustomizable = 1 THEN 'Customizable' ELSE 'Non-customizable' END,
           mc.CategoryID, mc.IsCustomizable
    FROM (VALUES
        ('Portland Cement (50kg)',          'OPC 52.5 grade, 50kg multi-wall paper bag',            650.00,  'Cement'),
        ('Reinforcement Steel Rod (12mm)',  '12mm deformed TMT bar, Grade 500W, 12m length',        95000.00,'Steel & Rebar'),
        ('Ceramic Floor Tile',              'Glazed ceramic, 600x600mm, matte finish',              120.00,  'Tiles'),
        ('Interior Wall Paint',             'Water-based emulsion, low-VOC, matte finish',          2200.00, 'Paint')
    ) AS x(ProductName, Description, Price, CategoryName)
    JOIN MasterCategories mc ON mc.CategoryName = x.CategoryName;
END
GO

-- 2. Backfill: match each NULL-CategoryID product to a MasterCategory by name.
--    Category / IsApprovedForClientCustomization follow the matched category's IsCustomizable
--    flag, the same starting state AdminController.AddProduct gives a new product.
DECLARE @map TABLE (NamePattern NVARCHAR(100), CategoryName NVARCHAR(100));
-- Cement is matched on a word boundary ('Cement%' / '% Cement%') -- a bare '%Cement%' would
-- also hit "Reinforcement".
INSERT INTO @map VALUES
    ('Cement%',   'Cement'),
    ('% Cement%', 'Cement'),
    ('%Steel%',   'Steel & Rebar'),
    ('%Rebar%',   'Steel & Rebar'),
    ('%Tile%',    'Tiles'),
    ('%Paint%',   'Paint');

UPDATE p
SET p.CategoryID = mc.CategoryID,
    p.Category = CASE WHEN mc.IsCustomizable = 1 THEN 'Customizable' ELSE 'Non-customizable' END,
    p.IsApprovedForClientCustomization = mc.IsCustomizable
FROM Products p
CROSS APPLY (SELECT TOP 1 m.CategoryName FROM @map m WHERE p.ProductName LIKE m.NamePattern ORDER BY m.CategoryName) hit
JOIN MasterCategories mc ON mc.CategoryName = hit.CategoryName
WHERE p.CategoryID IS NULL;

-- Anything still NULL had no name match and needs a category assigned by hand.
SELECT ProductID, ProductName AS StillUncategorised FROM Products WHERE CategoryID IS NULL;
GO
