USE Concord_Practicum;
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
