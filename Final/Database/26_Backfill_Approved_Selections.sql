
-- USE Concord_Practicum;
GO

-- ONE-TIME DATA MIGRATION (no schema change, no code change).
--
-- Before the Approval workflow, every customization selection a client made counted toward the price
-- automatically and was left "Pending" -- there was no reason to approve it. The workflow now bills ONLY
-- Approved selections, so bookings whose clients had ALREADY PAID on the old basis suddenly showed a
-- lower total than what they paid (a negative balance).
--
-- This approves exactly those legacy selections: still-Pending selections on a booking that already has
-- a recorded client payment, made BEFORE the approval workflow went live (2026-09-20 10:36:40).
--   * Selections on bookings with no payment stay Pending -- the Project Manager reviews those properly.
--   * Rejected selections are never touched.
--   * The cut-off keeps a later re-run from approving selections clients make under the new workflow.
-- Going forward new selections stay Pending until a Project Manager reviews them.
--
-- Safe to re-run: it only ever changes Pending rows older than the cut-off, so a second run does nothing.

UPDATE cs
SET cs.Status  = 'Approved',
    cs.Remarks = ISNULL(cs.Remarks, 'Approved by migration 26: the booking already had payments before the approval workflow existed.')
FROM CustomizationSelections cs
WHERE cs.Status = 'Pending'
  AND cs.SelectedDate < '2026-09-20T10:36:40'
  AND EXISTS (SELECT 1 FROM ClientPayments cp WHERE cp.BookingID = cs.BookingID);
GO
