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
