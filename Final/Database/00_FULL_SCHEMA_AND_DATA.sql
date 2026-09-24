-- CONCORD REAL ESTATE ERP - FULL COMBINED DATABASE SCHEMA & DATA
-- Execute this script in your cloud database query window

-- ==========================================
-- SECTION: 00_MASTER_Setup.sql
-- ==========================================

/*
    Concord Real Estate Project Management System
    00_MASTER_Setup.sql

    Baseline schema for a FRESH database. Run this first, then 01 .. 27 in numeric order
    (there is no script 21).

    This baseline only creates the tables the application still uses. The earlier
    procurement design (Vendors, Requisitions, RequisitionItems, Approvals, Employees,
    VendorPayments) is gone and is not created here. A few legacy columns/values that the
    numbered scripts still reference are removed again by 27_Drop_Legacy_Objects.sql:
    Products.VendorID (plain nullable column, no foreign key), Products.CreatedDate and the
    Vendor / Site Engineer / HR Executive roles in the Users.Role CHECK.

    Seeded logins (all roles, password = Test@123):
        admin@concord.test           / Test@123   (Admin)
        client@concord.test          / Test@123   (Client)
        pm@concord.test              / Test@123   (Project Manager)
        accounts@concord.test        / Test@123   (Accounts Officer)
*/

-- Database table creation script (runs inside current database context)


-- ============================================================
-- 1. Users
-- ============================================================
CREATE TABLE Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100)   NOT NULL,
    Email           NVARCHAR(100)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(64)    NOT NULL,
    Role            NVARCHAR(30)    NOT NULL
                        CHECK (Role IN ('Admin','HR Executive','Client','Site Engineer',
                                        'Project Manager','Vendor','Accounts Officer')),
    Phone           NVARCHAR(20)    NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedDate     DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 2. Projects
-- ============================================================
CREATE TABLE Projects (
    ProjectID       INT IDENTITY(1,1) PRIMARY KEY,
    ProjectName     NVARCHAR(150)   NOT NULL,
    Location        NVARCHAR(200)   NULL,
    Budget          DECIMAL(18,2)   NOT NULL,
    StartDate       DATE            NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Active'
                        CHECK (Status IN ('Active','Completed','OnHold')),
    CreatedByUserID INT             NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    CreatedDate     DATETIME        NOT NULL DEFAULT GETDATE()
    -- MaxBuildings / TotalAreaSqFt are added by 07_Project_MaxBuildings_TotalArea.sql
);
GO

