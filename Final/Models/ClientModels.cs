using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Final.Models
{
    public class BuildingModel

    {
        
        public int BuildingID { get; set; }

        [Required(ErrorMessage = "Select a project")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a project")]
        public int ProjectID { get; set; }

        // Populated for display in lists; not bound from the create form.
        public string ProjectName { get; set; }

        [Required(ErrorMessage = "Building name is required")]
        public string BuildingName { get; set; }

        [Required(ErrorMessage = "Total floors is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Total floors must be at least 1")]
        public int? TotalFloors { get; set; }

        [Required(ErrorMessage = "Units per floor is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Units per floor must be at least 1")]
        public int? UnitsPerFloor { get; set; }

        public bool HasParkingFacility { get; set; }

        [Required(ErrorMessage = "Estimated completion date is required")]
        [DataType(DataType.Date)]
        public DateTime? EstimatedCompletionDate { get; set; }
    }

    public class UnitModel
    {
        public int UnitID { get; set; }

        [Required(ErrorMessage = "Select a building")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a building")]
        public int BuildingID { get; set; }

        // Populated for display in lists; not bound from the create form.
        public string BuildingName { get; set; }

        // Derived server-side from the chosen Building, not bound from the create form.
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }

        [Required(ErrorMessage = "Flat number is required")]
        public string FlatNumber { get; set; }

        [Required(ErrorMessage = "Floor number is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Floor number must be zero or greater")]
        public int FloorNumber { get; set; }

        [Required(ErrorMessage = "Size is required")]
        [Range(0.01, 999999999.00, ErrorMessage = "Size must be greater than zero")]
        public decimal SizeSqFt { get; set; }

        [Required(ErrorMessage = "Bedroom count is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Bedroom count must be zero or greater")]
        public int BedroomCount { get; set; }

        [Required(ErrorMessage = "Bathroom count is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Bathroom count must be zero or greater")]
        public int BathroomCount { get; set; }

        // Optional: how many of BathroomCount are en-suite/attached. Not required since not
        // every unit's listing specifies this.
        [Range(0, int.MaxValue, ErrorMessage = "Attached bathrooms must be zero or greater")]
        public int? AttachedBathrooms { get; set; }

        public bool HasKitchen { get; set; }

        public bool HasHall { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Balcony count must be zero or greater")]
        public int? BalconyCount { get; set; }

        [Required(ErrorMessage = "Facing is required")]
        public string Facing { get; set; }

        [Required(ErrorMessage = "Base price is required")]
        [Range(0.01, 999999999.00, ErrorMessage = "Base price must be greater than zero")]
        public decimal BasePrice { get; set; }

        [Required(ErrorMessage = "Status is required")]
        public string Status { get; set; }

        // From the parent Project; null if not yet determined.
        public DateTime? EstimatedCompletionDate { get; set; }

        public string BhkSummary
        {
            get
            {
                var parts = new List<string>();
                parts.Add(BedroomCount + " Bed");

                string bathPart = BathroomCount + " Bath";
                if (AttachedBathrooms.HasValue && AttachedBathrooms.Value > 0)
                    bathPart += " (" + AttachedBathrooms.Value + " attached)";
                parts.Add(bathPart);

                if (HasKitchen) parts.Add("Kitchen");
                if (HasHall) parts.Add("Hall");
                if (BalconyCount.HasValue && BalconyCount.Value > 0) parts.Add(BalconyCount.Value + " Balcony");
                return string.Join(", ", parts);
            }
        }
    }

    // One "position" (A, B, C...) on every floor of a building being bulk-generated --
    // the same spec is applied to that position on every floor, e.g. every "B" unit
    // across all floors shares this template's size/price/facing/amenities.
    public class UnitPositionTemplate
    {
        // Assigned server-side (A, B, C...) from the position's index; not bound from the form.
        public string PositionLabel { get; set; }

        [Required(ErrorMessage = "Size is required")]
        [Range(0.01, 999999999.00, ErrorMessage = "Size must be greater than zero")]
        public decimal? SizeSqFt { get; set; }

        [Required(ErrorMessage = "Base price is required")]
        [Range(0.01, 999999999.00, ErrorMessage = "Base price must be greater than zero")]
        public decimal? BasePrice { get; set; }

        [Required(ErrorMessage = "Bedroom count is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Bedroom count must be zero or greater")]
        public int? BedroomCount { get; set; }

        [Required(ErrorMessage = "Bathroom count is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Bathroom count must be zero or greater")]
        public int? BathroomCount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Attached bathrooms must be zero or greater")]
        public int? AttachedBathrooms { get; set; }

        [Required(ErrorMessage = "Facing is required")]
        public string Facing { get; set; }

        public bool HasKitchen { get; set; }

        public bool HasHall { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Balcony count must be zero or greater")]
        public int? BalconyCount { get; set; }
    }

    public class GenerateUnitsViewModel
    {
        [Required(ErrorMessage = "Select a building")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a building")]
        public int BuildingID { get; set; }

        // Read-only display, sourced from the Building record via AJAX -- not re-entered here.
        public string BuildingName { get; set; }
        public int TotalFloors { get; set; }
        public int UnitsPerFloor { get; set; }
        public bool HasParkingFacility { get; set; }

        public List<UnitPositionTemplate> Positions { get; set; }

        public GenerateUnitsViewModel()
        {
            Positions = new List<UnitPositionTemplate>();
        }
    }

    public class ClientBookingSummary
    {
        public int BookingID { get; set; }
        public string ProjectName { get; set; }
        public string BuildingName { get; set; }
        public string FlatNumber { get; set; }
        public DateTime BookingDate { get; set; }

        // Computed live by the MyBookings() query: booking's stored base price plus current
        // selections' ExtraCost (per-sq-ft rate x the unit's SizeSqFt), same as what the modal calls "Total".
        public decimal TotalPrice { get; set; }
        public int CustomizationCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }

        // The Project Manager's readiness gate (MaterialAssignmentStatus): false = the Customize modal
        // is not built or opened for this booking yet.
        public bool CustomizationReady { get; set; }

        // From the Project; null if not yet determined.
        public DateTime? EstimatedCompletionDate { get; set; }
    }

    public class CustomizableProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public bool IsSelected { get; set; }

        // Set only when IsSelected: the review state of this booking's selection of this product.
        public string Status { get; set; }
        public string Remarks { get; set; }

        // Whether this is the included/default option for its category
        // (a premium, non-default choice adds an extra cost to the price).
        public bool IsDefaultOption { get; set; }

        // Products.ExtraCost: a per-square-foot RATE, not a flat amount. Multiply by the unit's
        // SizeSqFt (CustomizeViewModel.UnitSizeSqFt) for the actual charge on that unit.
        public decimal ExtraCost { get; set; }

        // MasterCategories.CategoryName, for grouping the Customize modal's category list.
        public string CategoryName { get; set; }

        // MasterCategories.IsCustomizable for this product's category -- true shows a checkbox
        // (Client can pick), false shows the item read-only (fixed spec, nothing to choose).
        public bool IsCustomizableCategory { get; set; }

        // False only for a customizable product this booking has ALREADY selected but that the
        // Project Manager has since un-offered for its project/building. It is still listed (and still
        // counted/billed) -- the modal just marks it "No longer offered".
        public bool IsStillOffered { get; set; } = true;

        // The rejection history is kept: when the client picked this product again after the Project Manager
        // rejected it, the newest selection is a fresh one and the old Rejected row stays on record. These say
        // the earlier rejection existed (and why) so the modal can show it next to the new status.
        public bool HasEarlierRejection { get; set; }
        public string EarlierRejectionReason { get; set; }

        // What the client (and Accounts) reads for this selection's review state.
        public string StatusLabel { get { return SelectionStatusLabel.For(Status, Remarks); } }
    }

    // One wording for a customization selection's status everywhere it is shown:
    // "Pending approval", "Approved", or "Rejected: <the PM's reason>".
    public static class SelectionStatusLabel
    {
        public static string For(string status, string remarks)
        {
            if (status == "Approved") return "Approved";
            if (status == "Pending") return "Pending approval";
            if (status == "Rejected")
                return string.IsNullOrWhiteSpace(remarks) ? "Rejected" : "Rejected: " + remarks.Trim();
            return status ?? "";
        }
    }

    public class CustomizeViewModel
    {
        public int BookingID { get; set; }
        public string ProjectName { get; set; }
        public string BuildingName { get; set; }
        public string FlatNumber { get; set; }
        public decimal UnitBasePrice { get; set; }
        public decimal UnitSizeSqFt { get; set; }
        // Only APPROVED customizations are billed: this is what "Selected Upgrades" and the Total include.
        public decimal CurrentExtraTotal { get; set; }

        // Selections still awaiting the Project Manager's decision -- shown, but not billed until approved.
        public decimal PendingExtraTotal { get; set; }

        // One unified, category-grouped list -- customizable categories show checkboxes,
        // non-customizable categories show the same rows read-only. No separate "Fixed" list.
        public List<CustomizableProduct> Products { get; set; } = new List<CustomizableProduct>();

        // Every MasterCategory name, alphabetical -- so the modal can show a "Coming soon" placeholder
        // for a category the Project Manager has not assigned anything to yet, instead of silently
        // dropping it. A client only ever sees products through Products (i.e. PM assignments).
        public List<string> CategoryNames { get; set; } = new List<string>();
    }

    public class ClientPaymentHistoryModel
    {
        public int PaymentID { get; set; }
        public DateTime PaymentDate { get; set; }
        public string ProjectName { get; set; }
        public string FlatNumber { get; set; }
        public decimal Amount { get; set; }
    }

    // Per-booking Total/Paid/Balance for one client, broken down by booking so partially-paid
    // units are clearly identifiable.
    public class ClientBookingBreakdownModel
    {
        public int BookingID { get; set; }
        public string ProjectName { get; set; }
        public string FlatNumber { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal ExtraTotal { get; set; }
        public decimal Paid { get; set; }
        public decimal GrandTotal { get { return TotalPrice + ExtraTotal; } }
        public decimal Balance { get { return GrandTotal - Paid; } }
    }
}
