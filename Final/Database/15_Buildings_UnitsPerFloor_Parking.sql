

USE Concord_Practicum;
GO

ALTER TABLE Buildings ADD UnitsPerFloor INT NULL;
GO
ALTER TABLE Buildings ADD HasParkingFacility BIT NOT NULL DEFAULT 0;
GO
