

USE Concord_Practicum;
GO

-- ============================================================
-- 1. Buildings  (NEW table)
-- ============================================================
CREATE TABLE Buildings (
    BuildingID      INT IDENTITY(1,1) PRIMARY KEY,
    ProjectID       INT             NOT NULL FOREIGN KEY REFERENCES Projects(ProjectID),
    BuildingName    NVARCHAR(100)   NOT NULL,
    TotalFloors     INT             NOT NULL,
    CreatedDate     DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Buildings_Project_Name UNIQUE (ProjectID, BuildingName)
);
GO

-- ============================================================
-- 2. Units.BuildingID  (nullable first, so existing rows can be backfilled)
-- ============================================================
ALTER TABLE Units ADD BuildingID INT NULL;
GO

-- ============================================================
-- 3. Backfill: one default Building per Project that already has Units
-- ============================================================
INSERT INTO Buildings (ProjectID, BuildingName, TotalFloors)
SELECT DISTINCT ProjectID, 'Main Building', 10
FROM Units u
WHERE NOT EXISTS (SELECT 1 FROM Buildings b WHERE b.ProjectID = u.ProjectID);
GO

UPDATE u
SET u.BuildingID = b.BuildingID
FROM Units u
JOIN Buildings b ON b.ProjectID = u.ProjectID AND b.BuildingName = 'Main Building'
WHERE u.BuildingID IS NULL;
GO

-- ============================================================
-- 4. Lock down BuildingID (NOT NULL + FK)
-- ============================================================
ALTER TABLE Units ALTER COLUMN BuildingID INT NOT NULL;
GO
ALTER TABLE Units ADD CONSTRAINT FK_Units_Buildings FOREIGN KEY (BuildingID) REFERENCES Buildings(BuildingID);
GO

-- ============================================================
-- 5. Rename UnitNumber -> FlatNumber, rescope uniqueness to (BuildingID, FlatNumber)
-- ============================================================
ALTER TABLE Units DROP CONSTRAINT UQ_Units_Project_Number;
GO
EXEC sp_rename 'Units.UnitNumber', 'FlatNumber', 'COLUMN';
GO
ALTER TABLE Units ADD CONSTRAINT UQ_Units_Building_Flat UNIQUE (BuildingID, FlatNumber);
GO

-- ============================================================
-- 6. New Unit attribute columns
-- ============================================================
ALTER TABLE Units ADD FloorNumber    INT           NOT NULL DEFAULT 0;
ALTER TABLE Units ADD BedroomCount   INT           NOT NULL DEFAULT 0;
ALTER TABLE Units ADD BathroomCount  INT           NOT NULL DEFAULT 0;
ALTER TABLE Units ADD HasKitchen     BIT           NOT NULL DEFAULT 1;
ALTER TABLE Units ADD HasHall        BIT           NOT NULL DEFAULT 1;
ALTER TABLE Units ADD Facing         NVARCHAR(20)  NULL;
GO
ALTER TABLE Units ADD CONSTRAINT CK_Units_Facing
    CHECK (Facing IS NULL OR Facing IN ('North','South','East','West','North-East','North-West','South-East','South-West'));
GO
