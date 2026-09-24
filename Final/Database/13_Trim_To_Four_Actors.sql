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
