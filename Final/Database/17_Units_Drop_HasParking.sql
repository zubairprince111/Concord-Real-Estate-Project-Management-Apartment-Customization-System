

USE Concord_Practicum;
GO

-- Unit-level parking was redundant with Buildings.HasParkingFacility (15_Buildings_UnitsPerFloor_Parking.sql),
-- which already says whether the building has shared parking. HasParking was added with an
-- unnamed DEFAULT constraint (14_Units_Balcony_Parking.sql), so it must be dropped first.
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('Units') AND c.name = 'HasParking';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Units DROP CONSTRAINT ' + @constraintName);

ALTER TABLE Units DROP COLUMN HasParking;
GO
