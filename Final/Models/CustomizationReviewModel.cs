using System;

namespace Final.Models
{
    // A Client's customizable-product selection awaiting Project Manager review.
    public class CustomizationReviewModel
    {
        public int SelectionID { get; set; }
        public int BookingID { get; set; }
        public string ProjectName { get; set; }
        public string BuildingName { get; set; }
        public string FlatNumber { get; set; }
        public string ClientName { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public DateTime SelectedDate { get; set; }
    }
}
