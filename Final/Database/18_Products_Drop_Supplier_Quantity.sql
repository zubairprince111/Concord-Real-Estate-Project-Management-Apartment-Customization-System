
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
