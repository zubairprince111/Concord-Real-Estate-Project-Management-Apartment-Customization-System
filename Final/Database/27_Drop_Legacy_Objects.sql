
USE Concord_Practicum;
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
