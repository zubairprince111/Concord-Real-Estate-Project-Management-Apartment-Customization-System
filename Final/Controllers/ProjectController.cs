using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    public class ProjectController : Controller
    {
        private bool IsAuthorized(string requiredRole)
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == requiredRole;
        }

        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.StatusOptions = GetStatusOptions();
            return View(BuildProjectList());
        }

        // Shared by Index and by the Create/Edit POST actions when redisplaying the
        // list page with a modal forced open after a validation failure.
        private List<ProjectModel> BuildProjectList()
        {
            DataTable table = DbHelper.QueryTable(
    @"SELECT p.ProjectID, p.ProjectName, p.Location, p.Budget, p.StartDate, p.EstimatedCompletionDate,
             p.MaxBuildings, p.TotalAreaSqFt, p.Status, p.CreatedDate,
             p.ProjectManagerID, pm.FullName AS ProjectManagerName,
             p.AccountOfficerID, ao.FullName AS AccountOfficerName
      FROM Projects p
      LEFT JOIN Users pm ON pm.UserID = p.ProjectManagerID
      LEFT JOIN Users ao ON ao.UserID = p.AccountOfficerID
      ORDER BY p.ProjectID DESC");

            var projects = new List<ProjectModel>();
            foreach (DataRow row in table.Rows)
            {
                projects.Add(new ProjectModel
                {
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    Location = row["Location"] == DBNull.Value ? null : row["Location"].ToString(),
                    Budget = (decimal)row["Budget"],
                    StartDate = (DateTime)row["StartDate"],
                    EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"],
                    MaxBuildings = row["MaxBuildings"] == DBNull.Value ? (int?)null : (int)row["MaxBuildings"],
                    TotalAreaSqFt = row["TotalAreaSqFt"] == DBNull.Value ? (decimal?)null : (decimal)row["TotalAreaSqFt"],
                    Status = row["Status"].ToString(),
                    CreatedDate = (DateTime)row["CreatedDate"],
                    ProjectManagerID = row["ProjectManagerID"] == DBNull.Value ? (int?)null : (int)row["ProjectManagerID"],
                    ProjectManagerName = row["ProjectManagerName"] == DBNull.Value ? null : row["ProjectManagerName"].ToString(),
                    AccountOfficerID = row["AccountOfficerID"] == DBNull.Value ? (int?)null : (int)row["AccountOfficerID"],
                    AccountOfficerName = row["AccountOfficerName"] == DBNull.Value ? null : row["AccountOfficerName"].ToString()
                });
            }

            return projects;
        }

        [HttpGet]
        public ActionResult Create()
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ProjectModel model)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            ValidateDates(model);

            if (!ModelState.IsValid)
            {
                ViewBag.StatusOptions = GetStatusOptions();
                ViewBag.OpenModalId = "createModal";
                ViewData["CreateFormModel"] = model;
                return View("Index", BuildProjectList());
            }

            int createdByUserId = (int)Session["UserID"];

            object completionDateValue = (model.EstimatedCompletionDate == null ||
                model.EstimatedCompletionDate == DateTime.MinValue)
                ? (object)DBNull.Value
                : model.EstimatedCompletionDate;

            object maxBuildingsValue = model.MaxBuildings == null
                ? (object)DBNull.Value
                : model.MaxBuildings;

            object totalAreaSqFtValue = model.TotalAreaSqFt == null
                ? (object)DBNull.Value
                : model.TotalAreaSqFt;

            DbHelper.Execute(
                @"INSERT INTO Projects (ProjectName, Location, Budget, StartDate, EstimatedCompletionDate, MaxBuildings, TotalAreaSqFt, CreatedByUserID)
                  VALUES (@ProjectName, @Location, @Budget, @StartDate, @EstimatedCompletionDate, @MaxBuildings, @TotalAreaSqFt, @CreatedByUserID)",
                new SqlParameter("@ProjectName", model.ProjectName),
                new SqlParameter("@Location", (object)model.Location ?? DBNull.Value),
                new SqlParameter("@Budget", model.Budget),
                new SqlParameter("@StartDate", model.StartDate.Value),
                new SqlParameter("@EstimatedCompletionDate", completionDateValue),
                new SqlParameter("@MaxBuildings", maxBuildingsValue),
                new SqlParameter("@TotalAreaSqFt", totalAreaSqFtValue),
                new SqlParameter("@CreatedByUserID", createdByUserId));

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            DataRow row = DbHelper.QuerySingleRow(
               @"SELECT ProjectID, ProjectName, Location, Budget, StartDate, EstimatedCompletionDate, MaxBuildings, TotalAreaSqFt, Status
  FROM Projects WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", id));

            if (row == null)
                return HttpNotFound();

            var model = new ProjectModel
            {
                ProjectID = (int)row["ProjectID"],
                ProjectName = row["ProjectName"].ToString(),
                Location = row["Location"] == DBNull.Value ? null : row["Location"].ToString(),
                Budget = (decimal)row["Budget"],
                StartDate = (DateTime)row["StartDate"],
                EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"],
                MaxBuildings = row["MaxBuildings"] == DBNull.Value ? (int?)null : (int)row["MaxBuildings"],
                TotalAreaSqFt = row["TotalAreaSqFt"] == DBNull.Value ? (decimal?)null : (decimal)row["TotalAreaSqFt"],
                Status = row["Status"].ToString()
            };

            ViewBag.StatusOptions = GetStatusOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ProjectModel model)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            if (model.Status != "Active" && model.Status != "Completed" && model.Status != "OnHold")
                ModelState.AddModelError("Status", "Select a valid status.");

            ValidateDates(model);
            ValidateAgainstBuildings(model);

            if (!ModelState.IsValid)
            {
                ViewBag.StatusOptions = GetStatusOptions();
                ViewBag.OpenModalId = "editModal_" + model.ProjectID;
                ViewData["EditFormModel_" + model.ProjectID] = model;
                return View("Index", BuildProjectList());
            }

            object completionDateValue = (model.EstimatedCompletionDate == null ||
                model.EstimatedCompletionDate == DateTime.MinValue)
                ? (object)DBNull.Value
                : model.EstimatedCompletionDate;

            object maxBuildingsValue = model.MaxBuildings == null
                ? (object)DBNull.Value
                : model.MaxBuildings;

            object totalAreaSqFtValue = model.TotalAreaSqFt == null
                ? (object)DBNull.Value
                : model.TotalAreaSqFt;

            DbHelper.Execute(
                @"UPDATE Projects
                  SET ProjectName = @ProjectName, Location = @Location, Budget = @Budget,
                      StartDate = @StartDate, EstimatedCompletionDate = @EstimatedCompletionDate,
                      MaxBuildings = @MaxBuildings, TotalAreaSqFt = @TotalAreaSqFt, Status = @Status
                  WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectName", model.ProjectName),
                new SqlParameter("@Location", (object)model.Location ?? DBNull.Value),
                new SqlParameter("@Budget", model.Budget),
                new SqlParameter("@StartDate", model.StartDate.Value),
                new SqlParameter("@EstimatedCompletionDate", completionDateValue),
                new SqlParameter("@MaxBuildings", maxBuildingsValue),
                new SqlParameter("@TotalAreaSqFt", totalAreaSqFtValue),
                new SqlParameter("@Status", model.Status),
                new SqlParameter("@ProjectID", model.ProjectID));

            return RedirectToAction("Index");
        }

        // A project can't finish before it starts.
        private void ValidateDates(ProjectModel model)
        {
            if (model.StartDate != null && model.EstimatedCompletionDate != null &&
                model.EstimatedCompletionDate.Value.Date < model.StartDate.Value.Date)
                ModelState.AddModelError("EstimatedCompletionDate", "Completion date cannot be before the start date.");
        }

        // On edit, the project's dates and building limit must still fit the buildings it already has
        // (Building create/edit checks the same thing the other way round).
        private void ValidateAgainstBuildings(ProjectModel model)
        {
            DataRow buildings = DbHelper.QuerySingleRow(
                @"SELECT COUNT(*) AS BuildingCount,
                         MIN(EstimatedCompletionDate) AS FirstFinish,
                         MAX(EstimatedCompletionDate) AS LastFinish
                  FROM Buildings WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectID", model.ProjectID));

            int buildingCount = (int)buildings["BuildingCount"];

            if (model.MaxBuildings != null && model.MaxBuildings.Value < buildingCount)
                ModelState.AddModelError("MaxBuildings", "This project already has " + buildingCount + " buildings.");

            if (model.EstimatedCompletionDate != null && buildings["LastFinish"] != DBNull.Value)
            {
                DateTime lastFinish = (DateTime)buildings["LastFinish"];
                if (model.EstimatedCompletionDate.Value.Date < lastFinish.Date)
                    ModelState.AddModelError("EstimatedCompletionDate", "A building in this project finishes on " + lastFinish.ToString("dd MMM yyyy") + ", so the project cannot finish earlier.");
            }

            if (model.StartDate != null && buildings["FirstFinish"] != DBNull.Value)
            {
                DateTime firstFinish = (DateTime)buildings["FirstFinish"];
                if (model.StartDate.Value.Date > firstFinish.Date)
                    ModelState.AddModelError("StartDate", "A building in this project finishes on " + firstFinish.ToString("dd MMM yyyy") + ", so the project cannot start later.");
            }
        }

        private List<SelectListItem> GetStatusOptions()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Active", Text = "Active" },
                new SelectListItem { Value = "Completed", Text = "Completed" },
                new SelectListItem { Value = "OnHold", Text = "On Hold" }
            };
        }

    }
}
