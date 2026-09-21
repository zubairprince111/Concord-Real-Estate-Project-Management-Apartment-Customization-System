using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;
using Final.Security;

namespace Final.Controllers
{
    public class AdminController : Controller
    {
        // Exact role strings allowed by the Users.Role CHECK constraint.
        private static readonly string[] Roles =
        {
            "Admin", "Client", "Project Manager", "Accounts Officer"
        };

        private bool IsAuthorized()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Admin";
        }

        private ActionResult Guard()
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Account");
            return null;
        }

        // ---------- Manage Users ----------

        public ActionResult Users()
        {
            var guard = Guard();
            if (guard != null) return guard;

            var model = BuildManageUsersViewModel();
            ViewBag.RoleOptions = model.RoleOptions;
            return View(model);
        }

        // Shared by Users and by CreateUser when redisplaying the list page with the
        // create modal forced open after a validation failure.
        private ManageUsersViewModel BuildManageUsersViewModel()
        {
            var model = new ManageUsersViewModel();

            DataTable table = DbHelper.QueryTable(
                @"SELECT UserID, FullName, Email, Role, Phone, IsActive, CreatedDate
                  FROM Users ORDER BY UserID DESC");

            foreach (DataRow row in table.Rows)
            {
                model.Users.Add(new UserModel
                {
                    UserID = (int)row["UserID"],
                    FullName = row["FullName"].ToString(),
                    Email = row["Email"].ToString(),
                    Role = row["Role"].ToString(),
                    Phone = row["Phone"] == DBNull.Value ? null : row["Phone"].ToString(),
                    IsActive = (bool)row["IsActive"],
                    CreatedDate = (DateTime)row["CreatedDate"],

                });
            }

            foreach (string role in Roles)
                model.RoleOptions.Add(new SelectListItem { Value = role, Text = role });

            return model;
        }

        private ActionResult RedisplayUsersWithCreateModalOpen(CreateUserViewModel model)
        {
            var vm = BuildManageUsersViewModel();
            ViewBag.RoleOptions = vm.RoleOptions;
            ViewBag.OpenModalId = "createModal";
            ViewData["CreateFormModel"] = model;
            return View("Users", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser(CreateUserViewModel model)
        {
            var guard = Guard();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return RedisplayUsersWithCreateModalOpen(model);

            if (Array.IndexOf(Roles, model.Role) < 0)
            {
                TempData["Error"] = "Select a valid role.";
                return RedisplayUsersWithCreateModalOpen(model);
            }

            // Email is UNIQUE in the schema; check first for a friendly message.
            object existing = DbHelper.ExecuteScalar(
                "SELECT UserID FROM Users WHERE Email = @Email",
                new SqlParameter("@Email", model.Email));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("Email", "A user with that email already exists.");
                return RedisplayUsersWithCreateModalOpen(model);
            }

            string passwordHash = PasswordHasher.Hash(model.Password);

            DbHelper.Execute(
                @"INSERT INTO Users (FullName, Email, PasswordHash, Role, Phone)
                  VALUES (@FullName, @Email, @PasswordHash, @Role, @Phone)",
                new SqlParameter("@FullName", model.FullName),
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Role", model.Role),
                new SqlParameter("@Phone", (object)model.Phone ?? DBNull.Value));

            TempData["Success"] = string.Format("User '{0}' created as {1}.", model.FullName, model.Role);
            return RedirectToAction("Users");
        }

        [HttpGet]
        public ActionResult EditUser(int id)
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataRow row = DbHelper.QuerySingleRow(
                "SELECT UserID, FullName, Email, Role, Phone FROM Users WHERE UserID = @UserID",
                new SqlParameter("@UserID", id));

            if (row == null)
                return HttpNotFound();

            var model = new EditUserViewModel
            {
                UserID = (int)row["UserID"],
                FullName = row["FullName"].ToString(),
                Email = row["Email"].ToString(),
                Role = row["Role"].ToString(),
                Phone = row["Phone"] == DBNull.Value ? null : row["Phone"].ToString()
            };

            var vm = BuildManageUsersViewModel();
            ViewBag.RoleOptions = vm.RoleOptions;
            return View(model);
        }

        private ActionResult RedisplayUsersWithEditModalOpen(EditUserViewModel model)
        {
            var vm = BuildManageUsersViewModel();
            ViewBag.RoleOptions = vm.RoleOptions;
            ViewBag.OpenModalId = "editModal_" + model.UserID;
            ViewData["EditFormModel_" + model.UserID] = model;
            return View("Users", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(EditUserViewModel model)
        {
            var guard = Guard();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return RedisplayUsersWithEditModalOpen(model);

            if (Array.IndexOf(Roles, model.Role) < 0)
            {
                TempData["Error"] = "Select a valid role.";
                return RedisplayUsersWithEditModalOpen(model);
            }

            // Email is UNIQUE in the schema; check first for a friendly message, excluding this user's own row.
            object existing = DbHelper.ExecuteScalar(
                "SELECT UserID FROM Users WHERE Email = @Email AND UserID <> @UserID",
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@UserID", model.UserID));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("Email", "A user with that email already exists.");
                return RedisplayUsersWithEditModalOpen(model);
            }

            DbHelper.Execute(
                @"UPDATE Users
                  SET FullName = @FullName, Email = @Email, Role = @Role, Phone = @Phone
                  WHERE UserID = @UserID",
                new SqlParameter("@FullName", model.FullName),
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@Role", model.Role),
                new SqlParameter("@Phone", (object)model.Phone ?? DBNull.Value),
                new SqlParameter("@UserID", model.UserID));

            TempData["Success"] = string.Format("User '{0}' updated.", model.FullName);
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserActive(int userId)
        {
            var guard = Guard();
            if (guard != null) return guard;

            // Don't let an admin lock themselves out of their own account.
            if (userId == (int)Session["UserID"])
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction("Users");
            }

            int rows = DbHelper.Execute(
                "UPDATE Users SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE UserID = @UserID",
                new SqlParameter("@UserID", userId));

            if (rows > 0)
                TempData["Success"] = "User status updated.";
            else
                TempData["Error"] = "User not found.";

            return RedirectToAction("Users");
        }

        // ---------- Manage Bookings ----------

        public ActionResult Bookings()
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BookingID, p.ProjectName, bl.BuildingName, u.FlatNumber, u.UnitID, u.Status AS UnitStatus,
                         cu.FullName AS ClientName, b.BookingDate, b.TotalPrice, b.Status, b.CancelReason
                  FROM UnitBookings b
                  JOIN Units u ON u.UnitID = b.UnitID
                  JOIN Buildings bl ON bl.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  JOIN Users cu ON cu.UserID = b.ClientUserID
                  ORDER BY b.BookingID DESC");

            var bookings = new List<BookingAdminModel>();
            foreach (DataRow row in table.Rows)
            {
                bookings.Add(new BookingAdminModel
                {
                    BookingID = (int)row["BookingID"],
                    ProjectName = row["ProjectName"].ToString(),
                    BuildingName = row["BuildingName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    UnitID = (int)row["UnitID"],
                    UnitStatus = row["UnitStatus"].ToString(),
                    ClientName = row["ClientName"].ToString(),
                    BookingDate = (DateTime)row["BookingDate"],
                    TotalPrice = (decimal)row["TotalPrice"],
                    Status = row["Status"].ToString(),
                    CancelReason = row["CancelReason"] == DBNull.Value ? null : row["CancelReason"].ToString()
                });
            }

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int bookingId, string reason)
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataRow booking = DbHelper.QuerySingleRow(
                "SELECT UnitID FROM UnitBookings WHERE BookingID = @BookingID AND Status = 'Active'",
                new SqlParameter("@BookingID", bookingId));

            if (booking == null)
            {
                TempData["Error"] = "Booking not found or already cancelled.";
                return RedirectToAction("Bookings");
            }

            int unitId = (int)booking["UnitID"];

            DbHelper.Execute(
                "UPDATE UnitBookings SET Status = 'Cancelled', CancelReason = @Reason WHERE BookingID = @BookingID",
                new SqlParameter("@Reason", (object)reason ?? DBNull.Value),
                new SqlParameter("@BookingID", bookingId));

            // Only put the Unit back on the market if it hadn't already been marked Sold --
            // cancelling a booking after it's fully paid would be an unusual manual
            // correction, not something this action should silently undo.
            DbHelper.Execute(
                "UPDATE Units SET Status = 'Available' WHERE UnitID = @UnitID AND Status = 'Booked'",
                new SqlParameter("@UnitID", unitId));

            TempData["Success"] = "Booking cancelled and the unit is available again.";
            return RedirectToAction("Bookings");
        }

        // ---------- Products ----------

        // Master Category list for the Add Product dropdown, ordered by CategoryName.
        private List<SelectListItem> GetCategoryOptions()
        {
            DataTable table = DbHelper.QueryTable(
                "SELECT CategoryID, CategoryName FROM MasterCategories ORDER BY CategoryName");

            var options = new List<SelectListItem>();
            foreach (DataRow row in table.Rows)
            {
                options.Add(new SelectListItem
                {
                    Value = row["CategoryID"].ToString(),
                    Text = row["CategoryName"].ToString()
                });
            }
            return options;
        }

        // Add Product is a modal on the Customization Catalog page now, so a direct GET of the
        // old URL (bookmark/back button) just lands on the catalog.
        [HttpGet]
        public ActionResult AddProduct()
        {
            var guard = Guard();
            if (guard != null) return guard;

            return RedirectToAction("CustomizationCatalog");
        }

        // Inserts one Products row per variant in the submission -- Category
        // (and the ProductName derived from the category) are shared across all of them, and
        // Description/Price come from that variant. This is the Admin's pure catalog: which
        // Project/Building offers which product is decided afterwards by the Project Manager
        // (MaterialAssignmentController / ProductScopeAssignments), not here.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddProduct(AddProductViewModel model)
        {
            var guard = Guard();
            if (guard != null) return guard;

            if (model.Variants == null || model.Variants.Count == 0)
                ModelState.AddModelError("Variants", "Add at least one variant.");

            // ExtraCost is only meaningful (and only required to be > 0) when the variant's
            // "Has Extra Cost?" box is checked -- an unchecked variant never needs one.
            if (model.Variants != null)
            {
                for (int i = 0; i < model.Variants.Count; i++)
                {
                    // The page clears (and hides) ExtraCost when "Has Extra Cost?" is unchecked, so it
                    // posts a blank -- which the binder rejects for a non-nullable decimal with an
                    // error on a hidden field, silently redisplaying the form. The value is ignored
                    // for an unchecked variant, so drop that binding error.
                    if (!model.Variants[i].HasExtraCost)
                        ModelState.Remove("Variants[" + i + "].ExtraCost");

                    if (model.Variants[i].HasExtraCost && model.Variants[i].ExtraCost <= 0)
                        ModelState.AddModelError("Variants[" + i + "].ExtraCost", "Enter an extra cost greater than zero.");
                }
            }

            if (!ModelState.IsValid)
                return RenderCatalog(model);

            // ProductName is still populated from the chosen category's name so every
            // existing read of Products.ProductName elsewhere keeps working unchanged.
            object categoryNameResult = DbHelper.ExecuteScalar(
                "SELECT CategoryName FROM MasterCategories WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", model.CategoryID));

            if (categoryNameResult == null || categoryNameResult == DBNull.Value)
            {
                ModelState.AddModelError("CategoryID", "Select a valid category.");
                return RenderCatalog(model);
            }

            string categoryName = categoryNameResult.ToString();

            // Starting Category/IsApprovedForClientCustomization follows the chosen
            // MasterCategory's IsCustomizable flag, so a variant under a customizable
            // category (e.g. Bathroom Fittings) is a valid client option once a Project
            // Manager assigns it, without Admin having to manually approve each one.
            // Admin can still flip either one afterward; this only sets the starting state.
            object isCategoryCustomizableResult = DbHelper.ExecuteScalar(
                "SELECT IsCustomizable FROM MasterCategories WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", model.CategoryID));
            bool categoryIsCustomizable = isCategoryCustomizableResult != null &&
                isCategoryCustomizableResult != DBNull.Value && (bool)isCategoryCustomizableResult;

            string initialCategoryText = categoryIsCustomizable ? "Customizable" : "Non-customizable";
            bool initialApproval = categoryIsCustomizable;

            foreach (var variant in model.Variants)
            {
                // IsDefaultOption/ExtraCost come straight from this variant's "Has Extra Cost?"
                // checkbox -- unchecked means included/default at no extra charge (0/0); checked
                // means a premium choice whose charge is the entered ExtraCost. Price mirrors
                // ExtraCost since the form no longer collects a separate base price per variant.
                bool isDefaultOption = !variant.HasExtraCost;
                decimal extraCost = variant.HasExtraCost ? variant.ExtraCost : 0m;
                decimal price = extraCost;

                DbHelper.Execute(
                    @"INSERT INTO Products (ProductName, Description, Price, Category, CategoryID, IsDefaultOption, ExtraCost, IsApprovedForClientCustomization)
                      VALUES (@ProductName, @Description, @Price, @Category, @CategoryID, @IsDefaultOption, @ExtraCost, @IsApproved)",
                    new SqlParameter("@ProductName", categoryName),
                    new SqlParameter("@Description", (object)variant.Description ?? DBNull.Value),
                    new SqlParameter("@Price", price),
                    new SqlParameter("@Category", initialCategoryText),
                    new SqlParameter("@CategoryID", model.CategoryID),
                    new SqlParameter("@IsDefaultOption", isDefaultOption),
                    new SqlParameter("@ExtraCost", extraCost),
                    new SqlParameter("@IsApproved", initialApproval));
            }

            TempData["Success"] = "Product added.";
            return RedirectToAction("CustomizationCatalog");
        }

        // ---------- Customization Catalog ----------

        // Every product, regardless of Category, so Admin can pick any of them to become
        // a client-facing customization option.
        public ActionResult CustomizationCatalog()
        {
            var guard = Guard();
            if (guard != null) return guard;

            return RenderCatalog(null);
        }

        // Shared by the catalog GET and by the AddProduct POST when redisplaying the page with
        // the Add Product modal forced open after a validation failure (addProductModel != null).
        private ActionResult RenderCatalog(AddProductViewModel addProductModel)
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT p.ProductID, p.ProductName, p.Description,
                         p.Category, p.IsApprovedForClientCustomization, mc.CategoryName, p.ExtraCost
                  FROM Products p
                  LEFT JOIN MasterCategories mc ON mc.CategoryID = p.CategoryID
                  ORDER BY p.IsApprovedForClientCustomization DESC, p.ProductID DESC");

            var rows = new List<CustomizationCatalogRow>();
            foreach (DataRow row in table.Rows)
            {
                rows.Add(new CustomizationCatalogRow
                {
                    ProductID = (int)row["ProductID"],
                    ProductName = row["ProductName"].ToString(),
                    Description = row["Description"] == DBNull.Value ? null : row["Description"].ToString(),
                    Category = row["Category"].ToString(),
                    CategoryName = row["CategoryName"] == DBNull.Value ? null : row["CategoryName"].ToString(),
                    IsApprovedForClientCustomization = (bool)row["IsApprovedForClientCustomization"],
                    ExtraCost = (decimal)row["ExtraCost"]
                });
            }

            DataTable categoryTable = DbHelper.QueryTable(
                "SELECT CategoryName FROM MasterCategories ORDER BY CategoryName");

            var categoryOptions = new List<string>();
            foreach (DataRow row in categoryTable.Rows)
                categoryOptions.Add(row["CategoryName"].ToString());

            ViewBag.CategoryOptions = categoryOptions;

            if (addProductModel == null)
            {
                addProductModel = new AddProductViewModel();
            }
            else
            {
                ViewBag.OpenModalId = "addProductModal";
                if (addProductModel.Variants == null || addProductModel.Variants.Count == 0)
                    addProductModel.Variants = new List<VariantInput> { new VariantInput() };
            }
            addProductModel.CategoryOptions = GetCategoryOptions();

            ViewData["AddProductModel"] = addProductModel;

            return View("CustomizationCatalog", rows);
        }

        // Admin-only decision on whether a variant costs extra on top of the base price --
        // IsDefaultOption is derived from ExtraCost so the two fields can never disagree.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateExtraCost(int productId, decimal extraCost)
        {
            var guard = Guard();
            if (guard != null) return guard;

            if (extraCost < 0)
                extraCost = 0;

            int rows = DbHelper.Execute(
                @"UPDATE Products
                  SET ExtraCost = @ExtraCost,
                      IsDefaultOption = CASE WHEN @ExtraCost = 0 THEN 1 ELSE 0 END
                  WHERE ProductID = @ProductID",
                new SqlParameter("@ExtraCost", extraCost),
                new SqlParameter("@ProductID", productId));

            if (rows > 0)
                TempData["Success"] = "Extra cost updated.";
            else
                TempData["Error"] = "Product not found.";

            return RedirectToAction("CustomizationCatalog");
        }

        // ---------- Product Categories ----------

        public ActionResult Categories()
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataTable table = DbHelper.QueryTable(
                "SELECT CategoryID, CategoryName, IsCustomizable FROM MasterCategories ORDER BY CategoryID DESC");

            var categories = new List<MasterCategoryModel>();
            foreach (DataRow row in table.Rows)
            {
                categories.Add(new MasterCategoryModel
                {
                    CategoryID = (int)row["CategoryID"],
                    CategoryName = row["CategoryName"].ToString(),
                    IsCustomizable = (bool)row["IsCustomizable"]
                });
            }

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCategory(string categoryName, bool isCustomizable)
        {
            var guard = Guard();
            if (guard != null) return guard;

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("Categories");
            }

            object exists = DbHelper.ExecuteScalar(
                "SELECT CategoryID FROM MasterCategories WHERE CategoryName = @CategoryName",
                new SqlParameter("@CategoryName", categoryName.Trim()));

            if (exists != null && exists != DBNull.Value)
            {
                TempData["Error"] = "A category named '" + categoryName.Trim() + "' already exists.";
                return RedirectToAction("Categories");
            }

            DbHelper.Execute(
                "INSERT INTO MasterCategories (CategoryName, IsCustomizable) VALUES (@CategoryName, @IsCustomizable)",
                new SqlParameter("@CategoryName", categoryName.Trim()),
                new SqlParameter("@IsCustomizable", isCustomizable));

            TempData["Success"] = string.Format("Category '{0}' created.", categoryName.Trim());
            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCategory(int categoryId)
        {
            var guard = Guard();
            if (guard != null) return guard;

            DataRow catRow = DbHelper.QuerySingleRow(
                "SELECT CategoryName FROM MasterCategories WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", categoryId));

            if (catRow == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToAction("Categories");
            }

            string categoryName = catRow["CategoryName"].ToString();

            int productCount = (int)DbHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Products WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", categoryId));

            if (productCount > 0)
            {
                TempData["Error"] = string.Format("Cannot delete category '{0}' because {1} product(s) in the catalog belong to it.", categoryName, productCount);
                return RedirectToAction("Categories");
            }

            int rows = DbHelper.Execute(
                "DELETE FROM MasterCategories WHERE CategoryID = @CategoryID",
                new SqlParameter("@CategoryID", categoryId));

            if (rows > 0)
                TempData["Success"] = string.Format("Category '{0}' deleted successfully.", categoryName);
            else
                TempData["Error"] = "Category could not be deleted.";

            return RedirectToAction("Categories");
        }

        // ---------- Company-wide Reports ----------

        public ActionResult Reports()
        {
            var guard = Guard();
            if (guard != null) return guard;

            var viewModel = new AdminReportsViewModel();

            // All-projects rollup of client payments received.
            DataTable table = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName,
                         ISNULL((SELECT SUM(cp.Amount) FROM ClientPayments cp WHERE cp.ProjectID = p.ProjectID), 0) AS TotalClientPayments
                  FROM Projects p
                  ORDER BY p.ProjectName");

            foreach (DataRow row in table.Rows)
            {
                viewModel.ProjectSummaries.Add(new CompanyReportRow
                {
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    TotalClientPayments = (decimal)row["TotalClientPayments"]
                });
            }

            // Recent Client Payments & Transactions
            DataTable txTable = DbHelper.QueryTable(
                @"SELECT TOP 50 cp.ClientPaymentID, cp.PaymentDate, cp.Amount,
                         cu.FullName AS ClientName, p.ProjectName, u.FlatNumber,
                         rec.FullName AS RecordedByName
                  FROM ClientPayments cp
                  JOIN UnitBookings b ON b.BookingID = cp.BookingID
                  JOIN Users cu ON cu.UserID = b.ClientUserID
                  JOIN Projects p ON p.ProjectID = cp.ProjectID
                  JOIN Units u ON u.UnitID = b.UnitID
                  LEFT JOIN Users rec ON rec.UserID = cp.RecordedByUserID
                  ORDER BY cp.ClientPaymentID DESC");

            foreach (DataRow row in txTable.Rows)
            {
                viewModel.RecentTransactions.Add(new RecentTransactionModel
                {
                    PaymentID = (int)row["ClientPaymentID"],
                    PaymentDate = (DateTime)row["PaymentDate"],
                    Amount = (decimal)row["Amount"],
                    ClientName = row["ClientName"].ToString(),
                    ProjectName = row["ProjectName"].ToString(),
                    FlatNumber = row["FlatNumber"].ToString(),
                    RecordedByName = row["RecordedByName"] == DBNull.Value ? "System" : row["RecordedByName"].ToString()
                });
            }

            return View(viewModel);
        }

        // ---------- Assigned Projects ----------

        public ActionResult Assigned()
        {
            var guard = Guard();
            if (guard != null) return guard;

            var model = new AssignedProjectsViewModel();

            // Populate PM dropdown options
            DataTable pmTable = DbHelper.QueryTable(
                "SELECT UserID, FullName FROM Users WHERE Role = 'Project Manager' AND IsActive = 1 ORDER BY FullName");
            model.ProjectManagerOptions.Add(new SelectListItem { Value = "", Text = "-- Select Project Manager --" });
            foreach (DataRow r in pmTable.Rows)
            {
                model.ProjectManagerOptions.Add(new SelectListItem
                {
                    Value = r["UserID"].ToString(),
                    Text = r["FullName"].ToString()
                });
            }

            // Populate Accounts Officer dropdown options
            DataTable aoTable = DbHelper.QueryTable(
                "SELECT UserID, FullName FROM Users WHERE Role = 'Accounts Officer' AND IsActive = 1 ORDER BY FullName");
            model.AccountOfficerOptions.Add(new SelectListItem { Value = "", Text = "-- Select Accounts Officer --" });
            foreach (DataRow r in aoTable.Rows)
            {
                model.AccountOfficerOptions.Add(new SelectListItem
                {
                    Value = r["UserID"].ToString(),
                    Text = r["FullName"].ToString()
                });
            }

            // Populate Projects list with assignments
            DataTable pTable = DbHelper.QueryTable(
                @"SELECT p.ProjectID, p.ProjectName, p.Location, p.Budget, p.Status,
                         p.ProjectManagerID, pm.FullName AS ProjectManagerName,
                         p.AccountOfficerID, ao.FullName AS AccountOfficerName
                  FROM Projects p
                  LEFT JOIN Users pm ON pm.UserID = p.ProjectManagerID
                  LEFT JOIN Users ao ON ao.UserID = p.AccountOfficerID
                  ORDER BY p.ProjectID DESC");

            foreach (DataRow row in pTable.Rows)
            {
                model.Projects.Add(new ProjectAssignmentRow
                {
                    ProjectID = (int)row["ProjectID"],
                    ProjectName = row["ProjectName"].ToString(),
                    Location = row["Location"] == DBNull.Value ? null : row["Location"].ToString(),
                    Budget = (decimal)row["Budget"],
                    Status = row["Status"].ToString(),
                    ProjectManagerID = row["ProjectManagerID"] == DBNull.Value ? (int?)null : (int)row["ProjectManagerID"],
                    ProjectManagerName = row["ProjectManagerName"] == DBNull.Value ? null : row["ProjectManagerName"].ToString(),
                    AccountOfficerID = row["AccountOfficerID"] == DBNull.Value ? (int?)null : (int)row["AccountOfficerID"],
                    AccountOfficerName = row["AccountOfficerName"] == DBNull.Value ? null : row["AccountOfficerName"].ToString()
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignProject(int projectId, int? projectManagerId, int? accountOfficerId)
        {
            var guard = Guard();
            if (guard != null) return guard;

            DbHelper.Execute(
                @"UPDATE Projects
                  SET ProjectManagerID = @ProjectManagerID,
                      AccountOfficerID = @AccountOfficerID
                  WHERE ProjectID = @ProjectID",
                new SqlParameter("@ProjectManagerID", (object)projectManagerId ?? DBNull.Value),
                new SqlParameter("@AccountOfficerID", (object)accountOfficerId ?? DBNull.Value),
                new SqlParameter("@ProjectID", projectId));

            TempData["Success"] = "Project assignments updated successfully.";
            return RedirectToAction("Assigned");
        }
    }
}
