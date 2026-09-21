using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Final.Data;
using Final.Models;
using Final.Services;

namespace Final.Controllers
{
    public class ClientController : Controller
    {
        // Shown wherever the readiness gate stops a client from customizing.
        private const string NotReadyMessage =
            "Customization options are still being finalized for your building. Please check back soon.";

        // The readiness gate (MaterialAssignmentStatus) for the unit aliased `u`: the unit's building
        // row if there is one, else its project row, else 0 -- a scope the Project Manager hasn't marked
        // ready is NOT ready. (Same building -> project fallback as the material assignments.)
        private const string UnitReadySql =
            @"ISNULL((SELECT TOP 1 CAST(s.IsReady AS int)
                      FROM MaterialAssignmentStatus s
                      WHERE s.ProjectID = u.ProjectID AND (s.BuildingID = u.BuildingID OR s.BuildingID IS NULL)
                      ORDER BY CASE WHEN s.BuildingID = u.BuildingID THEN 1 ELSE 2 END), 0)";

        private bool IsCustomizationReady(int bookingId, int clientUserId)
        {
            object ready = DbHelper.ExecuteScalar(
                @"SELECT " + UnitReadySql + @"
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE b.BookingID = @BookingID AND b.ClientUserID = @ClientUserID",
                new SqlParameter("@BookingID", bookingId),
                new SqlParameter("@ClientUserID", clientUserId));
            return ready != null && ready != DBNull.Value && Convert.ToInt32(ready) == 1;
        }

        private bool IsAuthorized()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Client";
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
            return RedirectToAction("Units");
        }

        // ---------- Available units ----------

        public ActionResult Units()
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataTable table = DbHelper.QueryTable(
                @"SELECT u.UnitID, u.BuildingID, b.BuildingName, u.ProjectID, p.ProjectName,
                         u.FlatNumber, u.FloorNumber, u.SizeSqFt, u.BedroomCount, u.BathroomCount, u.AttachedBathrooms,
                         u.HasKitchen, u.HasHall, u.BalconyCount, u.Facing, u.BasePrice, u.Status, p.EstimatedCompletionDate,
                         " + UnitReadySql + @" AS CustomizationReady
                  FROM Units u
                  JOIN Buildings b ON b.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  WHERE u.Status = 'Available'
                  ORDER BY u.UnitID DESC");

            var units = new List<UnitModel>();
            foreach (DataRow row in table.Rows)
                units.Add(UnitController.MapUnit(row));

            var unitCustomizations = new Dictionary<int, List<CustomizableProduct>>();
            foreach (var u in units)
            {
                unitCustomizations[u.UnitID] = GetAvailableCustomizationsForScope(u.ProjectID, u.BuildingID);
            }
            ViewBag.UnitCustomizations = unitCustomizations;

            var categoryNames = new List<string>();
            foreach (DataRow r in DbHelper.QueryTable("SELECT CategoryName FROM MasterCategories ORDER BY CategoryID DESC").Rows)
                categoryNames.Add(r["CategoryName"].ToString());
            ViewBag.CategoryNames = categoryNames;

            return View(units);
        }

        private List<CustomizableProduct> GetAvailableCustomizationsForScope(int projectId, int buildingId)
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT pr.ProductID, pr.ProductName, pr.Description, pr.Price,
                         pr.IsDefaultOption, pr.ExtraCost, mc.CategoryName, mc.IsCustomizable
                  FROM Products pr
                  JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                  CROSS APPLY (SELECT CASE WHEN (pr.Category = 'Customizable' OR mc.IsCustomizable = 1) AND pr.IsApprovedForClientCustomization = 1
                                            AND EXISTS (SELECT 1 FROM ProductScopeAssignments a
                                                        WHERE a.ProductID = pr.ProductID
                                                          AND (a.BuildingID = @BuildingID
                                                               OR (a.ProjectID = @ProjectID AND a.BuildingID IS NULL)))
                                       THEN 1 ELSE 0 END AS Offered) o
                  WHERE (mc.IsCustomizable = 0 AND pr.ProductID =
                            (SELECT TOP 1 a.ProductID
                             FROM ProductScopeAssignments a
                             JOIN Products x ON x.ProductID = a.ProductID
                             WHERE x.CategoryID = pr.CategoryID
                               AND (a.BuildingID = @BuildingID
                                    OR (a.ProjectID = @ProjectID AND a.BuildingID IS NULL)
                                    OR (a.ProjectID IS NULL AND a.BuildingID IS NULL))
                             ORDER BY CASE WHEN a.BuildingID = @BuildingID THEN 1
                                           WHEN a.ProjectID = @ProjectID THEN 2
                                           ELSE 3 END,
                                      a.ProductID DESC))
                         OR (mc.IsCustomizable = 1 AND o.Offered = 1)
                  ORDER BY mc.CategoryID DESC, pr.ProductID DESC",
                new SqlParameter("@ProjectID", projectId),
                new SqlParameter("@BuildingID", buildingId));

            var list = new List<CustomizableProduct>();
            foreach (DataRow row in table.Rows)
            {
                list.Add(new CustomizableProduct
                {
                    ProductID = (int)row["ProductID"],
                    ProductName = row["ProductName"].ToString(),
                    Description = row["Description"] == DBNull.Value ? null : row["Description"].ToString(),
                    Price = (decimal)row["Price"],
                    IsDefaultOption = (bool)row["IsDefaultOption"],
                    ExtraCost = (decimal)row["ExtraCost"],
                    CategoryName = row["CategoryName"].ToString(),
                    IsCustomizableCategory = (bool)row["IsCustomizable"]
                });
            }
            return list;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookUnit(int unitId)
        {
            var guard = Guard();
            if (guard != null) return guard;

            int clientUserId = (int)Session["UserID"];

            // Claim the unit atomically: only proceed if it is still Available and PM setup is complete.
            DataRow unit = DbHelper.QuerySingleRow(
                @"SELECT u.BasePrice, " + UnitReadySql + @" AS CustomizationReady
                  FROM Units u WHERE u.UnitID = @UnitID AND u.Status = 'Available'",
                new SqlParameter("@UnitID", unitId));

            if (unit == null)
            {
                TempData["Error"] = "That unit is no longer available.";
                return RedirectToAction("Units");
            }

            if (Convert.ToInt32(unit["CustomizationReady"]) != 1)
            {
                TempData["Error"] = "Booking is locked for this unit until the Project Manager completes the material and customization setup.";
                return RedirectToAction("Units");
            }

            int claimed = DbHelper.Execute(
                "UPDATE Units SET Status = 'Booked' WHERE UnitID = @UnitID AND Status = 'Available'",
                new SqlParameter("@UnitID", unitId));

            if (claimed == 0)
            {
                TempData["Error"] = "That unit was just booked by someone else.";
                return RedirectToAction("Units");
            }

            decimal basePrice = (decimal)unit["BasePrice"];

            DbHelper.Execute(
                @"INSERT INTO UnitBookings (UnitID, ClientUserID, TotalPrice)
                  VALUES (@UnitID, @ClientUserID, @TotalPrice)",
                new SqlParameter("@UnitID", unitId),
                new SqlParameter("@ClientUserID", clientUserId),
                new SqlParameter("@TotalPrice", basePrice));

            TempData["Success"] = "Unit booked successfully! You can view and manage your booking in My Bookings.";
            return RedirectToAction("MyBookings");
        }

        // ---------- My bookings ----------

        public ActionResult MyBookings(int? openCustomize = null)
        {
            var guard = Guard();
            if (guard != null) return guard;

            int clientUserId = (int)Session["UserID"];

            // TotalPrice here is computed live (booking's stored base + APPROVED customizations'
            // ExtraCost -- Pending and Rejected selections are never billed) rather than read from the
            // stale UnitBookings.TotalPrice column, so it always matches the Customize modal's total.
            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BookingID, p.ProjectName, bl.BuildingName, u.FlatNumber, b.BookingDate,
                         b.TotalPrice +
                         ISNULL((SELECT SUM(pr.ExtraCost) * u.SizeSqFt FROM CustomizationSelections cs
                                 JOIN Products pr ON pr.ProductID = cs.ProductID
                                 JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                                 WHERE cs.BookingID = b.BookingID AND cs.Status = 'Approved'
                                 AND pr.ExtraCost > 0 AND mc.IsCustomizable = 1), 0) AS TotalPrice,
                         p.EstimatedCompletionDate,
                         (SELECT COUNT(*) FROM CustomizationSelections cs WHERE cs.BookingID = b.BookingID) AS CustomizationCount,
                         (SELECT COUNT(*) FROM CustomizationSelections cs WHERE cs.BookingID = b.BookingID AND cs.Status = 'Approved') AS ApprovedCount,
                         (SELECT COUNT(*) FROM CustomizationSelections cs WHERE cs.BookingID = b.BookingID AND cs.Status = 'Pending') AS PendingCount,
                         (SELECT COUNT(*) FROM CustomizationSelections cs WHERE cs.BookingID = b.BookingID AND cs.Status = 'Rejected') AS RejectedCount,
                         " + UnitReadySql + @" AS CustomizationReady
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  WHERE b.ClientUserID = @ClientUserID AND b.Status = 'Active'
                  ORDER BY b.BookingID DESC",
                new SqlParameter("@ClientUserID", clientUserId));

            var bookings = new List<ClientBookingSummary>();
            foreach (DataRow row in table.Rows)
            {
                bookings.Add(new ClientBookingSummary
                {
                    BookingID = (int)row["BookingID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    BookingDate = (DateTime)row["BookingDate"],
                    TotalPrice = (decimal)row["TotalPrice"],
                    CustomizationCount = (int)row["CustomizationCount"],
                    ApprovedCount = (int)row["ApprovedCount"],
                    PendingCount = (int)row["PendingCount"],
                    RejectedCount = (int)row["RejectedCount"],
                    CustomizationReady = Convert.ToInt32(row["CustomizationReady"]) == 1,
                    EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"]
                });
            }

            // Every booking's Customize modal is rendered upfront on this same page (this project
            // has no AJAX anywhere -- same pattern as the per-row Edit modals on the Project/Building/Unit lists), keyed
            // by BookingID so the view can look up the right one for each row's modal partial.
            // A booking whose building/project the Project Manager has not marked ready gets NO modal at all
            // (nothing is built or sent to the browser for it) and a disabled Customize button.
            var customizeModels = new Dictionary<int, CustomizeViewModel>();
            foreach (var b in bookings)
            {
                if (b.CustomizationReady)
                    customizeModels[b.BookingID] = BuildCustomizeViewModel(b.BookingID, clientUserId);
            }
            ViewBag.CustomizeModels = customizeModels;
            ViewBag.NotReadyMessage = NotReadyMessage;

            if (openCustomize.HasValue)
            {
                var target = bookings.FirstOrDefault(x => x.BookingID == openCustomize.Value);
                if (target != null && target.CustomizationReady)
                    ViewBag.OpenModalId = "customizeModal_" + openCustomize.Value;
                else if (target != null)
                    ViewBag.NotReadyNotice = NotReadyMessage;   // e.g. straight after booking a unit that isn't ready yet
            }

            return View(bookings);
        }

        // ---------- Customization ----------

        // Asked by My Bookings the moment a client clicks Customize (before the modal is shown), so a page that
        // was opened while the scope was ready can't keep opening the modal after the Project Manager
        // un-readies it. Always answers from the database right now (never cached).
        [HttpGet]
        public ActionResult CustomizationStatus(int bookingId)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Client")
                return new HttpStatusCodeResult(403);

            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            int clientUserId = (int)Session["UserID"];
            bool ready = IsCustomizationReady(bookingId, clientUserId);
            return Json(new { ready = ready, message = ready ? null : NotReadyMessage }, JsonRequestBehavior.AllowGet);
        }

        // One unified, category-grouped product list for a booking's Customize modal: every
        // category (customizable or not) is included, driven off MasterCategories -- a product
        // with no CategoryID assigned is excluded entirely rather than shown in a fallback bucket.
        private CustomizeViewModel BuildCustomizeViewModel(int bookingId, int clientUserId)
        {
            DataRow booking = DbHelper.QuerySingleRow(
                @"SELECT b.BookingID, u.ProjectID, u.BuildingID, p.ProjectName, bl.BuildingName, u.FlatNumber, u.BasePrice, u.SizeSqFt
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  WHERE b.BookingID = @BookingID AND b.ClientUserID = @ClientUserID",
                new SqlParameter("@BookingID", bookingId),
                new SqlParameter("@ClientUserID", clientUserId));

            if (booking == null)
                return null;

            var model = new CustomizeViewModel
            {
                BookingID = bookingId,
                ProjectName = booking["ProjectName"].ToString(),
                BuildingName = booking["BuildingName"].ToString(),
                FlatNumber = booking["FlatNumber"].ToString(),
                // Display-only running total: the Unit's
                // stored BasePrice is untouched, this is just what the price WOULD be with the
                // currently selected premium items. Doesn't affect UnitBookings/ClientPayments.
                UnitBasePrice = (decimal)booking["BasePrice"],
                // Products.ExtraCost is a per-sq-ft rate; a selection's actual extra cost is
                // ExtraCost * this unit's size.
                UnitSizeSqFt = (decimal)booking["SizeSqFt"]
            };

            // Only APPROVED customizations are billed (they alone make up CurrentExtraTotal, which the modal's
            // "Selected Upgrades"/Total show); Pending ones are reported separately and Rejected ones not at all.
            DataRow extras = DbHelper.QuerySingleRow(
                @"SELECT ISNULL(SUM(CASE WHEN cs.Status = 'Approved' THEN pr.ExtraCost * u.SizeSqFt END), 0) AS ApprovedTotal,
                         ISNULL(SUM(CASE WHEN cs.Status = 'Pending' THEN pr.ExtraCost * u.SizeSqFt END), 0) AS PendingTotal
                  FROM CustomizationSelections cs
                  JOIN Products pr ON pr.ProductID = cs.ProductID
                  JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                  JOIN UnitBookings b ON b.BookingID = cs.BookingID
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE cs.BookingID = @BookingID AND cs.Status IN ('Approved', 'Pending') AND pr.ExtraCost > 0
                    AND mc.IsCustomizable = 1",
                new SqlParameter("@BookingID", bookingId));
            model.CurrentExtraTotal = (decimal)extras["ApprovedTotal"];
            model.PendingExtraTotal = (decimal)extras["PendingTotal"];

            // What this booking's unit is offered comes from ProductScopeAssignments, which the Project
            // Manager fills in (MaterialAssignmentController) from the Admin's catalog:
            //  - Non-customizable categories (read-only rows): exactly ONE product per category -- the
            //    most specific assignment for this unit: its Building, else its Project (BuildingID
            //    NULL), else a global one (ProjectID and BuildingID NULL). ProductID breaks a tie.
            //  - Customizable categories (selectable): every Admin-approved variant assigned to the
            //    unit's Building or to its whole Project (a building sees the union of the two), PLUS any
            //    variant this booking has already selected even if the PM has since un-offered it -- the
            //    client committed to it (and it still counts toward their total), so it stays listed and
            //    the modal marks it "No longer offered" (IsStillOffered = false). Nothing here ever
            //    deletes or voids a saved selection.
            // A product can have several selection rows for this booking now (a Rejected one kept as history plus
            // a newer one after the client picked it again), so `cs` is the NEWEST row and `rj` the latest
            // rejection; a product only counts as selected while its newest row is Pending or Approved.
            DataTable table = DbHelper.QueryTable(
                @"SELECT pr.ProductID, pr.ProductName, pr.Description, pr.Price,
                         cs.Status, cs.Remarks,
                         CASE WHEN cs.SelectionID IS NOT NULL AND cs.Status <> 'Rejected' THEN 1 ELSE 0 END AS IsSelected,
                         CASE WHEN rj.SelectionID IS NOT NULL THEN 1 ELSE 0 END AS HasRejection,
                         rj.Remarks AS RejectedRemarks,
                         pr.IsDefaultOption, pr.ExtraCost, mc.CategoryName, mc.IsCustomizable, o.Offered
                  FROM Products pr
                  JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                  OUTER APPLY (SELECT TOP 1 c.SelectionID, c.Status, c.Remarks
                               FROM CustomizationSelections c
                               WHERE c.BookingID = @BookingID AND c.ProductID = pr.ProductID
                               ORDER BY c.SelectionID DESC) cs
                  OUTER APPLY (SELECT TOP 1 c.SelectionID, c.Remarks
                               FROM CustomizationSelections c
                               WHERE c.BookingID = @BookingID AND c.ProductID = pr.ProductID AND c.Status = 'Rejected'
                               ORDER BY c.SelectionID DESC) rj
                  CROSS APPLY (SELECT CASE WHEN pr.Category = 'Customizable' AND pr.IsApprovedForClientCustomization = 1
                                            AND EXISTS (SELECT 1 FROM ProductScopeAssignments a
                                                        WHERE a.ProductID = pr.ProductID
                                                          AND (a.BuildingID = @BuildingID
                                                               OR (a.ProjectID = @ProjectID AND a.BuildingID IS NULL)))
                                       THEN 1 ELSE 0 END AS Offered) o
                  WHERE (mc.IsCustomizable = 0 AND pr.ProductID =
                            (SELECT TOP 1 a.ProductID
                             FROM ProductScopeAssignments a
                             JOIN Products x ON x.ProductID = a.ProductID
                             WHERE x.CategoryID = pr.CategoryID
                               AND (a.BuildingID = @BuildingID
                                    OR (a.ProjectID = @ProjectID AND a.BuildingID IS NULL)
                                    OR (a.ProjectID IS NULL AND a.BuildingID IS NULL))
                             ORDER BY CASE WHEN a.BuildingID = @BuildingID THEN 1
                                           WHEN a.ProjectID = @ProjectID THEN 2
                                           ELSE 3 END,
                                      a.ProductID))
                         OR (mc.IsCustomizable = 1 AND (o.Offered = 1 OR (cs.SelectionID IS NOT NULL AND cs.Status <> 'Rejected')))
                  ORDER BY mc.CategoryID DESC, pr.ProductID DESC",
                new SqlParameter("@BookingID", bookingId),
                new SqlParameter("@ProjectID", (int)booking["ProjectID"]),
                new SqlParameter("@BuildingID", (int)booking["BuildingID"]));

            foreach (DataRow row in table.Rows)
            {
                model.Products.Add(new CustomizableProduct
                {
                    ProductID = (int)row["ProductID"],
                    ProductName = row["ProductName"].ToString(),
                    Description = row["Description"] == DBNull.Value ? null : row["Description"].ToString(),
                    Price = (decimal)row["Price"],
                    IsSelected = Convert.ToInt32(row["IsSelected"]) == 1,
                    Status = row["Status"] == DBNull.Value ? null : row["Status"].ToString(),
                    Remarks = row["Remarks"] == DBNull.Value ? null : row["Remarks"].ToString(),
                    IsDefaultOption = (bool)row["IsDefaultOption"],
                    ExtraCost = (decimal)row["ExtraCost"],
                    CategoryName = row["CategoryName"].ToString(),
                    IsCustomizableCategory = (bool)row["IsCustomizable"],
                    IsStillOffered = !(bool)row["IsCustomizable"] || Convert.ToInt32(row["Offered"]) == 1,
                    // an earlier rejection worth mentioning only once a newer (non-rejected) selection has replaced it
                    HasEarlierRejection = Convert.ToInt32(row["HasRejection"]) == 1 && row["Status"] != DBNull.Value && row["Status"].ToString() != "Rejected",
                    EarlierRejectionReason = row["RejectedRemarks"] == DBNull.Value ? null : row["RejectedRemarks"].ToString()
                });
            }

            foreach (DataRow row in DbHelper.QueryTable("SELECT CategoryName FROM MasterCategories ORDER BY CategoryID DESC").Rows)
                model.CategoryNames.Add(row["CategoryName"].ToString());

            return model;
        }

        // Deep-link entry point (e.g. a bookmarked URL) -- verifies ownership, then forwards into
        // the same modal flow My Bookings uses; there's no standalone Customize page anymore.
        public ActionResult Customize(int id)
        {
            var guard = Guard();
            if (guard != null) return guard;

            int clientUserId = (int)Session["UserID"];

            object owns = DbHelper.ExecuteScalar(
                "SELECT BookingID FROM UnitBookings WHERE BookingID = @BookingID AND ClientUserID = @ClientUserID",
                new SqlParameter("@BookingID", id),
                new SqlParameter("@ClientUserID", clientUserId));

            if (owns == null || owns == DBNull.Value)
                return HttpNotFound();

            return RedirectToAction("MyBookings", new { openCustomize = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCustomization(int bookingId, int[] productIds)
        {
            var guard = Guard();
            if (guard != null) return guard;

            int clientUserId = (int)Session["UserID"];

            // Verify ownership before touching anything.
            object owns = DbHelper.ExecuteScalar(
                "SELECT BookingID FROM UnitBookings WHERE BookingID = @BookingID AND ClientUserID = @ClientUserID",
                new SqlParameter("@BookingID", bookingId),
                new SqlParameter("@ClientUserID", clientUserId));

            if (owns == null || owns == DBNull.Value)
                return HttpNotFound();

            // Same gate as the modal: a booking whose scope isn't marked ready can't change selections.
            if (!IsCustomizationReady(bookingId, clientUserId))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = NotReadyMessage });

                TempData["Error"] = NotReadyMessage;
                return RedirectToAction("MyBookings");
            }

            // Diff against the existing ACTIVE selection set (Pending/Approved) rather than delete-all-reinsert,
            // so that an untouched selection's review Status/Remarks (set by the Project Manager) survives a
            // resave. Unchecking a product removes it (cancel); checking a new one inserts it fresh as Pending.
            // Rejected selections are HISTORY: they are never deleted here, and picking a product again after a
            // rejection (a different variant, or the same one) inserts a NEW Pending row next to the old one.
            // productIds carries single-product categories' checkboxes; mutually-exclusive
            // categories with more than one product post a radio per category instead, each
            // named "category_<CategoryName>" so only one value per category comes through.
            var newSet = new HashSet<int>(productIds ?? new int[0]);
            foreach (string key in Request.Form.AllKeys)
            {
                if (key == null || !key.StartsWith("category_"))
                    continue;

                int radioProductId;
                if (int.TryParse(Request.Form[key], out radioProductId))
                    newSet.Add(radioProductId);
            }

            DataTable existingTable = DbHelper.QueryTable(
                // Only selections the modal could actually have posted (products in customizable
                // categories) -- non-customizable categories are shown read-only with no input, and
                // products with no category aren't shown at all, so neither was in the submitted
                // form and neither may be diffed away.
                @"SELECT cs.ProductID
                  FROM CustomizationSelections cs
                  JOIN Products pr ON pr.ProductID = cs.ProductID
                  JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                  WHERE cs.BookingID = @BookingID
                    AND mc.IsCustomizable = 1
                    AND cs.Status <> 'Rejected'",
                new SqlParameter("@BookingID", bookingId));

            var existingSet = new HashSet<int>();
            foreach (DataRow row in existingTable.Rows)
                existingSet.Add((int)row["ProductID"]);

            foreach (int productId in existingSet)
            {
                if (!newSet.Contains(productId))
                {
                    DbHelper.Execute(
                        "DELETE FROM CustomizationSelections WHERE BookingID = @BookingID AND ProductID = @ProductID AND Status <> 'Rejected'",
                        new SqlParameter("@BookingID", bookingId),
                        new SqlParameter("@ProductID", productId));
                }
            }

            foreach (int productId in newSet)
            {
                if (existingSet.Contains(productId))
                    continue;

                // A client can only newly select a product the Project Manager has assigned to THEIR
                // unit's building/project (the same rule the modal lists by): customizable, Admin-approved
                // and in ProductScopeAssignments. A forged post naming a read-only structural product, or
                // any catalog product nobody has assigned yet, is ignored. (Selections already saved are
                // never re-validated here, so un-offering a variant can't drop them.)
                object valid = DbHelper.ExecuteScalar(
                    @"SELECT pr.ProductID
                      FROM Products pr
                      JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                      JOIN UnitBookings b ON b.BookingID = @BookingID
                      JOIN Units u ON u.UnitID = b.UnitID
                      WHERE pr.ProductID = @ProductID AND mc.IsCustomizable = 1
                        AND pr.Category = 'Customizable' AND pr.IsApprovedForClientCustomization = 1
                        AND EXISTS (SELECT 1 FROM ProductScopeAssignments a
                                    WHERE a.ProductID = pr.ProductID
                                      AND (a.BuildingID = u.BuildingID
                                           OR (a.ProjectID = u.ProjectID AND a.BuildingID IS NULL)))",
                    new SqlParameter("@BookingID", bookingId),
                    new SqlParameter("@ProductID", productId));
                if (valid == null || valid == DBNull.Value)
                    continue;

                DbHelper.Execute(
                    "INSERT INTO CustomizationSelections (BookingID, ProductID, Status) VALUES (@BookingID, @ProductID, 'Pending')",
                    new SqlParameter("@BookingID", bookingId),
                    new SqlParameter("@ProductID", productId));
            }
            
            TempData["Success"] = "Customization selections saved.";

            // The Customize modal posts this via AJAX (see _CustomizeModal.cshtml) so it can close
            // itself and then reload; a non-AJAX post keeps the plain redirect.
            if (Request.IsAjaxRequest())
                return Json(new { success = true });

            return RedirectToAction("MyBookings");
        }

        // ---------- AI Advisor ----------

        // Feeds this booking's customizable-product list to Gemini as context so the advisor
        // only ever discusses options actually available on this booking, mirroring
        // HomeController.Chat's own "grounded in real data" approach for the landing page.
        [HttpPost]
        public async Task<JsonResult> AskAiAdvisor(int bookingId, string question)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Client")
                return Json(new { error = "Please log in as a client." });

            if (string.IsNullOrWhiteSpace(question))
                return Json(new { error = "Please type a question first." });

            int clientUserId = (int)Session["UserID"];

            if (!IsCustomizationReady(bookingId, clientUserId))
                return Json(new { error = NotReadyMessage });

            var model = BuildCustomizeViewModel(bookingId, clientUserId);
            if (model == null)
                return Json(new { error = "Booking not found." });

            var context = new StringBuilder();
            context.AppendLine("You are a helpful customization advisor for a real estate client finishing out their " +
                "new unit at " + model.ProjectName + ", " + model.BuildingName + ", Flat " + model.FlatNumber + ".");
            context.AppendLine("The unit's base price is Tk " + model.UnitBasePrice.ToString("N0") + ".");
            context.AppendLine("Here are the customization options available for this unit, grouped by category:");

            var advisorGroups = model.Products.GroupBy(p => p.CategoryName).ToDictionary(g => g.Key);
            foreach (string categoryName in model.CategoryNames.Where(n => !advisorGroups.ContainsKey(n)))
                context.AppendLine("Category: " + categoryName + " -- COMING SOON: no options have been assigned to this unit yet. " +
                    "Tell the client that; do not invent or suggest any options for it.");

            foreach (var group in advisorGroups.Values)
            {
                context.AppendLine("Category: " + group.Key);
                foreach (var p in group)
                {
                    string costNote = p.IsDefaultOption
                        ? "included at no extra cost"
                        : "extra cost Tk " + (p.ExtraCost * model.UnitSizeSqFt).ToString("N0") +
                          " (Tk " + p.ExtraCost.ToString("N2") + " per sq ft x " + model.UnitSizeSqFt.ToString("N0") + " sq ft)";
                    string selectedNote = p.IsSelected ? " [currently selected by this client]" : "";
                    if (!p.IsStillOffered)
                        selectedNote += " [NO LONGER OFFERED -- listed only because this client already selected it; do not recommend it to anyone]";
                    string descNote = string.IsNullOrEmpty(p.Description) ? "" : " - " + p.Description;

                    context.AppendLine("  - " + p.ProductName + descNote + ", price Tk " +
                        p.Price.ToString("N2") + ", " + costNote + selectedNote);
                }
            }

            context.AppendLine("Answer the client's question using only these options -- help them compare, decide, or " +
                "understand cost implications. If they ask about something not listed above, say so and suggest " +
                "they contact the project team directly.");

            try
            {
                var advisor = new GeminiAdvisorService();
                string reply = await advisor.AskAsync(context.ToString(), question);
                return Json(new { reply = reply });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // ---------- Payment history ----------

        // Per-booking Total/Paid/Balance for this client, one row per UnitBooking.
        private List<ClientBookingBreakdownModel> GetClientBookingBreakdown(int clientUserId)
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BookingID, p.ProjectName, bl.BuildingName, u.FlatNumber, b.TotalPrice,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.BookingID = b.BookingID), 0) AS Paid,
                         (SELECT ISNULL(SUM(pr.ExtraCost), 0) * u.SizeSqFt
                            FROM CustomizationSelections cs
                            JOIN Products pr ON pr.ProductID = cs.ProductID
                            JOIN MasterCategories mc ON mc.CategoryID = pr.CategoryID
                            WHERE cs.BookingID = b.BookingID AND cs.Status = 'Approved' AND pr.ExtraCost > 0
                              AND mc.IsCustomizable = 1) AS ExtraTotal
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  WHERE b.ClientUserID = @ClientUserID AND b.Status = 'Active'
                  ORDER BY b.BookingID DESC",
                new SqlParameter("@ClientUserID", clientUserId));

            var breakdown = new List<ClientBookingBreakdownModel>();
            foreach (DataRow row in table.Rows)
            {
                breakdown.Add(new ClientBookingBreakdownModel
                {
                    BookingID = (int)row["BookingID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    TotalPrice = (decimal)row["TotalPrice"],
                    ExtraTotal = (decimal)row["ExtraTotal"],
                    Paid = (decimal)row["Paid"]
                });
            }
            return breakdown;
        }

        public ActionResult Payments()
        {
            var guard = Guard();
            if (guard != null) return guard;

            int clientUserId = (int)Session["UserID"];

            var breakdown = GetClientBookingBreakdown(clientUserId);
            decimal totalPrice = breakdown.Sum(b => b.GrandTotal);
            decimal totalPaid = breakdown.Sum(b => b.Paid);
            ViewBag.TotalPrice = totalPrice;
            ViewBag.TotalPaid = totalPaid;
            ViewBag.Balance = totalPrice - totalPaid;
            ViewBag.BookingBreakdown = breakdown;

            DataTable table = DbHelper.QueryTable(
                @"SELECT cp.ClientPaymentID, cp.PaymentDate, p.ProjectName, bl.BuildingName, u.FlatNumber, cp.Amount
                  FROM ClientPayments cp
                  JOIN UnitBookings b ON b.BookingID = cp.BookingID
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  WHERE b.ClientUserID = @ClientUserID
                  ORDER BY cp.ClientPaymentID DESC",
                new SqlParameter("@ClientUserID", clientUserId));

            var payments = new List<ClientPaymentHistoryModel>();
            foreach (DataRow row in table.Rows)
            {
                payments.Add(new ClientPaymentHistoryModel
                {
                    PaymentID = (int)row["ClientPaymentID"],
                    PaymentDate = (DateTime)row["PaymentDate"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    Amount = (decimal)row["Amount"]
                });
            }

            return View(payments);
        }
    }
}
