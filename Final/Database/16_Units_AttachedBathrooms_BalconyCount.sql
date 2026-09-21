

USE Concord_Practicum;
GO

-- HasBalcony was added with an unnamed DEFAULT constraint (14_Units_Balcony_Parking.sql),
-- so it must be dropped before the column itself can be dropped.
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('Units') AND c.name = 'HasBalcony';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Units DROP CONSTRAINT ' + @constraintName);

ALTER TABLE Units DROP COLUMN HasBalcony;
GO

ALTER TABLE Units ADD AttachedBathrooms INT NULL;
GO
ALTER TABLE Units ADD BalconyCount INT NULL DEFAULT 0 WITH VALUES;
GO
