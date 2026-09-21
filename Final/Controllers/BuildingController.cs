using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    public class BuildingController : Controller
    {
        private bool IsAuthorized(string requiredRole)
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == requiredRole;
        }

        private List<SelectListItem> GetProjectOptions()
        {
            DataTable table = DbHelper.QueryTable(
                "SELECT ProjectID, ProjectName FROM Projects ORDER BY ProjectID DESC");

            var options = new List<SelectListItem>();
            foreach (DataRow row in table.Rows)
            {
                options.Add(new SelectListItem
                {
                    Value = row["ProjectID"].ToString(),
                    Text = row["ProjectName"].ToString()
                });
            }
            return options;
        }

        // Create-only: Projects that haven't reached their Building limit yet. A NULL
        // MaxBuildings means no limit, so that project always stays eligible. Deliberately
        // NOT used by Edit -- a Building being edited must keep showing its current Project
        // in the dropdown even if that project is now at/over its limit, otherwise the form
        // would silently drop the building's own project out of the list.
        private List<SelectListItem> GetAvailableProjectOptionsForCreate()
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName
                  FROM Projects p
                  WHERE p.MaxBuildings IS NULL
                     OR (SELECT COUNT(*) FROM Buildings b WHERE b.ProjectID = p.ProjectID) < p.MaxBuildings
                  ORDER BY p.ProjectID DESC");

            var options = new List<SelectListItem>();
            foreach (DataRow row in table.Rows)
            {
                options.Add(new SelectListItem
                {
                    Value = row["ProjectID"].ToString(),
                    Text = row["ProjectName"].ToString()
                });
            }
            return options;
        }

        // Shared with HRController/ApprovalController (and any other place that lists Projects)
        // so a Project can always be shown together with the Buildings under it. A ProjectID
        // with no Buildings yet simply has no entry in the returned dictionary.
        public static Dictionary<int, string> GetBuildingNamesByProject()
        {
            DataTable table = DbHelper.QueryTable(
                "SELECT ProjectID, BuildingName FROM Buildings ORDER BY BuildingID DESC");

            var result = new Dictionary<int, string>();
            foreach (DataRow row in table.Rows)
            {
                int projectId = (int)row["ProjectID"];
                string buildingName = row["BuildingName"].ToString();
                result[projectId] = result.ContainsKey(projectId)
                    ? result[projectId] + ", " + buildingName
                    : buildingName;
            }
            return result;
        }

        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ProjectOptions = GetProjectOptions();
            ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
            return View(BuildBuildingList());
        }

        // Shared by Index and by the Create POST action when redisplaying the list page
        // with the create modal forced open after a validation failure.

        // Ordered by BuildingID DESC (newest building first) -- Views/Building/Index.cshtml
        // relies on this exact order when it groups this flat list by Project via LINQ
        // GroupBy, which preserves source order both across groups and within each group,
        // so the project containing the most recently added building surfaces first too.
        private List<BuildingModel> BuildBuildingList()
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BuildingID, b.ProjectID, p.ProjectName, b.BuildingName, b.TotalFloors, b.UnitsPerFloor, b.HasParkingFacility, b.EstimatedCompletionDate
          FROM Buildings b
          JOIN Projects p ON p.ProjectID = b.ProjectID
          ORDER BY b.BuildingID DESC");

            var buildings = new List<BuildingModel>();
            foreach (DataRow row in table.Rows)
            {
                buildings.Add(new BuildingModel
                {
                    BuildingID = (int)row["BuildingID"],
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    TotalFloors = (int)row["TotalFloors"],
                    UnitsPerFloor = row["UnitsPerFloor"] == DBNull.Value ? (int?)null : (int)row["UnitsPerFloor"],
                    HasParkingFacility = (bool)row["HasParkingFacility"],
                    EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"]
                });
            }
            return buildings;
        }

        // A Building's completion date must fall within its own Project's window -- not before
        // the Project's StartDate, and not after the Project's EstimatedCompletionDate (when
        // that's set). Returns the error message to attach to EstimatedCompletionDate, or null
        // when the date is fine.
        private string ValidateCompletionDateAgainstProject(int projectId, DateTime? buildingCompletionDate)
        {
            if (buildingCompletionDate == null)
                return null;

            DataRow project = DbHelper.QuerySingleRow(
                "SELECT StartDate, EstimatedCompletionDate FROM Projects WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", projectId));

            if (project == null)
                return null;

            DateTime projectStartDate = (DateTime)project["StartDate"];

            if (buildingCompletionDate.Value.Date < projectStartDate.Date)
            {
                return "Building completion date can't be before the project's start date (" +
                    projectStartDate.ToString("dd MMM yyyy") + ").";
            }

            if (project["EstimatedCompletionDate"] != DBNull.Value)
            {
                DateTime projectCompletionDate = (DateTime)project["EstimatedCompletionDate"];

                if (buildingCompletionDate.Value.Date > projectCompletionDate.Date)
                {
                    return "Building completion date can't be after the project's completion date (" +
                        projectCompletionDate.ToString("dd MMM yyyy") + ").";
                }
            }

            return null;
        }

        // AJAX source for the Create/Edit forms: once Admin picks a Project, this returns its
        // StartDate/EstimatedCompletionDate so the Building's own date picker can be bounded
        // to that window client-side (a convenience only -- ValidateCompletionDateAgainstProject
        // is the real enforcement).
        [HttpGet]
        public JsonResult ProjectCompletionDate(int projectId)
        {
            if (Session["UserRole"] == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            DataRow project = DbHelper.QuerySingleRow(
                "SELECT StartDate, EstimatedCompletionDate FROM Projects WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", projectId));

            if (project == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            string startDate = ((DateTime)project["StartDate"]).ToString("yyyy-MM-dd");
            string completionDate = (project["EstimatedCompletionDate"] == DBNull.Value)
                ? null
                : ((DateTime)project["EstimatedCompletionDate"]).ToString("yyyy-MM-dd");

            return Json(new { startDate = startDate, completionDate = completionDate }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.ProjectOptions = GetAvailableProjectOptionsForCreate();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BuildingModel model)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            if (!string.IsNullOrEmpty(model.BuildingName) && Regex.IsMatch(model.BuildingName, @"\d"))
                ModelState.AddModelError("BuildingName", "Building name cannot contain numbers.");

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "createModal";
                ViewData["CreateFormModel"] = model;
                return View("Index", BuildBuildingList());
            }

            // Respect UNIQUE(ProjectID, BuildingName).
            object existing = DbHelper.ExecuteScalar(
                "SELECT BuildingID FROM Buildings WHERE ProjectID = @ProjectID AND BuildingName = @BuildingName",
                new SqlParameter("@ProjectID", model.ProjectID),
                new SqlParameter("@BuildingName", model.BuildingName));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("BuildingName", "That building name already exists in the selected project.");
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "createModal";
                ViewData["CreateFormModel"] = model;
                return View("Index", BuildBuildingList());
            }

            // Re-checked here regardless of what the dropdown showed, so a manipulated POST
            // (spoofed ProjectID, or a project that hit its limit between page load and
            // submit) can never sneak a Building past the limit.
            object maxBuildingsRaw = DbHelper.ExecuteScalar(
                "SELECT MaxBuildings FROM Projects WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", model.ProjectID));

            if (maxBuildingsRaw != null && maxBuildingsRaw != DBNull.Value)
            {
                int maxBuildings = (int)maxBuildingsRaw;

                int existingCount = (int)DbHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM Buildings WHERE ProjectID = @ProjectID",
                    new SqlParameter("@ProjectID", model.ProjectID));

                if (existingCount >= maxBuildings)
                {
                    ModelState.AddModelError("ProjectID", "This project has reached its maximum of " + maxBuildings + " buildings.");
                    ViewBag.ProjectOptions = GetProjectOptions();
                    ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                    ViewBag.OpenModalId = "createModal";
                    ViewData["CreateFormModel"] = model;
                    return View("Index", BuildBuildingList());
                }
            }

            string completionDateError = ValidateCompletionDateAgainstProject(model.ProjectID, model.EstimatedCompletionDate);
            if (completionDateError != null)
            {
                ModelState.AddModelError("EstimatedCompletionDate", completionDateError);
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "createModal";
                ViewData["CreateFormModel"] = model;
                return View("Index", BuildBuildingList());
            }

            object completionDateValue = model.EstimatedCompletionDate == null
                ? (object)DBNull.Value
                : model.EstimatedCompletionDate;

            // Readiness gate: a brand-new building always starts NOT ready, even inside a project that is already
            // marked Ready (a building's own status row overrides its project's). It stays closed to clients until
            // a Project Manager reviews its materials and marks it ready on Assign Materials. Both rows are written
            // in one transaction so a building can never exist without its Not-ready row.
            DbHelper.Execute(
                @"SET XACT_ABORT ON;
                  BEGIN TRAN;
                  INSERT INTO Buildings (ProjectID, BuildingName, TotalFloors, UnitsPerFloor, HasParkingFacility, EstimatedCompletionDate)
                  VALUES (@ProjectID, @BuildingName, @TotalFloors, @UnitsPerFloor, @HasParkingFacility, @EstimatedCompletionDate);
                  INSERT INTO MaterialAssignmentStatus (ProjectID, BuildingID, IsReady)
                  VALUES (@ProjectID, SCOPE_IDENTITY(), 0);
                  COMMIT;",
                new SqlParameter("@ProjectID", model.ProjectID),
                new SqlParameter("@BuildingName", model.BuildingName),
                new SqlParameter("@TotalFloors", model.TotalFloors),
                new SqlParameter("@UnitsPerFloor", model.UnitsPerFloor),
                new SqlParameter("@HasParkingFacility", model.HasParkingFacility),
                new SqlParameter("@EstimatedCompletionDate", completionDateValue));

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            DataRow row = DbHelper.QuerySingleRow(
                @"SELECT BuildingID, ProjectID, BuildingName, TotalFloors, UnitsPerFloor, HasParkingFacility, EstimatedCompletionDate
  FROM Buildings WHERE BuildingID = @BuildingID",
                new SqlParameter("@BuildingID", id));

            if (row == null)
                return HttpNotFound();

            var model = new BuildingModel
            {
                BuildingID = (int)row["BuildingID"],
                ProjectID = (int)row["ProjectID"],
                BuildingName = row["BuildingName"].ToString(),
                TotalFloors = (int)row["TotalFloors"],
                UnitsPerFloor = row["UnitsPerFloor"] == DBNull.Value ? (int?)null : (int)row["UnitsPerFloor"],
                HasParkingFacility = (bool)row["HasParkingFacility"],
                EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"]
            };

            ViewBag.ProjectOptions = GetProjectOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(BuildingModel model)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            if (!string.IsNullOrEmpty(model.BuildingName) && Regex.IsMatch(model.BuildingName, @"\d"))
                ModelState.AddModelError("BuildingName", "Building name cannot contain numbers.");

            // A building's units, material status and payments all belong to its project,
            // so it can't be moved to a different one.
            object currentProject = DbHelper.ExecuteScalar(
                "SELECT ProjectID FROM Buildings WHERE BuildingID = @BuildingID",
                new SqlParameter("@BuildingID", model.BuildingID));

            if (currentProject != null && currentProject != DBNull.Value && (int)currentProject != model.ProjectID)
                ModelState.AddModelError("ProjectID", "A building cannot be moved to a different project.");

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "editModal_" + model.BuildingID;
                ViewData["EditFormModel_" + model.BuildingID] = model;
                return View("Index", BuildBuildingList());
            }

            // Respect UNIQUE(ProjectID, BuildingName), excluding this row.
            object existing = DbHelper.ExecuteScalar(
                "SELECT BuildingID FROM Buildings WHERE ProjectID = @ProjectID AND BuildingName = @BuildingName AND BuildingID <> @BuildingID",
                new SqlParameter("@ProjectID", model.ProjectID),
                new SqlParameter("@BuildingName", model.BuildingName),
                new SqlParameter("@BuildingID", model.BuildingID));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("BuildingName", "That building name already exists in the selected project.");
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "editModal_" + model.BuildingID;
                ViewData["EditFormModel_" + model.BuildingID] = model;
                return View("Index", BuildBuildingList());
            }

            string completionDateError = ValidateCompletionDateAgainstProject(model.ProjectID, model.EstimatedCompletionDate);
            if (completionDateError != null)
            {
                ModelState.AddModelError("EstimatedCompletionDate", completionDateError);
                ViewBag.ProjectOptions = GetProjectOptions();
                ViewBag.CreateProjectOptions = GetAvailableProjectOptionsForCreate();
                ViewBag.OpenModalId = "editModal_" + model.BuildingID;
                ViewData["EditFormModel_" + model.BuildingID] = model;
                return View("Index", BuildBuildingList());
            }

            object completionDateValue = model.EstimatedCompletionDate == null
                ? (object)DBNull.Value
                : model.EstimatedCompletionDate;

            DbHelper.Execute(
                @"UPDATE Buildings
                  SET ProjectID = @ProjectID, BuildingName = @BuildingName, TotalFloors = @TotalFloors,
                      UnitsPerFloor = @UnitsPerFloor, HasParkingFacility = @HasParkingFacility, EstimatedCompletionDate = @EstimatedCompletionDate
                  WHERE BuildingID = @BuildingID",
                new SqlParameter("@ProjectID", model.ProjectID),
                new SqlParameter("@BuildingName", model.BuildingName),
                new SqlParameter("@TotalFloors", model.TotalFloors),
                new SqlParameter("@UnitsPerFloor", model.UnitsPerFloor),
                new SqlParameter("@HasParkingFacility", model.HasParkingFacility),
                new SqlParameter("@EstimatedCompletionDate", completionDateValue),
                new SqlParameter("@BuildingID", model.BuildingID));

            return RedirectToAction("Index");
        }
    }
}
