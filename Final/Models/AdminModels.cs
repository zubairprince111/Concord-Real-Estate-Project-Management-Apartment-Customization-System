using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Final.Models
{
    public class ManageUsersViewModel
    {
        public List<UserModel> Users { get; set; } = new List<UserModel>();
        public List<SelectListItem> RoleOptions { get; set; } = new List<SelectListItem>();
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; }

        [Required(ErrorMessage = "Phone is required")]
        public string Phone { get; set; }
    }

    public class EditUserViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; }

        [Required(ErrorMessage = "Phone is required")]
        public string Phone { get; set; }
    }

    public class BookingAdminModel
    {
        public int BookingID { get; set; }
        public string ProjectName { get; set; }
        public string BuildingName { get; set; }
        public string FlatNumber { get; set; }
        public int UnitID { get; set; }
        public string UnitStatus { get; set; }
        public string ClientName { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public string CancelReason { get; set; }
    }

    public class CustomizationCatalogRow
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string CategoryName { get; set; }
        public bool IsApprovedForClientCustomization { get; set; }
        public decimal ExtraCost { get; set; }
    }

    public class MasterCategoryModel
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public bool IsCustomizable { get; set; }
    }

    public class CompanyReportRow
    {
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public decimal TotalClientPayments { get; set; }
    }

    public class ProjectAssignmentRow
    {
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public string Location { get; set; }
        public decimal Budget { get; set; }
        public string Status { get; set; }
        public int? ProjectManagerID { get; set; }
        public string ProjectManagerName { get; set; }
        public int? AccountOfficerID { get; set; }
        public string AccountOfficerName { get; set; }
    }

    public class AssignedProjectsViewModel
    {
        public List<ProjectAssignmentRow> Projects { get; set; } = new List<ProjectAssignmentRow>();
        public List<SelectListItem> ProjectManagerOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AccountOfficerOptions { get; set; } = new List<SelectListItem>();
    }

    public class RecentTransactionModel
    {
        public int PaymentID { get; set; }
        public DateTime PaymentDate { get; set; }
        public string ClientName { get; set; }
        public string ProjectName { get; set; }
        public string FlatNumber { get; set; }
        public decimal Amount { get; set; }
        public string RecordedByName { get; set; }
    }

    public class AdminReportsViewModel
    {
        public List<CompanyReportRow> ProjectSummaries { get; set; } = new List<CompanyReportRow>();
        public List<RecentTransactionModel> RecentTransactions { get; set; } = new List<RecentTransactionModel>();
    }
}
