

USE Concord_Practicum;
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
