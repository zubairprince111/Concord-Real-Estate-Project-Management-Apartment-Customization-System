

USE Concord_Practicum;
GO

INSERT INTO Units (ProjectID, UnitNumber, SizeSqFt, BasePrice, Status)
SELECT p.ProjectID, x.UnitNumber, x.SizeSqFt, x.BasePrice, 'Available'
FROM Projects p
CROSS APPLY (VALUES
    ('A-101', 1200.00,  9000000.00),
    ('A-102', 1450.00, 10800000.00),
    ('B-201', 1800.00, 13500000.00),
    ('B-202', 2000.00, 15000000.00)
) AS x(UnitNumber, SizeSqFt, BasePrice)
WHERE p.ProjectName = 'Concord Heights - Phase 1'
  AND NOT EXISTS (SELECT 1 FROM Units u WHERE u.ProjectID = p.ProjectID AND u.UnitNumber = x.UnitNumber);
GO
