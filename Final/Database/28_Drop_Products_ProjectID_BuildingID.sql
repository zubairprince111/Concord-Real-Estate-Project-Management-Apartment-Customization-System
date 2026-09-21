
USE Concord_Practicum;
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
