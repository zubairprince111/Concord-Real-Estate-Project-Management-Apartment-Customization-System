
-- USE Concord_Practicum;
GO

-- Which of the Admin's catalog products each Project / Building offers to its clients.
-- Set by the Project Manager (MaterialAssignmentController); read by the Customize modal
-- (ClientController.BuildCustomizeViewModel). A junction table rather than columns on Products
-- because one catalog item (e.g. "OPC 52.5 cement") can be assigned to Building A and Building B
-- independently.
--
--   ProjectID set,  BuildingID NULL  = the whole project
--   ProjectID set,  BuildingID set   = that one building only
--   ProjectID NULL, BuildingID NULL  = global (only the pre-existing legacy structural products;
--                                      the PM screen never creates one)
--
-- Structural (non-customizable) categories: at most ONE product per Category + Project + Building --
-- enforced by MaterialAssignmentController.SaveStructural (replace, never add).
-- Customizable categories: any number of variants per scope; a building sees the union of its own
-- and its project's.
--
-- Products.ProjectID / Products.BuildingID (scripts 22/23) are no longer read or written by the app;
-- they are kept (not dropped) and their data is copied here below.
--
-- Safe to re-run: the table is only created when missing, and the backfill only runs while the
-- table is empty.

IF OBJECT_ID('ProductScopeAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE ProductScopeAssignments (
        AssignmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductScopeAssignments PRIMARY KEY,
        ProductID    INT NOT NULL CONSTRAINT FK_PSA_Products  FOREIGN KEY REFERENCES Products(ProductID),
        ProjectID    INT NULL     CONSTRAINT FK_PSA_Projects  FOREIGN KEY REFERENCES Projects(ProjectID),
        BuildingID   INT NULL     CONSTRAINT FK_PSA_Buildings FOREIGN KEY REFERENCES Buildings(BuildingID),
        AssignedDate DATETIME NOT NULL CONSTRAINT DF_PSA_AssignedDate DEFAULT GETDATE()
    );

    -- The same product can't be assigned twice to the same scope (NULLs compare equal in a unique index).
    CREATE UNIQUE INDEX UX_PSA_Product_Scope ON ProductScopeAssignments (ProductID, ProjectID, BuildingID);
END
GO

-- Backfill so every existing project keeps offering exactly what it offers today.
IF NOT EXISTS (SELECT 1 FROM ProductScopeAssignments)
BEGIN
    -- Structural products: copy each one's existing Project/Building scope (NULL/NULL = the legacy global one).
    INSERT INTO ProductScopeAssignments (ProductID, ProjectID, BuildingID)
    SELECT p.ProductID, p.ProjectID, p.BuildingID
    FROM Products p
    JOIN MasterCategories mc ON mc.CategoryID = p.CategoryID
    WHERE mc.IsCustomizable = 0;

    -- Customizable variants the Client could pick today (approved): offered project-wide to every
    -- project (or only to the project the product was scoped to, if it had one).
    INSERT INTO ProductScopeAssignments (ProductID, ProjectID, BuildingID)
    SELECT p.ProductID, pj.ProjectID, NULL
    FROM Products p
    JOIN MasterCategories mc ON mc.CategoryID = p.CategoryID
    CROSS JOIN Projects pj
    WHERE mc.IsCustomizable = 1
      AND p.Category = 'Customizable' AND p.IsApprovedForClientCustomization = 1
      AND (p.ProjectID IS NULL OR p.ProjectID = pj.ProjectID);
END
GO
