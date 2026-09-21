using System;
using System.ComponentModel.DataAnnotations;

namespace Final.Models
{
    public class ProjectModel
    {
       
        public int ProjectID { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        public string ProjectName { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [RegularExpression(@"^[a-zA-Z\s,.-]+$", ErrorMessage = "Location must contain letters only, no numbers")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Budget is required")]
        [Range(0.01, 999999999.00, ErrorMessage = "Budget must be greater than zero")]
        public decimal Budget { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "Estimated completion date is required")]
        [DataType(DataType.Date)]
        public DateTime? EstimatedCompletionDate { get; set; }

        [Required(ErrorMessage = "Max Buildings is required")]
        public int? MaxBuildings { get; set; }

        [Required(ErrorMessage = "Total Area is required")]
        public decimal? TotalAreaSqFt { get; set; }

        public string Status { get; set; }
        public int CreatedByUserID { get; set; }
        public DateTime CreatedDate { get; set; }

        public int? ProjectManagerID { get; set; }
        public string ProjectManagerName { get; set; }
        public int? AccountOfficerID { get; set; }
        public string AccountOfficerName { get; set; }
    }
}
