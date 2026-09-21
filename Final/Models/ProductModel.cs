using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Final.Models
{
    // One variant of a category being added in a single Add Product submission (e.g. 3
    // different bathroom tap options in one go). Description doubles as the variant
    // name/spec -- no separate VariantLabel field, per the simpler reuse-Description design.
    public class VariantInput
    {
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; }

        // Checkbox: unchecked (default) means this variant is the included/default option
        // at no extra charge -- IsDefaultOption/ExtraCost/Price are then derived server-side
        // as true/0/0. Checked reveals the ExtraCost input for a premium choice, and derives
        // IsDefaultOption = false, Price = ExtraCost (see AdminController.AddProduct).
        public bool HasExtraCost { get; set; }

        [Range(0, 999999999.00, ErrorMessage = "Extra cost cannot be negative")]
        public decimal ExtraCost { get; set; }
    }

    // Add Product form: the Admin picks a MasterCategories row instead of typing a free-text
    // ProductName. Category is shared across every variant in Variants; one Products row is
    // inserted per variant on submit.
    public class AddProductViewModel
    {
        [Required(ErrorMessage = "Category is required")]
        public int CategoryID { get; set; }

        public List<VariantInput> Variants { get; set; } = new List<VariantInput> { new VariantInput() };

        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();
    }

}
