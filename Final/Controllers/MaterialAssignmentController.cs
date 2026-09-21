using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Final.Data;

namespace Final.Controllers
{
    // Project Manager only: decides which of the Admin's catalog products each Project / Building
    // offers to its clients. The Admin builds the catalog (Category + variants, no scoping); this
    // writes ProductScopeAssignments, which ClientController.BuildCustomizeViewModel reads.
    public class MaterialAssignmentController : Controller
    {
        private bool IsAuthorized()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Project Manager";
        }

        // Same gate as ApprovalController.Index.
        private ActionResult Guard()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");
            return null;
        }

        // Serialises the replace-the-assignments writes below so two quick saves for the same scope
        // can't interleave and leave a structural category with two products.
        private static readonly object SaveLock = new object();

        public ActionResult Index()
        {
            var guard = Guard();
            if (guard != null) return guard;

            var projectOptions = new List<SelectListItem>();
            foreach (DataRow row in DbHelper.QueryTable("SELECT ProjectID, ProjectName FROM Projects ORDER BY ProjectName").Rows)
            {
                projectOptions.Add(new SelectListItem
                {
                    Value = row["ProjectID"].ToString(),
                    Text = row["ProjectName"].ToString()
                });
            }
            ViewBag.ProjectOptions = projectOptions;

            return View();
        }

        // Error message when the project doesn't exist or the building isn't one of its buildings; null when valid.
        private string ValidateScope(int projectId, int? buildingId)
        {
            object projectExists = DbHelper.ExecuteScalar(
                "SELECT ProjectID FROM Projects WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", projectId));
            if (projectExists == null || projectExists == DBNull.Value)
                return "Select a valid project.";

            if (buildingId.HasValue)
            {
                object buildingInProject = DbHelper.ExecuteScalar(
                    "SELECT BuildingID FROM Buildings WHERE BuildingID = @BuildingID AND ProjectID = @ProjectID",
                    new SqlParameter("@BuildingID", buildingId.Value),
                    new SqlParameter("@ProjectID", projectId));
                if (buildingInProject == null || buildingInProject == DBNull.Value)
                    return "Select a building that belongs to the chosen project.";
            }

            return null;
        }

        private bool TryGetCategory(int categoryId, out string categoryName, out bool isCustomizable)
        {
            DataRow row = DbHelper.QuerySingleRow(
                "SELECT CategoryName, IsCustomizable FROM MasterCategories WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", categoryId));
            categoryName = row == null ? null : row["CategoryName"].ToString();
            isCustomizable = row != null && (bool)row["IsCustomizable"];
            return row != null;
        }

        private JsonResult Fail(string message)
        {
            return Json(new { success = false, message = message });
        }

        private static SqlParameter BuildingParam(int? buildingId)
        {
            return new SqlParameter("@BuildingID", SqlDbType.Int) { Value = (object)buildingId ?? DBNull.Value };
        }

        // Readiness gate state for one scope (MaterialAssignmentStatus): this scope's own row if it has one,
        // otherwise -- for a building -- its project's, otherwise not ready. `source` tells the screen
        // whether the state was set here ("building" / "project"), inherited from the whole project
        // ("project-inherited"), or is just the default because nothing was ever set ("none").
        private object GetReadiness(int projectId, int? buildingId)
        {
            // A building's own row overrides its project's, so setting the whole project does not reach a
            // building that has its own setting. For the whole-project scope, list those buildings so the
            // screen can show them (and offer to apply the change to them too).
            var overrides = new List<object>();
            if (!buildingId.HasValue)
            {
                foreach (DataRow row in DbHelper.QueryTable(
                    @"SELECT b.BuildingID, b.BuildingName, s.IsReady
                      FROM MaterialAssignmentStatus s
                      JOIN Buildings b ON b.BuildingID = s.BuildingID
                      WHERE s.ProjectID = @ProjectID
                      ORDER BY b.BuildingName",
                    new SqlParameter("@ProjectID", projectId)).Rows)
                {
                    overrides.Add(new { buildingId = (int)row["BuildingID"], name = row["BuildingName"].ToString(), isReady = (bool)row["IsReady"] });
                }
            }

            DataRow exact = DbHelper.QuerySingleRow(
                @"SELECT IsReady FROM MaterialAssignmentStatus
                  WHERE ProjectID = @ProjectID AND ((@BuildingID IS NULL AND BuildingID IS NULL) OR BuildingID = @BuildingID)",
                new SqlParameter("@ProjectID", projectId),
                BuildingParam(buildingId));
            if (exact != null)
                return new { isReady = (bool)exact["IsReady"], source = buildingId.HasValue ? "building" : "project", overrides = overrides };

            if (buildingId.HasValue)
            {
                DataRow project = DbHelper.QuerySingleRow(
                    "SELECT IsReady FROM MaterialAssignmentStatus WHERE ProjectID = @ProjectID AND BuildingID IS NULL",
                    new SqlParameter("@ProjectID", projectId));
                if (project != null)
                    return new { isReady = (bool)project["IsReady"], source = "project-inherited", overrides = overrides };
            }

            return new { isReady = false, source = "none", overrides = overrides };
        }

        // The buildings of one project, labelled "ProjectName - BuildingName", for the Building dropdown.
        [HttpGet]
        public ActionResult Buildings(int projectId)
        {
            if (!IsAuthorized())
                return new HttpStatusCodeResult(403);

            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BuildingID, p.ProjectName + ' - ' + b.BuildingName AS Label
                  FROM Buildings b
                  JOIN Projects p ON p.ProjectID = b.ProjectID
                  WHERE b.ProjectID = @ProjectID
                  ORDER BY b.BuildingName",
                new SqlParameter("@ProjectID", projectId));

            var buildings = new List<object>();
            foreach (DataRow row in table.Rows)
                buildings.Add(new { id = (int)row["BuildingID"], label = row["Label"].ToString() });

            return Json(buildings, JsonRequestBehavior.AllowGet);
        }

        // Everything the screen shows for one scope (Project, optionally one Building), all
        // categories in one response.
        //  - Structural: the Admin's catalog products for the category (to choose ONE from), which one is
        //    assigned to exactly this scope, and -- when none is -- what clients here fall back to.
        //  - Customizable: the Admin's approved variants, whether each is offered at exactly this scope,
        //    and (for a building) whether it is already offered project-wide.
        [HttpGet]
        public ActionResult Load(int projectId, int? buildingId)
        {
            if (!IsAuthorized())
                return new HttpStatusCodeResult(403);

            string scopeError = ValidateScope(projectId, buildingId);
            if (scopeError != null)
                return Json(new { error = scopeError }, JsonRequestBehavior.AllowGet);

            var categories = new List<object>();
            foreach (DataRow cat in DbHelper.QueryTable(
                "SELECT CategoryID, CategoryName, IsCustomizable FROM MasterCategories ORDER BY IsCustomizable, CategoryName").Rows)
            {
                int categoryId = (int)cat["CategoryID"];
                bool isCustomizable = (bool)cat["IsCustomizable"];

                if (isCustomizable)
                {
                    var variants = new List<object>();
                    foreach (DataRow v in DbHelper.QueryTable(
                        @"SELECT p.ProductID, p.Description, p.ExtraCost,
                                 CASE WHEN EXISTS (SELECT 1 FROM ProductScopeAssignments a
                                                   WHERE a.ProductID = p.ProductID AND a.ProjectID = @ProjectID
                                                     AND ((@BuildingID IS NULL AND a.BuildingID IS NULL) OR a.BuildingID = @BuildingID))
                                      THEN 1 ELSE 0 END AS Assigned,
                                 CASE WHEN @BuildingID IS NOT NULL AND EXISTS (SELECT 1 FROM ProductScopeAssignments a
                                                   WHERE a.ProductID = p.ProductID AND a.ProjectID = @ProjectID AND a.BuildingID IS NULL)
                                      THEN 1 ELSE 0 END AS ViaProject
                          FROM Products p
                          WHERE p.CategoryID = @CategoryID AND p.Category = 'Customizable' AND p.IsApprovedForClientCustomization = 1
                          ORDER BY p.ProductID",
                        new SqlParameter("@CategoryID", categoryId),
                        new SqlParameter("@ProjectID", projectId),
                        BuildingParam(buildingId)).Rows)
                    {
                        variants.Add(new
                        {
                            id = (int)v["ProductID"],
                            description = v["Description"] == DBNull.Value ? "" : v["Description"].ToString(),
                            extraCost = ((decimal)v["ExtraCost"]).ToString("0.00"),
                            assigned = Convert.ToInt32(v["Assigned"]) == 1,
                            viaProject = Convert.ToInt32(v["ViaProject"]) == 1
                        });
                    }

                    categories.Add(new
                    {
                        id = categoryId,
                        name = cat["CategoryName"].ToString(),
                        isCustomizable = true,
                        variants = variants
                    });
                    continue;
                }

                var options = new List<object>();
                foreach (DataRow o in DbHelper.QueryTable(
                    "SELECT ProductID, Description, ExtraCost FROM Products WHERE CategoryID = @CategoryID ORDER BY ProductID",
                    new SqlParameter("@CategoryID", categoryId)).Rows)
                {
                    options.Add(new
                    {
                        id = (int)o["ProductID"],
                        description = o["Description"] == DBNull.Value ? "" : o["Description"].ToString(),
                        extraCost = ((decimal)o["ExtraCost"]).ToString("0.00")
                    });
                }

                DataRow assigned = DbHelper.QuerySingleRow(
                    @"SELECT TOP 1 a.ProductID
                      FROM ProductScopeAssignments a
                      JOIN Products x ON x.ProductID = a.ProductID
                      WHERE x.CategoryID = @CategoryID AND a.ProjectID = @ProjectID
                        AND ((@BuildingID IS NULL AND a.BuildingID IS NULL) OR a.BuildingID = @BuildingID)
                      ORDER BY a.ProductID",
                    new SqlParameter("@CategoryID", categoryId),
                    new SqlParameter("@ProjectID", projectId),
                    BuildingParam(buildingId));

                object inherited = null;
                if (assigned == null)
                {
                    DataRow fallback = null;
                    string level = null;
                    if (buildingId.HasValue)
                    {
                        fallback = DbHelper.QuerySingleRow(
                            @"SELECT TOP 1 x.Description
                              FROM ProductScopeAssignments a JOIN Products x ON x.ProductID = a.ProductID
                              WHERE x.CategoryID = @CategoryID AND a.ProjectID = @ProjectID AND a.BuildingID IS NULL
                              ORDER BY a.ProductID",
                            new SqlParameter("@CategoryID", categoryId),
                            new SqlParameter("@ProjectID", projectId));
                        level = "the whole project";
                    }
                    if (fallback == null)
                    {
                        fallback = DbHelper.QuerySingleRow(
                            @"SELECT TOP 1 x.Description
                              FROM ProductScopeAssignments a JOIN Products x ON x.ProductID = a.ProductID
                              WHERE x.CategoryID = @CategoryID AND a.ProjectID IS NULL AND a.BuildingID IS NULL
                              ORDER BY a.ProductID",
                            new SqlParameter("@CategoryID", categoryId));
                        level = "all projects (global)";
                    }
                    if (fallback != null)
                    {
                        inherited = new
                        {
                            level = level,
                            description = fallback["Description"] == DBNull.Value ? "" : fallback["Description"].ToString()
                        };
                    }
                }

                categories.Add(new
                {
                    id = categoryId,
                    name = cat["CategoryName"].ToString(),
                    isCustomizable = false,
                    options = options,
                    assignedProductId = assigned == null ? (int?)null : (int)assigned["ProductID"],
                    inherited = inherited
                });
            }

            return Json(new { categories = categories, readiness = GetReadiness(projectId, buildingId) }, JsonRequestBehavior.AllowGet);
        }

        // The readiness gate: only this explicit Mark / Unmark flips it -- saving material assignments never
        // does. Ready lets clients booked in the scope open Customize (ClientController checks the building's
        // row, then the project's); not ready blocks the modal entirely. A scope's row is updated, or
        // created on first use, never duplicated. Unmarking keeps the row as an explicit "not ready" so a
        // building can be held back even when its project is ready.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetReadiness(int projectId, int? buildingId, bool isReady, bool applyToBuildings = false)
        {
            if (!IsAuthorized())
                return new HttpStatusCodeResult(403);

            string scopeError = ValidateScope(projectId, buildingId);
            if (scopeError != null)
                return Fail(scopeError);

            object markedBy = DBNull.Value;
            object markedDate = DBNull.Value;
            if (isReady)
            {
                markedBy = Session["UserID"] is int ? (object)(int)Session["UserID"] : DBNull.Value;
                markedDate = DateTime.Now;
            }

            lock (SaveLock)
            {
                int updated = DbHelper.Execute(
                    @"UPDATE MaterialAssignmentStatus
                      SET IsReady = @IsReady, MarkedReadyByUserID = @By, MarkedReadyDate = @Date
                      WHERE ProjectID = @ProjectID AND ((@BuildingID IS NULL AND BuildingID IS NULL) OR BuildingID = @BuildingID)",
                    new SqlParameter("@IsReady", isReady),
                    new SqlParameter("@By", SqlDbType.Int) { Value = markedBy },
                    new SqlParameter("@Date", SqlDbType.DateTime) { Value = markedDate },
                    new SqlParameter("@ProjectID", projectId),
                    BuildingParam(buildingId));

                if (updated == 0)
                {
                    DbHelper.Execute(
                        @"INSERT INTO MaterialAssignmentStatus (ProjectID, BuildingID, IsReady, MarkedReadyByUserID, MarkedReadyDate)
                          VALUES (@ProjectID, @BuildingID, @IsReady, @By, @Date)",
                        new SqlParameter("@ProjectID", projectId),
                        BuildingParam(buildingId),
                        new SqlParameter("@IsReady", isReady),
                        new SqlParameter("@By", SqlDbType.Int) { Value = markedBy },
                        new SqlParameter("@Date", SqlDbType.DateTime) { Value = markedDate });
                }
            }

            // Whole-project scope only: also set every building of this project that carries its OWN status, so
            // "not ready" for the project really closes the buildings underneath it (they would otherwise keep
            // their own "ready" and stay open). Only existing building rows change; none are created.
            int buildingsChanged = 0;
            if (!buildingId.HasValue && applyToBuildings)
            {
                lock (SaveLock)
                {
                    buildingsChanged = DbHelper.Execute(
                        @"UPDATE MaterialAssignmentStatus
                          SET IsReady = @IsReady, MarkedReadyByUserID = @By, MarkedReadyDate = @Date
                          WHERE ProjectID = @ProjectID AND BuildingID IS NOT NULL AND IsReady <> @IsReady",
                        new SqlParameter("@IsReady", isReady),
                        new SqlParameter("@By", SqlDbType.Int) { Value = markedBy },
                        new SqlParameter("@Date", SqlDbType.DateTime) { Value = markedDate },
                        new SqlParameter("@ProjectID", projectId));
                }
            }

            string scope = buildingId.HasValue ? "this building" : "this project";
            string message = isReady
                ? "Marked ready \u2014 clients booked in " + scope + " can now open Customize."
                : "Marked not ready \u2014 clients booked in " + scope + " can't open Customize until it is marked ready again.";
            if (buildingsChanged > 0)
                message += " Also applied to " + buildingsChanged + " building(s) that had their own setting.";

            return Json(new { success = true, message = message, readiness = GetReadiness(projectId, buildingId) });
        }

        // Sets the ONE product a structural category offers at this exact scope. Replaces whatever
        // was assigned there (never adds a second), or clears the scope when productId is blank --
        // clients then fall back to the project-wide / global assignment.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveStructural(int categoryId, int projectId, int? buildingId, int? productId)
        {
            if (!IsAuthorized())
                return new HttpStatusCodeResult(403);

            string categoryName;
            bool isCustomizable;
            if (!TryGetCategory(categoryId, out categoryName, out isCustomizable))
                return Fail("Select a valid category.");
            if (isCustomizable)
                return Fail("This category is customizable -- tick the variants to offer instead.");

            string scopeError = ValidateScope(projectId, buildingId);
            if (scopeError != null)
                return Fail(scopeError);

            if (productId.HasValue)
            {
                object inCategory = DbHelper.ExecuteScalar(
                    "SELECT ProductID FROM Products WHERE ProductID = @ProductID AND CategoryID = @CategoryID",
                    new SqlParameter("@ProductID", productId.Value),
                    new SqlParameter("@CategoryID", categoryId));
                if (inCategory == null || inCategory == DBNull.Value)
                    return Fail("Pick a product from this category's catalog.");
            }

            lock (SaveLock)
            {
                DbHelper.Execute(
                    @"SET XACT_ABORT ON;
                      BEGIN TRAN;
                      DELETE a
                      FROM ProductScopeAssignments a
                      JOIN Products x ON x.ProductID = a.ProductID
                      WHERE x.CategoryID = @CategoryID AND a.ProjectID = @ProjectID
                        AND ((@BuildingID IS NULL AND a.BuildingID IS NULL) OR a.BuildingID = @BuildingID);
                      IF @ProductID IS NOT NULL
                          INSERT INTO ProductScopeAssignments (ProductID, ProjectID, BuildingID)
                          VALUES (@ProductID, @ProjectID, @BuildingID);
                      COMMIT;",
                    new SqlParameter("@CategoryID", categoryId),
                    new SqlParameter("@ProjectID", projectId),
                    BuildingParam(buildingId),
                    new SqlParameter("@ProductID", SqlDbType.Int) { Value = (object)productId ?? DBNull.Value });
            }

            return Json(new
            {
                success = true,
                message = productId.HasValue ? categoryName + " assigned." : categoryName + " cleared for this scope."
            });
        }

        // The variants of this category currently assigned at exactly this scope but NOT in the new
        // set are about to be un-offered. For each, counts the ACTIVE bookings in scope that have it
        // selected (Pending or Approved) and that will not still be offered it some other way -- a
        // building-level assignment when un-offering project-wide, or the project-wide one when
        // un-offering for a single building. Returns a warning for the PM, or null when nobody is affected.
        private string DescribeAffectedSelections(int categoryId, int projectId, int? buildingId, int[] chosen)
        {
            DataTable assignedNow = DbHelper.QueryTable(
                @"SELECT a.ProductID
                  FROM ProductScopeAssignments a
                  JOIN Products x ON x.ProductID = a.ProductID
                  WHERE x.CategoryID = @CategoryID AND a.ProjectID = @ProjectID
                    AND ((@BuildingID IS NULL AND a.BuildingID IS NULL) OR a.BuildingID = @BuildingID)",
                new SqlParameter("@CategoryID", categoryId),
                new SqlParameter("@ProjectID", projectId),
                BuildingParam(buildingId));

            var removed = new List<int>();
            foreach (DataRow row in assignedNow.Rows)
            {
                int id = (int)row["ProductID"];
                if (!chosen.Contains(id))
                    removed.Add(id);
            }
            if (removed.Count == 0)
                return null;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@ProjectID", projectId),
                BuildingParam(buildingId)
            };
            var names = new List<string>();
            for (int i = 0; i < removed.Count; i++)
            {
                names.Add("@r" + i);
                parameters.Add(new SqlParameter("@r" + i, removed[i]));
            }

            DataTable affected = DbHelper.QueryTable(
                @"SELECT p.Description, COUNT(DISTINCT cs.BookingID) AS Bookings
                  FROM CustomizationSelections cs
                  JOIN Products p ON p.ProductID = cs.ProductID
                  JOIN UnitBookings b ON b.BookingID = cs.BookingID AND b.Status = 'Active'
                  JOIN Units u ON u.UnitID = b.UnitID
                  WHERE cs.ProductID IN (" + string.Join(",", names) + @")
                    AND cs.Status IN ('Pending', 'Approved')
                    AND u.ProjectID = @ProjectID
                    AND (@BuildingID IS NULL OR u.BuildingID = @BuildingID)
                    AND NOT (@BuildingID IS NULL AND EXISTS
                             (SELECT 1 FROM ProductScopeAssignments a2
                              WHERE a2.ProductID = cs.ProductID AND a2.ProjectID = @ProjectID AND a2.BuildingID = u.BuildingID))
                    AND NOT (@BuildingID IS NOT NULL AND EXISTS
                             (SELECT 1 FROM ProductScopeAssignments a3
                              WHERE a3.ProductID = cs.ProductID AND a3.ProjectID = @ProjectID AND a3.BuildingID IS NULL))
                  GROUP BY cs.ProductID, p.Description
                  ORDER BY cs.ProductID",
                parameters.ToArray());

            if (affected.Rows.Count == 0)
                return null;

            var message = new StringBuilder("Clients have already selected variant(s) you are about to un-offer:\n");
            foreach (DataRow row in affected.Rows)
                message.AppendFormat("\n\u2022 {0} \u2014 {1} booking(s)", row["Description"], row["Bookings"]);
            message.Append("\n\nTheir selections are NOT removed: they stay saved and billable, and show as \"No longer offered\" to those clients.");
            return message.ToString();
        }

        // Sets exactly which of the Admin's approved variants a customizable category offers at this
        // scope (any number, including none). A variant already offered project-wide is not stored
        // again for one of its buildings.
        // Un-offering a variant that clients have already selected does NOT touch their selections --
        // they stay saved and billable (the client's modal shows them as "No longer offered"). Because
        // that is easy to do by accident, the first save is refused with needsConfirmation and a list
        // of how many bookings are affected; the screen asks the PM and re-posts with confirmed = true.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCustomizable(int categoryId, int projectId, int? buildingId, int[] productIds, bool confirmed = false)
        {
            if (!IsAuthorized())
                return new HttpStatusCodeResult(403);

            string categoryName;
            bool isCustomizable;
            if (!TryGetCategory(categoryId, out categoryName, out isCustomizable))
                return Fail("Select a valid category.");
            if (!isCustomizable)
                return Fail("This category is structural -- pick the one product to assign instead.");

            string scopeError = ValidateScope(projectId, buildingId);
            if (scopeError != null)
                return Fail(scopeError);

            int[] chosen = (productIds ?? new int[0]).Distinct().ToArray();

            if (chosen.Length > 0)
            {
                // Every chosen id must be an approved variant of THIS category.
                var idParams = new List<SqlParameter> { new SqlParameter("@CategoryID", categoryId) };
                var names = new List<string>();
                for (int i = 0; i < chosen.Length; i++)
                {
                    names.Add("@id" + i);
                    idParams.Add(new SqlParameter("@id" + i, chosen[i]));
                }
                int valid = (int)DbHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM Products WHERE CategoryID = @CategoryID AND Category = 'Customizable' AND IsApprovedForClientCustomization = 1 AND ProductID IN (" +
                    string.Join(",", names) + ")",
                    idParams.ToArray());
                if (valid != chosen.Length)
                    return Fail("Pick variants from this category's approved catalog.");
            }

            if (!confirmed)
            {
                string warning = DescribeAffectedSelections(categoryId, projectId, buildingId, chosen);
                if (warning != null)
                    return Json(new { success = false, needsConfirmation = true, message = warning });
            }

            var sql = new StringBuilder(
                @"SET XACT_ABORT ON;
                  BEGIN TRAN;
                  DELETE a
                  FROM ProductScopeAssignments a
                  JOIN Products x ON x.ProductID = a.ProductID
                  WHERE x.CategoryID = @CategoryID AND a.ProjectID = @ProjectID
                    AND ((@BuildingID IS NULL AND a.BuildingID IS NULL) OR a.BuildingID = @BuildingID);");
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@CategoryID", categoryId),
                new SqlParameter("@ProjectID", projectId),
                BuildingParam(buildingId)
            };
            for (int i = 0; i < chosen.Length; i++)
            {
                sql.Append(
                    @" INSERT INTO ProductScopeAssignments (ProductID, ProjectID, BuildingID)
                       SELECT @p" + i + @", @ProjectID, @BuildingID
                       WHERE NOT (@BuildingID IS NOT NULL AND EXISTS
                             (SELECT 1 FROM ProductScopeAssignments w
                              WHERE w.ProductID = @p" + i + @" AND w.ProjectID = @ProjectID AND w.BuildingID IS NULL));");
                parameters.Add(new SqlParameter("@p" + i, chosen[i]));
            }
            sql.Append(" COMMIT;");

            lock (SaveLock)
            {
                DbHelper.Execute(sql.ToString(), parameters.ToArray());
            }

            return Json(new { success = true, message = categoryName + " variants saved (" + chosen.Length + " offered)." });
        }
    }
}