-- ============================================================
-- 3. Products  (the Admin's catalog; Category drives client customization rules)
--    VendorID is a leftover from the Vendor design: a plain nullable column with no foreign key,
--    kept only so 13_Trim_To_Four_Actors.sql still applies. 27_Drop_Legacy_Objects.sql drops it.
-- ============================================================
CREATE TABLE Products (
    ProductID       INT IDENTITY(1,1) PRIMARY KEY,
    VendorID        INT             NULL,
    ProductName     NVARCHAR(150)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    Unit            NVARCHAR(30)    NOT NULL,
    Quantity        INT             NOT NULL DEFAULT 0,
    Price           DECIMAL(18,2)   NOT NULL,
    Category        NVARCHAR(20)    NOT NULL
                        CHECK (Category IN ('Customizable','Non-customizable')),
    -- Set together with Category (both follow the category's IsCustomizable flag), so the two
    -- fields can never disagree.
    IsApprovedForClientCustomization BIT NOT NULL DEFAULT 0,
    CreatedDate     DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 4. Units  (flats/apartments within a Project)
-- ============================================================
CREATE TABLE Units (
    UnitID          INT IDENTITY(1,1) PRIMARY KEY,
    ProjectID       INT             NOT NULL FOREIGN KEY REFERENCES Projects(ProjectID),
    UnitNumber      NVARCHAR(20)    NOT NULL,
    SizeSqFt        DECIMAL(18,2)   NOT NULL,
    BasePrice       DECIMAL(18,2)   NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Available'
                        CHECK (Status IN ('Available','Booked')),
    CONSTRAINT UQ_Units_Project_Number UNIQUE (ProjectID, UnitNumber)
);
GO

-- ============================================================
-- 5. UnitBookings  (Client books a Unit)
-- ============================================================
CREATE TABLE UnitBookings (
    BookingID       INT IDENTITY(1,1) PRIMARY KEY,
    UnitID          INT             NOT NULL FOREIGN KEY REFERENCES Units(UnitID),
    ClientUserID    INT             NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    BookingDate     DATETIME        NOT NULL DEFAULT GETDATE(),
    TotalPrice      DECIMAL(18,2)   NOT NULL
);
GO

-- ============================================================
-- 6. CustomizationSelections  (Client's chosen customizable Products for a booking)
-- ============================================================
CREATE TABLE CustomizationSelections (
    SelectionID     INT IDENTITY(1,1) PRIMARY KEY,
    BookingID       INT             NOT NULL FOREIGN KEY REFERENCES UnitBookings(BookingID),
    ProductID       INT             NOT NULL FOREIGN KEY REFERENCES Products(ProductID),
    SelectedDate    DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 7. ClientPayments  (Accounts Officer records a Client's installment payment)
-- ============================================================
CREATE TABLE ClientPayments (
    ClientPaymentID     INT IDENTITY(1,1) PRIMARY KEY,
    BookingID            INT             NOT NULL FOREIGN KEY REFERENCES UnitBookings(BookingID),
    ProjectID            INT             NOT NULL FOREIGN KEY REFERENCES Projects(ProjectID),
    Amount                DECIMAL(18,2)   NOT NULL,
    PaymentDate           DATETIME        NOT NULL DEFAULT GETDATE(),
    RecordedByUserID      INT             NOT NULL FOREIGN KEY REFERENCES Users(UserID)
);
GO

-- ============================================================
-- Seed: 4 test logins (Admin, Client, Project Manager, Accounts Officer),
-- password = Test@123 for all.
-- Hash = SHA256("Test@123") as lowercase hex, matches Security/PasswordHasher.cs
-- ============================================================
DECLARE @Hash NVARCHAR(64) = '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e';

INSERT INTO Users (FullName, Email, PasswordHash, Role) VALUES
    ('Concord Admin',        'admin@concord.test',        @Hash, 'Admin'),
    ('Concord Client',       'client@concord.test',       @Hash, 'Client'),
    ('Concord Proj Manager', 'pm@concord.test',           @Hash, 'Project Manager'),
    ('Concord Accounts',     'accounts@concord.test',     @Hash, 'Accounts Officer');
GO


GO

-- ==========================================
-- SECTION: 01_Sample_Data.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Sample Active project, created by the seeded Admin user
INSERT INTO Projects (ProjectName, Location, Budget, StartDate, Status, CreatedByUserID)
SELECT 'Concord Heights - Phase 1', 'Gulshan, Dhaka', 50000000.00, '2026-01-15', 'Active', UserID
FROM Users WHERE Email = 'admin@concord.test';
GO

-- Sample products are seeded by Database\20_Products_Backfill_CategoryID.sql instead: a
-- Product needs a CategoryID (MasterCategories, created in 10) to show up in the Client's
-- Customize modal, and Supplier/Quantity/Unit no longer exist by the time 20 runs.


GO

-- ==========================================
-- SECTION: 02_Sample_Units.sql
-- ==========================================



-- USE Concord_Practicum;
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


GO

-- ==========================================
-- SECTION: 03_Buildings_And_Units.sql
-- ==========================================



-- USE Concord_Practicum;
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


GO

-- ==========================================
-- SECTION: 04_Customization_Rejection.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE CustomizationSelections ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Pending'
    CONSTRAINT CK_CustomizationSelections_Status CHECK (Status IN ('Pending','Approved','Rejected'));
GO

ALTER TABLE CustomizationSelections ADD Remarks NVARCHAR(500) NULL;
GO


GO

-- ==========================================
-- SECTION: 05_Project_Completion_Date.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE Projects ADD EstimatedCompletionDate DATE NULL;
GO


GO

-- ==========================================
-- SECTION: 06_Building_Completion_Date.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE Buildings ADD EstimatedCompletionDate DATE NULL;
GO


GO

-- ==========================================
-- SECTION: 07_Project_MaxBuildings_TotalArea.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE Projects ADD MaxBuildings INT NULL;
GO
ALTER TABLE Projects ADD TotalAreaSqFt DECIMAL(18,2) NULL;
GO


GO

-- ==========================================
-- SECTION: 08_Project_Milestones.sql
-- ==========================================



-- USE Concord_Practicum;
GO

-- ============================================================
-- ProjectMilestones  (monthly progress checklist per Project,
-- ticked off by the assigned Site Engineer)
-- ============================================================
CREATE TABLE ProjectMilestones (
    MilestoneID         INT             IDENTITY(1,1) PRIMARY KEY,
    ProjectID            INT             NOT NULL FOREIGN KEY REFERENCES Projects(ProjectID),
    MonthNumber           INT             NOT NULL,
    IsCompleted            BIT             NOT NULL DEFAULT 0,
    CompletedDate           DATETIME        NULL,
    CompletedByUserID        INT             NULL FOREIGN KEY REFERENCES Users(UserID),
    CONSTRAINT UQ_ProjectMilestones_Project_Month UNIQUE (ProjectID, MonthNumber)
);
GO


GO

-- ==========================================
-- SECTION: 09_Milestone_Description.sql
-- ==========================================

-- USE Concord_Practicum;
GO

-- ============================================================
-- ProjectMilestones.Description  (free-text notes on what work
-- is planned/needed or was actually done for that month)
-- ============================================================
ALTER TABLE ProjectMilestones ADD Description NVARCHAR(500) NULL;
GO


GO

-- ==========================================
-- SECTION: 10_Master_Categories.sql
-- ==========================================

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


GO

-- ==========================================
-- SECTION: 11_More_Master_Categories.sql
-- ==========================================

-- USE Concord_Practicum;
GO

-- ============================================================
-- MasterCategories expansion -- broadens the Step 1 seed list to
-- comprehensively cover what's needed to build a residential/
-- commercial building. Idempotent: skips any name that already
-- exists, safe to re-run.
--
-- IsCustomizable judgment call: categories are aesthetic/finish
-- choices a Client would pick (Flooring, Ceiling, Bathroom/Kitchen
-- Fittings, Lighting Fixtures, Glass & Glazing, Landscaping) vs.
-- structural/infrastructure categories the Client doesn't choose
-- (Roofing, Wiring & Cables, HVAC, Insulation, Hardware & Fasteners,
-- Waterproofing, Scaffolding & Formwork, Concrete & Aggregates,
-- Wood & Timber). 'Ceiling' wasn't marked either way in the request;
-- defaulted to customizable (1) as a finish category, matching the
-- pattern above.
-- ============================================================
INSERT INTO MasterCategories (CategoryName, IsCustomizable)
SELECT v.CategoryName, v.IsCustomizable
FROM (VALUES
    ('Flooring', 1),
    ('Roofing', 0),
    ('Ceiling', 1),
    ('Bathroom Fittings', 1),
    ('Kitchen Fittings', 1),
    ('Wiring & Cables', 0),
    ('Lighting Fixtures', 1),
    ('HVAC / Air Conditioning', 0),
    ('Insulation', 0),
    ('Glass & Glazing', 1),
    ('Hardware & Fasteners', 0),
    ('Waterproofing', 0),
    ('Scaffolding & Formwork', 0),
    ('Concrete & Aggregates', 0),
    ('Wood & Timber', 0),
    ('Landscaping', 1)
) AS v(CategoryName, IsCustomizable)
WHERE NOT EXISTS (
    SELECT 1 FROM MasterCategories mc WHERE mc.CategoryName = v.CategoryName
);
GO


GO

-- ==========================================
-- SECTION: 12_Booking_Cancellation_And_Sold_Status.sql
-- ==========================================

-- USE Concord_Practicum;
GO

-- ============================================================
-- UnitBookings.Status ('Active'/'Cancelled') + CancelReason
-- Lets Admin cancel a booking (AdminController.CancelBooking) without deleting
-- its history -- cancelled bookings just stop counting as this client's active
-- booking (ClientController.MyBookings only lists Status = 'Active').
-- ============================================================
ALTER TABLE UnitBookings ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Active'
    CONSTRAINT CK_UnitBookings_Status CHECK (Status IN ('Active','Cancelled'));
GO

ALTER TABLE UnitBookings ADD CancelReason NVARCHAR(500) NULL;
GO

-- ============================================================
-- Units.Status: allow 'Sold' alongside 'Available'/'Booked'
-- (AccountsController.RecordClientPayment flips Booked -> Sold once a booking
-- is fully paid). The original CHECK constraint from 00_MASTER_Setup.sql was
-- unnamed, so its system-generated name is looked up before dropping it.
-- ============================================================
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON col.object_id = cc.parent_object_id AND col.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('Units') AND col.name = 'Status';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Units DROP CONSTRAINT [' + @constraintName + ']');
GO

ALTER TABLE Units ADD CONSTRAINT CK_Units_Status CHECK (Status IN ('Available','Booked','Sold'));
GO


GO

-- ==========================================
-- SECTION: 13_Trim_To_Four_Actors.sql
-- ==========================================

-- USE Concord_Practicum;
GO

-- Make VendorID optional on Products since Vendor accounts are gone
ALTER TABLE Products ALTER COLUMN VendorID INT NULL;
GO

-- Add a simple text field for who supplies a product (replaces the Vendor relationship)
ALTER TABLE Products ADD Supplier NVARCHAR(150) NULL;
GO

-- Requisitions/Approvals/VendorPayments/VendorOrders, the HR table (Employees), and the
-- ProjectMilestones checklist (cut in this same pass -- construction-progress tracking had
-- no owner left once Site Engineer was removed) are no longer referenced by any controller.
-- Confirm no other object depends on them (check with sp_depends or the DB's "View
-- Dependencies") then drop:
-- DROP TABLE RequisitionItems;
-- DROP TABLE Approvals;      -- NOTE: this table name is also used nowhere else; the
--                             -- kept Customizations approval flow writes to
--                             -- CustomizationSelections directly, not to Approvals.
-- DROP TABLE Requisitions;
-- DROP TABLE VendorPayments;
-- DROP TABLE Vendors;
-- DROP TABLE Employees;      -- the HR staff-assignment table (Site Engineer + Project Manager)
-- DROP TABLE ProjectMilestones;

-- Uncomment the DROP TABLE lines above one at a time, after confirming in
-- SSMS that nothing else references each table.


GO

-- ==========================================
-- SECTION: 14_Units_Balcony_Parking.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE Units ADD HasBalcony BIT NULL DEFAULT 0 WITH VALUES;
GO
ALTER TABLE Units ADD HasParking BIT NULL DEFAULT 0 WITH VALUES;
GO


GO

-- ==========================================
-- SECTION: 15_Buildings_UnitsPerFloor_Parking.sql
-- ==========================================



-- USE Concord_Practicum;
GO

ALTER TABLE Buildings ADD UnitsPerFloor INT NULL;
GO
ALTER TABLE Buildings ADD HasParkingFacility BIT NOT NULL DEFAULT 0;
GO


GO

-- ==========================================
-- SECTION: 16_Units_AttachedBathrooms_BalconyCount.sql
-- ==========================================



-- USE Concord_Practicum;
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


GO

-- ==========================================
-- SECTION: 17_Units_Drop_HasParking.sql
-- ==========================================



-- USE Concord_Practicum;
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


GO

-- ==========================================
-- SECTION: 18_Products_Drop_Supplier_Quantity.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Supplier (added in 13_Trim_To_Four_Actors.sql) and Quantity (from the original
-- 00_MASTER_Setup.sql baseline) are both gone from the Add Product form -- Supplier
-- was never actually surfaced anywhere useful, and Quantity was stock/inventory
-- tracking left over from the removed procurement/requisition flow.
ALTER TABLE Products DROP COLUMN Supplier;
GO

-- Quantity has an unnamed DEFAULT ((0)) from its original CREATE TABLE definition,
-- so that constraint must be dropped before the column itself can be.
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('Products') AND c.name = 'Quantity';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Products DROP CONSTRAINT ' + @constraintName);

ALTER TABLE Products DROP COLUMN Quantity;
GO


GO

-- ==========================================
-- SECTION: 19_Products_Drop_Unit.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Unit (e.g. SqFt/Gallon/Piece) is gone from the Add Product form and every
-- customization-related display -- it never factored into any calculation
-- (ExtraCost is a flat lump-sum, never multiplied by Unit/quantity), so it was
-- pure clutter. No DEFAULT constraint on this column (unlike Quantity/HasParking
-- in earlier drops), so a plain drop is enough.
ALTER TABLE Products DROP COLUMN Unit;
GO


GO

-- ==========================================
-- SECTION: 20_Products_Backfill_CategoryID.sql
-- ==========================================


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


GO

-- ==========================================
-- SECTION: 22_Add_ProjectID_To_Products.sql
-- ==========================================


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


GO

-- ==========================================
-- SECTION: 23_Add_BuildingID_To_Products.sql
-- ==========================================


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


GO

-- ==========================================
-- SECTION: 24_Create_ProductScopeAssignments.sql
-- ==========================================


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


GO

-- ==========================================
-- SECTION: 25_Create_MaterialAssignmentStatus.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Readiness gate for the client's Customize modal. Until a Project Manager marks a scope as ready
-- (Assign Materials > "Mark ... as ready for clients"), clients booked there cannot open Customize.
-- Saving material assignments never changes this flag -- only the explicit Mark / Unmark buttons do.
--
--   ProjectID set, BuildingID NULL = the whole project
--   ProjectID set, BuildingID set  = that one building
--
-- Resolution for a booking (ClientController): the booking's building row if there is one, else its
-- project row, else NOT ready. So a building row overrides the project (an explicit "not ready" for
-- one building blocks it even when the project is ready), and a scope with no row at all is not ready
-- -- nothing is exposed before a PM has reviewed it.
-- MarkedReadyByUserID / MarkedReadyDate describe the current "ready" state and are NULL when not ready.
--
-- Safe to re-run: the table is only created when missing, and the backfill only runs while it is empty.

IF OBJECT_ID('MaterialAssignmentStatus', 'U') IS NULL
BEGIN
    CREATE TABLE MaterialAssignmentStatus (
        StatusID            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaterialAssignmentStatus PRIMARY KEY,
        ProjectID           INT NOT NULL CONSTRAINT FK_MAS_Projects  FOREIGN KEY REFERENCES Projects(ProjectID),
        BuildingID          INT NULL     CONSTRAINT FK_MAS_Buildings FOREIGN KEY REFERENCES Buildings(BuildingID),
        IsReady             BIT NOT NULL CONSTRAINT DF_MAS_IsReady DEFAULT 0,
        MarkedReadyByUserID INT NULL     CONSTRAINT FK_MAS_Users     FOREIGN KEY REFERENCES Users(UserID),
        MarkedReadyDate     DATETIME NULL
    );

    -- One status row per scope (NULLs compare equal in a unique index, so one project-wide row per project).
    CREATE UNIQUE INDEX UX_MAS_Scope ON MaterialAssignmentStatus (ProjectID, BuildingID);
END
GO

-- Backfill: every project / building that ALREADY has real material assignments is marked ready, so
-- clients booked there keep working exactly as today. (MarkedReadyByUserID stays NULL = set by migration.)
-- A global assignment (ProjectID NULL) belongs to no particular project and is skipped.
IF NOT EXISTS (SELECT 1 FROM MaterialAssignmentStatus)
BEGIN
    INSERT INTO MaterialAssignmentStatus (ProjectID, BuildingID, IsReady, MarkedReadyDate)
    SELECT DISTINCT a.ProjectID, a.BuildingID, 1, GETDATE()
    FROM ProductScopeAssignments a
    WHERE a.ProjectID IS NOT NULL;
END
GO


GO

-- ==========================================
-- SECTION: 26_Backfill_Approved_Selections.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- ONE-TIME DATA MIGRATION (no schema change, no code change).
--
-- Before the Approval workflow, every customization selection a client made counted toward the price
-- automatically and was left "Pending" -- there was no reason to approve it. The workflow now bills ONLY
-- Approved selections, so bookings whose clients had ALREADY PAID on the old basis suddenly showed a
-- lower total than what they paid (a negative balance).
--
-- This approves exactly those legacy selections: still-Pending selections on a booking that already has
-- a recorded client payment, made BEFORE the approval workflow went live (2026-09-20 10:36:40).
--   * Selections on bookings with no payment stay Pending -- the Project Manager reviews those properly.
--   * Rejected selections are never touched.
--   * The cut-off keeps a later re-run from approving selections clients make under the new workflow.
-- Going forward new selections stay Pending until a Project Manager reviews them.
--
-- Safe to re-run: it only ever changes Pending rows older than the cut-off, so a second run does nothing.

UPDATE cs
SET cs.Status  = 'Approved',
    cs.Remarks = ISNULL(cs.Remarks, 'Approved by migration 26: the booking already had payments before the approval workflow existed.')
FROM CustomizationSelections cs
WHERE cs.Status = 'Pending'
  AND cs.SelectedDate < '2026-09-20T10:36:40'
  AND EXISTS (SELECT 1 FROM ClientPayments cp WHERE cp.BookingID = cs.BookingID);
GO


GO

-- ==========================================
-- SECTION: 27_Drop_Legacy_Objects.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Removes what is left of the earlier Vendor / Site Engineer / HR / procurement-era design.
-- Nothing in the application reads or writes any of it any more:
--   * Products.VendorID       - Vendor accounts are gone; the Admin's Add Product no longer sets it
--   * Products.CreatedDate    - never read
--   * Buildings.CreatedDate   - never read
--   * ProjectMilestones       - the Site Engineer progress checklist; no controller/view uses it
--   * Users with role Vendor / Site Engineer / HR Executive, and those roles in the Users.Role CHECK
--
-- Safe to re-run: every step checks whether there is anything left to do.
-- Runs against a live database (drops the columns/table/rows above) and against a fresh setup
-- (00 .. 26 create them; this then removes them), so a fresh setup ends up identical to the live one.
-- Take a backup first if you run it against data you care about -- the DELETE and DROPs are permanent.

-- ============================================================
-- 1. Drop Products.VendorID, Products.CreatedDate, Buildings.CreatedDate
--    Any DEFAULT / FOREIGN KEY constraint on a column has to go before the column itself, and
--    their names are system-generated, so they are looked up rather than hard-coded.
-- ============================================================
DECLARE @targets TABLE (TableName SYSNAME, ColumnName SYSNAME);
INSERT INTO @targets VALUES ('Products', 'VendorID'), ('Products', 'CreatedDate'), ('Buildings', 'CreatedDate');

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += N'ALTER TABLE ' + QUOTENAME(t.TableName) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM @targets t
JOIN sys.default_constraints dc ON dc.parent_object_id = OBJECT_ID(t.TableName)
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id AND c.name = t.ColumnName;

SELECT @sql += N'ALTER TABLE ' + QUOTENAME(t.TableName) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM @targets t
JOIN sys.foreign_key_columns fkc ON fkc.parent_object_id = OBJECT_ID(t.TableName)
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id AND c.name = t.ColumnName
JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id;

SELECT @sql += N'ALTER TABLE ' + QUOTENAME(t.TableName) + N' DROP COLUMN ' + QUOTENAME(t.ColumnName) + N';'
FROM @targets t
WHERE COL_LENGTH(t.TableName, t.ColumnName) IS NOT NULL;

EXEC sp_executesql @sql;
GO

-- ============================================================
-- 2. Drop ProjectMilestones (empty; nothing references it)
-- ============================================================
IF OBJECT_ID('ProjectMilestones', 'U') IS NOT NULL
    DROP TABLE ProjectMilestones;
GO

-- ============================================================
-- 3. Users: keep only the four real roles.
--    The DELETE fails (and changes nothing) if any of those users is still referenced by a
--    booking, payment, project or material-assignment row.
-- ============================================================
DELETE FROM Users
WHERE Role NOT IN ('Admin', 'Client', 'Project Manager', 'Accounts Officer');
GO

-- The original CHECK from 00_MASTER_Setup.sql is unnamed, so its system-generated name is looked up.
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON col.object_id = cc.parent_object_id AND col.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('Users') AND col.name = 'Role';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Users DROP CONSTRAINT ' + @constraintName);
GO

ALTER TABLE Users ADD CONSTRAINT CK_Users_Role
    CHECK (Role IN ('Admin', 'Client', 'Project Manager', 'Accounts Officer'));
GO


GO

-- ==========================================
-- SECTION: 28_Drop_Products_ProjectID_BuildingID.sql
-- ==========================================


-- USE Concord_Practicum;
GO

-- Products.ProjectID and Products.BuildingID (added by 22 and 23) were the first way of saying which
-- Project/Building a product belongs to. ProductScopeAssignments (24) replaced them -- 24 copies their
-- data across -- and the application no longer reads or writes either column, so they are dropped.
--
-- Safe to re-run: a column is only dropped while it still exists.
-- Take a backup first if you run it against data you care about -- the DROPs are permanent.

-- The FOREIGN KEY on each column has to go before the column itself; the names are looked up.
DECLARE @targets TABLE (TableName SYSNAME, ColumnName SYSNAME);
INSERT INTO @targets VALUES ('Products', 'ProjectID'), ('Products', 'BuildingID');

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += N'ALTER TABLE ' + QUOTENAME(t.TableName) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM @targets t
JOIN sys.foreign_key_columns fkc ON fkc.parent_object_id = OBJECT_ID(t.TableName)
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id AND c.name = t.ColumnName
JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id;

SELECT @sql += N'ALTER TABLE ' + QUOTENAME(t.TableName) + N' DROP COLUMN ' + QUOTENAME(t.ColumnName) + N';'
FROM @targets t
WHERE COL_LENGTH(t.TableName, t.ColumnName) IS NOT NULL;

EXEC sp_executesql @sql;
GO


GO

-- ==========================================
-- SECTION: 29_Add_ProjectManager_AccountOfficer_To_Projects.sql
-- ==========================================

-- USE Concord_Practicum;
GO

-- Add ProjectManagerID column if it does not exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'ProjectManagerID')
BEGIN
    ALTER TABLE Projects ADD ProjectManagerID INT NULL FOREIGN KEY REFERENCES Users(UserID);
END
GO

-- Add AccountOfficerID column if it does not exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Projects') AND name = 'AccountOfficerID')
BEGIN
    ALTER TABLE Projects ADD AccountOfficerID INT NULL FOREIGN KEY REFERENCES Users(UserID);
END
GO
