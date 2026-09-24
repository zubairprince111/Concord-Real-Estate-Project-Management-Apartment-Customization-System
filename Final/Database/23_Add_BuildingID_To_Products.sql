
-- USE Concord_Practicum;
GO

-- Building-level scoping for structural materials (products under a non-customizable
-- MasterCategory), extending 22_Add_ProjectID_To_Products.sql.
--
-- Products.BuildingID NULL = applies to the whole Project (or to all projects when ProjectID is
--                            NULL too) -- every existing row.
-- Products.BuildingID set  = only shown for bookings whose Unit is in that Building. It is set
--                            together with ProjectID (the Building's own project).
--
-- ClientController.BuildCustomizeViewModel resolves the most specific match per category:
-- Building -> Project (BuildingID NULL) -> global (ProjectID and BuildingID NULL).
--
-- Nullable with no default, so applying this changes nothing for existing rows.
-- Safe to re-run: the column is only added when it doesn't exist yet.

IF COL_LENGTH('Products', 'BuildingID') IS NULL
BEGIN
    ALTER TABLE Products
        ADD BuildingID INT NULL
        CONSTRAINT FK_Products_Buildings FOREIGN KEY REFERENCES Buildings(BuildingID);
END
GO
