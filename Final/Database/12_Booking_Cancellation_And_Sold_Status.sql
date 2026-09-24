-- USE Concord_Practicum;
GO

-- ============================================================
-- UnitBookings.Status ('Active'/'Cancelled') + CancelReason
-- Lets Admin cancel a booking (AdminController.CancelBooking) without deleting
-- its history -- cancelled bookings just stop counting as this client's active
-- booking (ClientController.MyBookings only lists Status = 'Active').
-- ============================================================
ALTER TABLE UnitBookings ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Active'
    CONSTRAINT CK_UnitBookings_Status CHECK (Status IN ('Active','Cancelled'));
GO

ALTER TABLE UnitBookings ADD CancelReason NVARCHAR(500) NULL;
GO

-- ============================================================
-- Units.Status: allow 'Sold' alongside 'Available'/'Booked'
-- (AccountsController.RecordClientPayment flips Booked -> Sold once a booking
-- is fully paid). The original CHECK constraint from 00_MASTER_Setup.sql was
-- unnamed, so its system-generated name is looked up before dropping it.
-- ============================================================
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns col ON col.object_id = cc.parent_object_id AND col.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('Units') AND col.name = 'Status';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Units DROP CONSTRAINT [' + @constraintName + ']');
GO

ALTER TABLE Units ADD CONSTRAINT CK_Units_Status CHECK (Status IN ('Available','Booked','Sold'));
GO
