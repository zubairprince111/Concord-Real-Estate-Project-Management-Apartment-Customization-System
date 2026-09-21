using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    // Accounts Officer payment recording. Named AccountsController to avoid clashing
    // with the existing AccountController (login/auth).
    public class AccountsController : Controller
    {
        private bool IsAuthorized()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Accounts Officer";
        }

        private ActionResult Guard()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");
            return null;
        }

        public ActionResult Index()
        {
            var guard = Guard();
            if (guard != null) return guard;
            return RedirectToAction("ClientPayments");
        }

        // ---------- Client Payments ----------

        public ActionResult ClientPayments()
        {
            var guard = Guard();
            if (guard != null) return guard;

            // TotalPrice here is the booking's stored base price plus APPROVED customization
            // ExtraCost only (Pending and Rejected selections are never billed) -- mirroring
            // ClientController.MyBookings / GetClientBookingBreakdown, so what Accounts bills matches
            // what the client sees they owe. Reading the raw (stale) UnitBookings.TotalPrice column would silently
            // drop any customization surcharge from the amount collected here.
            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BookingID, u.ProjectID, p.ProjectName, bl.BuildingName, u.FlatNumber,
                         cu.FullName AS ClientName, b.BookingDate,
                         b.TotalPrice +
                         ISNULL((SELECT SUM(pr.ExtraCost) * u.SizeSqFt FROM CustomizationSelections cs
                                 JOIN Products pr ON pr.ProductID = cs.ProductID
                                 JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                                 WHERE cs.BookingID = b.BookingID AND cs.Status = 'Approved'
                                 AND pr.ExtraCost > 0 AND mc.IsCustomizable = 1), 0) AS TotalPrice,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.BookingID = b.BookingID), 0) AS TotalPaid
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  JOIN Users cu ON cu.UserID = b.ClientUserID
                  WHERE b.Status = 'Active'
                  ORDER BY b.BookingID DESC");

            // Every customization selection with its review status, grouped by booking below, so Accounts sees
            // exactly what is (Approved) and is not (Pending / Rejected) part of each Total Price.
            var selectionsByBooking = new Dictionary<int, List<BookingSelectionRow>>();
            foreach (DataRow sRow in DbHelper.QueryTable(
                @"SELECT cs.BookingID, pr.ProductName, pr.Description, pr.ExtraCost * u.SizeSqFt AS Cost, cs.Status, cs.Remarks
                  FROM CustomizationSelections cs
                  JOIN Products pr ON pr.ProductID = cs.ProductID
                  JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                  JOIN UnitBookings b ON b.BookingID = cs.BookingID
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE mc.IsCustomizable = 1
                  ORDER BY cs.BookingID, cs.SelectionID").Rows)
            {
                int selBooking = (int)sRow["BookingID"];
                if (!selectionsByBooking.ContainsKey(selBooking))
                    selectionsByBooking[selBooking] = new List<BookingSelectionRow>();

                string description = sRow["Description"] == DBNull.Value ? "" : sRow["Description"].ToString();
                selectionsByBooking[selBooking].Add(new BookingSelectionRow
                {
                    Name = sRow["ProductName"] + (description.Length == 0 ? "" : " - " + description),
                    Cost = (decimal)sRow["Cost"],
                    Status = sRow["Status"].ToString(),
                    Remarks = sRow["Remarks"] == DBNull.Value ? null : sRow["Remarks"].ToString()
                });
            }

            var bookings = new List<ClientBookingModel>();
            foreach (DataRow row in table.Rows)
            {
                int bookingId = (int)row["BookingID"];

                // Individual payments already recorded against this booking, nested so
                // each one can link to its own printable receipt.
                DataTable paymentsTable = DbHelper.QueryTable(
                    "SELECT ClientPaymentID, PaymentDate, Amount FROM ClientPayments WHERE BookingID = @BookingID ORDER BY ClientPaymentID DESC",
                    new SqlParameter("@BookingID", bookingId));

                var payments = new List<ClientPaymentRow>();
                foreach (DataRow pRow in paymentsTable.Rows)
                {
                    payments.Add(new ClientPaymentRow
                    {
                        PaymentID = (int)pRow["ClientPaymentID"],
                        PaymentDate = (DateTime)pRow["PaymentDate"],
                        Amount = (decimal)pRow["Amount"]
                    });
                }

                bookings.Add(new ClientBookingModel
                {
                    BookingID = bookingId,
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    ClientName = row["ClientName"].ToString(),
                    BookingDate = (DateTime)row["BookingDate"],
                    TotalPrice = (decimal)row["TotalPrice"],
                    TotalPaid = (decimal)row["TotalPaid"],
                    Payments = payments,
                    Selections = selectionsByBooking.ContainsKey(bookingId) ? selectionsByBooking[bookingId] : new List<BookingSelectionRow>()
                });
            }

            return View(bookings);
        }

        // Printable single-transaction receipt for one ClientPayments row.
        // Accessible to the Accounts Officer (any receipt) or the Client who made this
        // specific payment (their own receipts only) -- ownership is checked against the
        // actual row, not just the role, so a Client can't view another client's receipt
        // by guessing a paymentId in the URL.
        public ActionResult ClientPaymentReceipt(int paymentId)
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");

            DataRow row = DbHelper.QuerySingleRow(
                @"SELECT cp.ClientPaymentID, cp.Amount, cp.PaymentDate,
                         p.ProjectName, u.FlatNumber, cu.FullName AS ClientName, ub.ClientUserID,
                         ub.TotalPrice +
                         ISNULL((SELECT SUM(pr.ExtraCost) * u.SizeSqFt FROM CustomizationSelections cs
                                 JOIN Products pr ON pr.ProductID = cs.ProductID
                                 JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                                 WHERE cs.BookingID = ub.BookingID AND cs.Status = 'Approved'
                                 AND pr.ExtraCost > 0 AND mc.IsCustomizable = 1), 0) AS TotalPrice,
                         ISNULL((SELECT SUM(cp2.Amount) FROM ClientPayments cp2
                                 WHERE cp2.BookingID = cp.BookingID), 0) AS TotalPaid
                  FROM ClientPayments cp
                  JOIN Projects p ON p.ProjectID = cp.ProjectID
                  JOIN UnitBookings ub ON ub.BookingID = cp.BookingID
                  JOIN Units u ON u.UnitID = ub.UnitID
                  JOIN Users cu ON cu.UserID = ub.ClientUserID
                  WHERE cp.ClientPaymentID = @PaymentID",
                new SqlParameter("@PaymentID", paymentId));

            if (row == null)
                return HttpNotFound();

            string role = Session["UserRole"].ToString();
            bool isAccountsOfficer = role == "Accounts Officer";
            bool isAdmin = role == "Admin";
            bool isOwningClient = role == "Client" && (int)row["ClientUserID"] == (int)Session["UserID"];

            if (!isAccountsOfficer && !isAdmin && !isOwningClient)
                return RedirectToAction("AccessDenied", "Account");

            decimal totalPrice = (decimal)row["TotalPrice"];
            decimal totalPaid = (decimal)row["TotalPaid"];

            var model = new PaymentReceiptModel
            {
                PaymentID = (int)row["ClientPaymentID"],
                Amount = (decimal)row["Amount"],
                PaymentDate = (DateTime)row["PaymentDate"],
                PartyLabel = "Client",
                PartyName = row["ClientName"].ToString(),
                ProjectName = row["ProjectName"].ToString(),
                ExtraDetail = "Flat " + row["FlatNumber"],
                TotalLabel = "Total Price",
                TotalPrice = totalPrice,
                Due = totalPrice - totalPaid
            };
            return View("Receipt", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordClientPayment(int bookingId, decimal amount)
        {
            var guard = Guard();
            if (guard != null) return guard;

            int userId = (int)Session["UserID"];

            // Derive the ProjectID from the booking's unit so it can't be spoofed from the form.
            DataRow booking = DbHelper.QuerySingleRow(
                @"SELECT u.ProjectID FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE b.BookingID = @BookingID AND b.Status = 'Active'",
                new SqlParameter("@BookingID", bookingId));

            if (booking == null)
            {
                TempData["Error"] = "Booking #" + bookingId + " was not found or has been cancelled. Payment not recorded.";
                return RedirectToAction("ClientPayments");
            }

            if (amount > 0)
            {
                // What is payable: base price + APPROVED customizations, less what is already paid. A payment
                // above that would be money "received" for customizations that are still pending or were
                // rejected, so it is refused.
                DataRow money = GetBookingMoney(bookingId);
                decimal due = (decimal)money["TotalPrice"] - (decimal)money["TotalPaid"];
                if (amount > due)
                {
                    TempData["Error"] = (due <= 0
                        ? "Booking #" + bookingId + " has nothing due right now"
                        : "Tk " + amount.ToString("N2") + " is more than the Tk " + due.ToString("N2") + " due on booking #" + bookingId) +
                        " (only approved customizations count toward what is due). Payment not recorded.";
                    return RedirectToAction("ClientPayments");
                }

                DbHelper.Execute(
                    @"INSERT INTO ClientPayments (BookingID, ProjectID, Amount, RecordedByUserID)
                      VALUES (@BookingID, @ProjectID, @Amount, @RecordedByUserID)",
                    new SqlParameter("@BookingID", bookingId),
                    new SqlParameter("@ProjectID", (int)booking["ProjectID"]),
                    new SqlParameter("@Amount", amount),
                    new SqlParameter("@RecordedByUserID", userId));
                TempData["Success"] = "Payment of Tk " + amount.ToString("N2") + " recorded for booking #" + bookingId + ".";
            }
            else
            {
                TempData["Error"] = "Enter an amount greater than zero. Payment not recorded.";
            }

            MarkUnitSoldIfFullyPaid(bookingId);

            return RedirectToAction("ClientPayments");
        }

        // Flips the booking's Unit from Booked to Sold once cumulative ClientPayments
        // reach the full price (base price + approved customization extras) --
        // mirrors the TotalPrice computation ClientPayments/ClientController.MyBookings use,
        // so "fully paid" here means the same thing it means everywhere else this is shown.
        // The booking's payable total (base price + APPROVED customization extras) and what has been paid.
        private DataRow GetBookingMoney(int bookingId)
        {
            return DbHelper.QuerySingleRow(
                @"SELECT u.UnitID,
                         b.TotalPrice +
                         ISNULL((SELECT SUM(pr.ExtraCost) * u.SizeSqFt FROM CustomizationSelections cs
                                 JOIN Products pr ON pr.ProductID = cs.ProductID
                                 JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                                 WHERE cs.BookingID = b.BookingID AND cs.Status = 'Approved'
                                 AND pr.ExtraCost > 0 AND mc.IsCustomizable = 1), 0) AS TotalPrice,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.BookingID = b.BookingID), 0) AS TotalPaid
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE b.BookingID = @BookingID",
                new SqlParameter("@BookingID", bookingId));
        }

        private void MarkUnitSoldIfFullyPaid(int bookingId)
        {
            DataRow row = GetBookingMoney(bookingId);

            if (row == null)
                return;

            decimal totalPrice = (decimal)row["TotalPrice"];
            decimal totalPaid = (decimal)row["TotalPaid"];

            if (totalPaid < totalPrice)
                return;

            DbHelper.Execute(
                "UPDATE Units SET Status = 'Sold' WHERE UnitID = @UnitID AND Status = 'Booked'",
                new SqlParameter("@UnitID", (int)row["UnitID"]));
        }

        // ---------- Project Ledger ----------

        public ActionResult Ledger()
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataTable table = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.ProjectID = p.ProjectID), 0) AS TotalClientPayments
                  FROM Projects p
                  ORDER BY p.ProjectName");

            var ledger = new List<ProjectLedgerModel>();
            foreach (DataRow row in table.Rows)
            {
                ledger.Add(new ProjectLedgerModel
                {
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    TotalClientPayments = (decimal)row["TotalClientPayments"]
                });
            }

            return View(ledger);
        }
    }
}
