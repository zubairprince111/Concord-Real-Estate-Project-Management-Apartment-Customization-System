USE Concord_Practicum;
GO

-- ============================================================
-- ProjectMilestones.Description  (free-text notes on what work
-- is planned/needed or was actually done for that month)
-- ============================================================
ALTER TABLE ProjectMilestones ADD Description NVARCHAR(500) NULL;
GO
