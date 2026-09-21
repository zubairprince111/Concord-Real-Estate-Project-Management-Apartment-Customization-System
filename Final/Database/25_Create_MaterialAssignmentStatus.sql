
USE Concord_Practicum;
GO

-- Readiness gate for the client's Customize modal. Until a Project Manager marks a scope as ready
-- (Assign Materials > "Mark ... as ready for clients"), clients booked there cannot open Customize.
-- Saving material assignments never changes this flag -- only the explicit Mark / Unmark buttons do.
--
--   ProjectID set, BuildingID NULL = the whole project
--   ProjectID set, BuildingID set  = that one building
--
-- Resolution for a booking (ClientController): the booking's building row if there is one, else its
-- project row, else NOT ready. So a building row overrides the project (an explicit "not ready" for
-- one building blocks it even when the project is ready), and a scope with no row at all is not ready
-- -- nothing is exposed before a PM has reviewed it.
-- MarkedReadyByUserID / MarkedReadyDate describe the current "ready" state and are NULL when not ready.
--
-- Safe to re-run: the table is only created when missing, and the backfill only runs while it is empty.

IF OBJECT_ID('MaterialAssignmentStatus', 'U') IS NULL
BEGIN
    CREATE TABLE MaterialAssignmentStatus (
        StatusID            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaterialAssignmentStatus PRIMARY KEY,
        ProjectID           INT NOT NULL CONSTRAINT FK_MAS_Projects  FOREIGN KEY REFERENCES Projects(ProjectID),
        BuildingID          INT NULL     CONSTRAINT FK_MAS_Buildings FOREIGN KEY REFERENCES Buildings(BuildingID),
        IsReady             BIT NOT NULL CONSTRAINT DF_MAS_IsReady DEFAULT 0,
        MarkedReadyByUserID INT NULL     CONSTRAINT FK_MAS_Users     FOREIGN KEY REFERENCES Users(UserID),
        MarkedReadyDate     DATETIME NULL
    );

    -- One status row per scope (NULLs compare equal in a unique index, so one project-wide row per project).
    CREATE UNIQUE INDEX UX_MAS_Scope ON MaterialAssignmentStatus (ProjectID, BuildingID);
END
GO

-- Backfill: every project / building that ALREADY has real material assignments is marked ready, so
-- clients booked there keep working exactly as today. (MarkedReadyByUserID stays NULL = set by migration.)
-- A global assignment (ProjectID NULL) belongs to no particular project and is skipped.
IF NOT EXISTS (SELECT 1 FROM MaterialAssignmentStatus)
BEGIN
    INSERT INTO MaterialAssignmentStatus (ProjectID, BuildingID, IsReady, MarkedReadyDate)
    SELECT DISTINCT a.ProjectID, a.BuildingID, 1, GETDATE()
    FROM ProductScopeAssignments a
    WHERE a.ProjectID IS NOT NULL;
END
GO
