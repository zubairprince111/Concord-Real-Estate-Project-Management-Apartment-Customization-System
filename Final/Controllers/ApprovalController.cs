using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    public class ApprovalController : Controller
    {
        private bool IsAuthorized()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Project Manager";
        }

        // ---------- Customization selections ----------

        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");

            DataTable table = DbHelper.QueryTable(
                @"SELECT cs.SelectionID, cs.BookingID, p.ProjectName, bl.BuildingName, u.FlatNumber,
                         cu.FullName AS ClientName, pr.ProductName, pr.Price, cs.SelectedDate
                  FROM CustomizationSelections cs
                  JOIN UnitBookings b ON b.BookingID = cs.BookingID
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  JOIN Users cu ON cu.UserID = b.ClientUserID
                  JOIN Products pr ON pr.ProductID = cs.ProductID
                  WHERE cs.Status = 'Pending' AND b.Status = 'Active'
                  ORDER BY cs.SelectedDate ASC");

            var pending = new List<CustomizationReviewModel>();
            foreach (DataRow row in table.Rows)
            {
                pending.Add(new CustomizationReviewModel
                {
                    SelectionID = (int)row["SelectionID"],
                    BookingID = (int)row["BookingID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    ClientName = row["ClientName"].ToString(),
                    ProductName = row["ProductName"].ToString(),
                    Price = (decimal)row["Price"],
                    SelectedDate = (DateTime)row["SelectedDate"]
                });
            }

            return View(pending);
        }

        // Approving needs no comment (a note is optional and shown to the client).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveCustomization(int id, string remarks)
        {
            string note = (remarks ?? "").Trim();
            return DecideCustomization(id, "Approved", note.Length == 0 ? null : note);
        }

        // Rejecting REQUIRES a reason: the client sees it ("Rejected: <reason>") and needs it to pick a
        // different option, so a blank one is refused and nothing is changed.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectCustomization(int id, string remarks)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");

            string reason = (remarks ?? "").Trim();
            if (reason.Length == 0)
            {
                TempData["Error"] = "A reason is required to reject a customization -- the client sees it. Nothing was changed.";
                return RedirectToAction("Index");
            }
            if (reason.Length > 500)
            {
                TempData["Error"] = "The reason can't be longer than 500 characters. Nothing was changed.";
                return RedirectToAction("Index");
            }

            return DecideCustomization(id, "Rejected", reason);
        }

        private ActionResult DecideCustomization(int selectionId, string decision, string remarks)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");

            int changed = DbHelper.Execute(
                "UPDATE CustomizationSelections SET Status = @Status, Remarks = @Remarks WHERE SelectionID = @SelectionID AND Status = 'Pending'",
                new SqlParameter("@Status", decision),
                new SqlParameter("@Remarks", (object)remarks ?? DBNull.Value),
                new SqlParameter("@SelectionID", selectionId));

            if (changed == 0)
                TempData["Error"] = "That selection was already decided (or no longer exists).";
            else
                TempData["Success"] = decision == "Approved" ? "Customization approved." : "Customization rejected -- the client can see your reason.";

            return RedirectToAction("Index");
        }
    }
}
