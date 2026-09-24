

-- USE Concord_Practicum;
GO

ALTER TABLE CustomizationSelections ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Pending'
    CONSTRAINT CK_CustomizationSelections_Status CHECK (Status IN ('Pending','Approved','Rejected'));
GO

ALTER TABLE CustomizationSelections ADD Remarks NVARCHAR(500) NULL;
GO
