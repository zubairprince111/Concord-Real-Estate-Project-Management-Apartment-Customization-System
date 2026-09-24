-- USE Concord_Practicum;
GO

-- ============================================================
-- MasterCategories  (Step 1 of the Master Category feature --
-- database foundation + Admin management page only. Later
-- steps wire this into Vendor's product form, Client pricing,
-- Requisition dropdowns, and PM approval.)
-- ============================================================
CREATE TABLE MasterCategories (
    CategoryID      INT             IDENTITY(1,1) PRIMARY KEY,
    CategoryName    NVARCHAR(100)   NOT NULL,
    IsCustomizable  BIT             NOT NULL DEFAULT 0
);
GO

INSERT INTO MasterCategories (CategoryName, IsCustomizable) VALUES
('Tiles', 1), ('Paint', 1), ('Electrical', 0), ('Cement', 0), ('Sanitary Fittings', 1),
('Plumbing', 0), ('Steel & Rebar', 0), ('Doors & Windows', 1);
GO

-- Purely additive: CategoryID stays NULL-able and existing Products rows are
-- NOT backfilled, so every current Vendor/Requisition/Customization-Catalog
-- flow keeps working unchanged until a later step wires this in.
ALTER TABLE Products ADD CategoryID INT NULL FOREIGN KEY REFERENCES MasterCategories(CategoryID);
ALTER TABLE Products ADD IsDefaultOption BIT NOT NULL DEFAULT 0;
ALTER TABLE Products ADD ExtraCost DECIMAL(18,2) NOT NULL DEFAULT 0;
GO
