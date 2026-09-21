using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    public class DashboardController : Controller
    {
        private bool IsAuthorized(string requiredRole)
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == requiredRole;
        }

        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");

            switch (Session["UserRole"].ToString())
            {
                case "Admin":
                    return RedirectToAction("AdminDashboard");
                case "Client":
                    return RedirectToAction("ClientDashboard");
                case "Project Manager":
                    return RedirectToAction("ProjectManagerDashboard");
                case "Accounts Officer":
                    return RedirectToAction("AccountsOfficerDashboard");
                default:
                    return RedirectToAction("AccessDenied", "Account");
            }
        }

        public ActionResult AdminDashboard()
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.ActiveProjectCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Projects WHERE Status = 'Active'");

            // Role-wise breakdown replaces the old flat "Total Users" count -- default each
            // to 0 first since a role with no accounts yet simply has no row in the GROUP BY.
            DataTable roleCounts = DbHelper.QueryTable(
                "SELECT Role, COUNT(*) AS RoleCount FROM Users GROUP BY Role");

            int adminCount = 0, clientCount = 0, projectManagerCount = 0, accountsOfficerCount = 0;
            foreach (DataRow row in roleCounts.Rows)
            {
                int count = (int)row["RoleCount"];
                switch (row["Role"].ToString())
                {
                    case "Admin": adminCount = count; break;
                    case "Client": clientCount = count; break;
                    case "Project Manager": projectManagerCount = count; break;
                    case "Accounts Officer": accountsOfficerCount = count; break;
                }
            }

            ViewBag.AdminCount = adminCount;
            ViewBag.ClientCount = clientCount;
            ViewBag.ProjectManagerCount = projectManagerCount;
            ViewBag.AccountsOfficerCount = accountsOfficerCount;

            ViewBag.TotalBuildingCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Buildings");

            ViewBag.TotalUnitCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Units");

            ViewBag.BookedUnitCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Units WHERE Status = 'Booked'");

            ViewBag.SoldUnitCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Units WHERE Status = 'Sold'");

            ViewBag.AvailableUnitCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Units WHERE Status = 'Available'");

            return View();
        }

        public ActionResult ClientDashboard()
        {
            if (!IsAuthorized("Client"))
                return RedirectToAction("AccessDenied", "Account");

            int clientUserId = (int)Session["UserID"];

            ViewBag.MyBookingCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM UnitBookings WHERE ClientUserID = @UserID AND Status = 'Active'",
                new SqlParameter("@UserID", clientUserId));

            return View();
        }

        public ActionResult ProjectManagerDashboard()
        {
            if (!IsAuthorized("Project Manager"))
                return RedirectToAction("AccessDenied", "Account");

            int userId = (int)Session["UserID"];

            ViewBag.PendingApprovalCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM CustomizationSelections WHERE Status = 'Pending'");
            ViewBag.ApprovedCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM CustomizationSelections WHERE Status = 'Approved'");
            ViewBag.RejectedCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM CustomizationSelections WHERE Status = 'Rejected'");

            // Fetch assigned projects for this PM
            DataTable pmProjectsTable = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName, p.Location, p.Budget, p.StartDate, p.EstimatedCompletionDate,
                         p.MaxBuildings, p.TotalAreaSqFt, p.Status,
                         ao.FullName AS AccountOfficerName,
                         (SELECT COUNT(*) FROM Buildings b WHERE b.ProjectID = p.ProjectID) AS BuildingCount,
                         (SELECT COUNT(*) FROM Units u WHERE u.ProjectID = p.ProjectID) AS UnitCount
                  FROM Projects p
                  LEFT JOIN Users ao ON ao.UserID = p.AccountOfficerID
                  WHERE p.ProjectManagerID = @UserID
                  ORDER BY p.ProjectID DESC",
                new SqlParameter("@UserID", userId));

            var pmProjects = new List<ProjectModel>();
            foreach (DataRow r in pmProjectsTable.Rows)
            {
                pmProjects.Add(new ProjectModel
                {
                    ProjectID = (int)r["ProjectID"],
                    ProjectName = r["ProjectName"].ToString(),
                    Location = r["Location"] == DBNull.Value ? null : r["Location"].ToString(),
                    Budget = (decimal)r["Budget"],
                    StartDate = r["StartDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["StartDate"],
                    EstimatedCompletionDate = r["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["EstimatedCompletionDate"],
                    MaxBuildings = r["MaxBuildings"] == DBNull.Value ? (int?)null : (int)r["MaxBuildings"],
                    TotalAreaSqFt = r["TotalAreaSqFt"] == DBNull.Value ? (decimal?)null : (decimal)r["TotalAreaSqFt"],
                    Status = r["Status"].ToString(),
                    AccountOfficerName = r["AccountOfficerName"] == DBNull.Value ? "Unassigned" : r["AccountOfficerName"].ToString()
                });
            }

            ViewBag.AssignedProjects = pmProjects;
            return View();
        }

        public ActionResult AccountsOfficerDashboard()
        {
            if (!IsAuthorized("Accounts Officer"))
                return RedirectToAction("AccessDenied", "Account");

            int userId = (int)Session["UserID"];

            ViewBag.MonthlyClientPaymentTotal = (decimal)DbHelper.ExecuteScalar(
                @"SELECT ISNULL(SUM(Amount), 0) FROM ClientPayments
                  WHERE MONTH(PaymentDate) = MONTH(GETDATE()) AND YEAR(PaymentDate) = YEAR(GETDATE())");

            // Fetch assigned projects for this Accounts Officer
            DataTable aoProjectsTable = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName, p.Location, p.Budget, p.StartDate, p.EstimatedCompletionDate,
                         p.Status,
                         pm.FullName AS ProjectManagerName,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.ProjectID = p.ProjectID), 0) AS TotalCollected,
                         (SELECT COUNT(*) FROM UnitBookings ub JOIN Units u ON u.UnitID = ub.UnitID WHERE u.ProjectID = p.ProjectID AND ub.Status = 'Active') AS BookingCount
                  FROM Projects p
                  LEFT JOIN Users pm ON pm.UserID = p.ProjectManagerID
                  WHERE p.AccountOfficerID = @UserID
                  ORDER BY p.ProjectID DESC",
                new SqlParameter("@UserID", userId));

            var aoProjects = new List<ProjectModel>();
            foreach (DataRow r in aoProjectsTable.Rows)
            {
                var proj = new ProjectModel
                {
                    ProjectID = (int)r["ProjectID"],
                    ProjectName = r["ProjectName"].ToString(),
                    Location = r["Location"] == DBNull.Value ? null : r["Location"].ToString(),
                    Budget = (decimal)r["Budget"],
                    StartDate = r["StartDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["StartDate"],
                    EstimatedCompletionDate = r["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["EstimatedCompletionDate"],
                    Status = r["Status"].ToString(),
                    ProjectManagerName = r["ProjectManagerName"] == DBNull.Value ? "Unassigned" : r["ProjectManagerName"].ToString()
                };
                ViewData["TotalCollected_" + proj.ProjectID] = (decimal)r["TotalCollected"];
                ViewData["BookingCount_" + proj.ProjectID] = (int)r["BookingCount"];
                aoProjects.Add(proj);
            }

            ViewBag.AssignedProjects = aoProjects;
            return View();
        }
    }
}
