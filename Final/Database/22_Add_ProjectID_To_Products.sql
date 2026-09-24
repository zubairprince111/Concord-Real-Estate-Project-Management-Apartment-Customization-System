
-- USE Concord_Practicum;
GO

-- Project-specific structural materials (Cement, Steel & Rebar, Concrete & Aggregates,
-- Scaffolding & Formwork -- i.e. products under a non-customizable MasterCategory).
--
-- Products.ProjectID NULL  = applies to every project (all existing data, and every
--                            customizable finishing product such as Tiles / Paint).
-- Products.ProjectID set   = only shown in the Customize modal of bookings whose Unit belongs
--                            to that Project (see ClientController.BuildCustomizeViewModel).
--
-- Nullable with no default, so applying this changes nothing for existing rows.
-- Safe to re-run: the column is only added when it doesn't exist yet.

IF COL_LENGTH('Products', 'ProjectID') IS NULL
BEGIN
    ALTER TABLE Products
        ADD ProjectID INT NULL
        CONSTRAINT FK_Products_Projects FOREIGN KEY REFERENCES Projects(ProjectID);
END
GO
