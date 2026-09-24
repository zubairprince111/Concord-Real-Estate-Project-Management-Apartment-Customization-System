
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
