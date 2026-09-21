using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;

namespace Final.Controllers
{
    public class UnitController : Controller
    {
        private bool IsAuthorized(string requiredRole)
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == requiredRole;
        }

        // Buildings for the create dropdown, labeled "Project - Building" since a Unit is created under a Building.
        private List<SelectListItem> GetBuildingOptions()
        {
            DataTable table = DbHelper.QueryTable(
                @"SELECT b.BuildingID, p.ProjectName, b.BuildingName
                  FROM Buildings b
                  JOIN Projects p ON p.ProjectID = b.ProjectID
                  ORDER BY b.BuildingID DESC");

            var options = new List<SelectListItem>();
            foreach (DataRow row in table.Rows)
            {
                options.Add(new SelectListItem
                {
                    Value = row["BuildingID"].ToString(),
                    Text = row["ProjectName"] + " - " + row["BuildingName"]
                });
            }
            return options;
        }

        private List<SelectListItem> GetFacingOptions()
        {
            var directions = new[] { "North", "South", "East", "West", "North-East", "North-West", "South-East", "South-West" };
            var options = new List<SelectListItem>();
            foreach (string d in directions)
                options.Add(new SelectListItem { Value = d, Text = d });
            return options;
        }

        public ActionResult Index(string search)
        {
            if (Session["UserRole"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.BuildingOptions = GetBuildingOptions();
            ViewBag.FacingOptions = GetFacingOptions();
            ViewBag.SearchTerm = search;
            return View(BuildUnitList(search));
        }

        // AJAX source for the Generate Units page: once Admin picks a Building, this returns
        // its saved TotalFloors/UnitsPerFloor/HasParkingFacility so the page can render exactly
        // UnitsPerFloor position templates without those values ever being re-entered by hand.
        [HttpGet]
        public JsonResult BuildingInfo(int buildingId)
        {
            if (Session["UserRole"] == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            DataRow row = DbHelper.QuerySingleRow(
                @"SELECT b.BuildingName, p.ProjectName, p.TotalAreaSqFt, b.TotalFloors, b.UnitsPerFloor, b.HasParkingFacility
                  FROM Buildings b
                  JOIN Projects p ON p.ProjectID = b.ProjectID
                  WHERE b.BuildingID = @BuildingID",
                new SqlParameter("@BuildingID", buildingId));

            if (row == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            var result = new
            {
                buildingName = row["ProjectName"] + " - " + row["BuildingName"],
                totalFloors = row["TotalFloors"] == DBNull.Value ? (int?)null : (int)row["TotalFloors"],
                unitsPerFloor = row["UnitsPerFloor"] == DBNull.Value ? (int?)null : (int)row["UnitsPerFloor"],
                hasParkingFacility = (bool)row["HasParkingFacility"],
                totalAreaSqFt = row["TotalAreaSqFt"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["TotalAreaSqFt"])
            };
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // Shared by Index and by the Create/Edit POST actions when redisplaying the list page
        // with a modal forced open after a validation failure (those call sites never pass a
        // search term, so they always see the full unfiltered list).
        // Search matches FlatNumber, BuildingName, or ProjectName/letter -- an OR'd LIKE against
        // all three, so the one search box on the Units page covers everything the grouped
        // Project > Building view can be searched by.
        private List<UnitModel> BuildUnitList(string search = null)
        {
            string whereClause = "";
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                whereClause = @"WHERE u.FlatNumber LIKE @Search
                                   OR b.BuildingName LIKE @Search
                                   OR p.ProjectName LIKE @Search";
                parameters.Add(new SqlParameter("@Search", "%" + search.Trim() + "%"));
            }

            DataTable table = DbHelper.QueryTable(
                $@"SELECT u.UnitID, u.BuildingID, b.BuildingName, u.ProjectID, p.ProjectName,
                         u.FlatNumber, u.FloorNumber, u.SizeSqFt, u.BedroomCount, u.BathroomCount, u.AttachedBathrooms,
                         u.HasKitchen, u.HasHall, u.BalconyCount, u.Facing, u.BasePrice, u.Status, p.EstimatedCompletionDate
                  FROM Units u
                  JOIN Buildings b ON b.BuildingID = u.BuildingID
                  JOIN Projects p ON p.ProjectID = u.ProjectID
                  {whereClause}
                  ORDER BY u.UnitID DESC",
                parameters.ToArray());

            var units = new List<UnitModel>();
            foreach (DataRow row in table.Rows)
                units.Add(MapUnit(row));
            return units;
        }

        // Primary path for populating a building: one position (A, B, C...) per UnitsPerFloor,
        // each with its own spec, applied to every floor. GET just shows the Building picker --
        // the position templates are rendered client-side once BuildingInfo's AJAX response
        // reports the building's saved UnitsPerFloor.
        [HttpGet]
        public ActionResult GenerateUnits()
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.BuildingOptions = GetBuildingOptions();
            ViewBag.FacingOptions = GetFacingOptions();
            ViewBag.OpenModalId = "generateUnitsModal";
            return View("Index", BuildUnitList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateUnits(GenerateUnitsViewModel model)
        {
            if (!IsAuthorized("Admin"))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Access denied." });
                return RedirectToAction("AccessDenied", "Account");
            }

            // TotalFloors/UnitsPerFloor are read-only display values sourced from the Building
            // record -- always re-read from the DB here and never trust whatever the client
            // posted for them, so a manipulated request can't generate a different floor/position
            // count than what the building actually has saved.
            DataRow building = DbHelper.QuerySingleRow(
                @"SELECT b.ProjectID, b.TotalFloors, b.UnitsPerFloor, p.TotalAreaSqFt
                  FROM Buildings b
                  JOIN Projects p ON p.ProjectID = b.ProjectID
                  WHERE b.BuildingID = @BuildingID",
                new SqlParameter("@BuildingID", model.BuildingID));

            var activePositions = new List<Tuple<int, UnitPositionTemplate>>();

            if (building == null)
            {
                ModelState.AddModelError("BuildingID", "Select a valid building.");
            }
            else if (building["UnitsPerFloor"] == DBNull.Value)
            {
                ModelState.AddModelError("BuildingID", "Set 'Units Per Floor' on this building (via Edit Building) before generating units.");
            }
            else
            {
                int expectedPositions = (int)building["UnitsPerFloor"];
                if (model.Positions == null || model.Positions.Count != expectedPositions)
                {
                    ModelState.AddModelError("BuildingID", "Position template count doesn't match this building's Units Per Floor. Reload the page and try again.");
                }
                else
                {
                    for (int i = 0; i < model.Positions.Count; i++)
                    {
                        var pos = model.Positions[i];
                        bool isActive = pos != null && (
                            pos.SizeSqFt.HasValue ||
                            pos.BasePrice.HasValue ||
                            pos.BedroomCount.HasValue ||
                            pos.BathroomCount.HasValue ||
                            !string.IsNullOrWhiteSpace(pos.Facing)
                        );

                        if (isActive)
                        {
                            activePositions.Add(Tuple.Create(i, pos));
                        }
                        else
                        {
                            string target = string.Format("Positions[{0}]", i);
                            var keysToRemove = ModelState.Keys.Where(k => k.Contains(target)).ToList();
                            foreach (var key in keysToRemove)
                            {
                                ModelState.Remove(key);
                            }
                        }
                    }

                    if (activePositions.Count == 0)
                    {
                        ModelState.AddModelError("BuildingID", "Please fill in specifications for at least 1 unit position.");
                    }
                    else if (building["TotalAreaSqFt"] != DBNull.Value)
                    {
                        decimal projectTotalArea = Convert.ToDecimal(building["TotalAreaSqFt"]);
                        foreach (var item in activePositions)
                        {
                            int i = item.Item1;
                            var pos = item.Item2;
                            string posLabel = ((char)('A' + i)).ToString();
                            if (pos.SizeSqFt.HasValue && pos.SizeSqFt.Value >= projectTotalArea)
                            {
                                ModelState.AddModelError(string.Format("Positions[{0}].SizeSqFt", i),
                                    string.Format("Position {0} unit size ({1:N0} SqFt) is larger than or equal to the project total area ({2:N0} SqFt). Unit size must be less than the project size.", posLabel, pos.SizeSqFt.Value, projectTotalArea));
                            }
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                if (Request.IsAjaxRequest())
                {
                    var errors = new List<string>();
                    foreach (var kvp in ModelState)
                    {
                        if (kvp.Value.Errors.Count > 0)
                        {
                            foreach (var error in kvp.Value.Errors)
                            {
                                string msg = error.ErrorMessage;
                                if (string.IsNullOrWhiteSpace(msg) && error.Exception != null)
                                    msg = error.Exception.Message;

                                if (string.IsNullOrWhiteSpace(msg))
                                {
                                    string fieldName = kvp.Key;
                                    if (fieldName.Contains("Positions["))
                                    {
                                        int startIdx = fieldName.IndexOf("Positions[");
                                        int endIdx = fieldName.IndexOf(']', startIdx);
                                        string indexStr = fieldName.Substring(startIdx + 10, endIdx - (startIdx + 10));
                                        int posIdx;
                                        string posLabel = int.TryParse(indexStr, out posIdx) ? ((char)('A' + posIdx)).ToString() : indexStr;
                                        string propName = fieldName.Substring(endIdx + 2);

                                        if (propName == "SizeSqFt") propName = "Size (SqFt)";
                                        else if (propName == "BasePrice") propName = "Base Price";
                                        else if (propName == "BedroomCount") propName = "Bedrooms";
                                        else if (propName == "BathroomCount") propName = "Bathrooms";
                                        else if (propName == "Facing") propName = "Facing Direction";

                                        msg = string.Format("Position {0}: {1} is required.", posLabel, propName);
                                    }
                                    else
                                    {
                                        msg = string.Format("{0} is required.", fieldName);
                                    }
                                }

                                if (!errors.Contains(msg))
                                    errors.Add(msg);
                            }
                        }
                    }
                    string errorResponse = errors.Count > 0
                        ? string.Join("<br/>", errors)
                        : "Please fill in all required fields (Size, Price, Bedrooms, Bathrooms, Facing).";
                    return Json(new { success = false, message = errorResponse });
                }

                ViewBag.BuildingOptions = GetBuildingOptions();
                ViewBag.FacingOptions = GetFacingOptions();
                ViewBag.OpenModalId = "generateUnitsModal";
                ViewData["GenerateFormModel"] = model;
                return View("Index", BuildUnitList());
            }

            int projectId = (int)building["ProjectID"];
            int totalFloors = (int)building["TotalFloors"];

            int created = 0;
            int skipped = 0;

            try
            {
                for (int floor = 1; floor <= totalFloors; floor++)
                {
                    foreach (var item in activePositions)
                    {
                        int posIndex = item.Item1;
                        var template = item.Item2;
                        string positionLetter = ((char)('A' + posIndex)).ToString();
                        string flatNumber = floor + positionLetter;

                        // Respect UNIQUE(BuildingID, FlatNumber) -- skip a flat that already exists
                        // (e.g. a one-off unit added earlier, or generation being re-run) instead of
                        // failing the whole batch.
                        object existing = DbHelper.ExecuteScalar(
                            "SELECT UnitID FROM Units WHERE BuildingID = @BuildingID AND FlatNumber = @FlatNumber",
                            new SqlParameter("@BuildingID", model.BuildingID),
                            new SqlParameter("@FlatNumber", flatNumber));

                        if (existing != null && existing != DBNull.Value)
                        {
                            skipped++;
                            continue;
                        }

                        DbHelper.Execute(
                            @"INSERT INTO Units (BuildingID, ProjectID, FlatNumber, FloorNumber, SizeSqFt,
                                                  BedroomCount, BathroomCount, AttachedBathrooms, HasKitchen, HasHall, BalconyCount, Facing, BasePrice, Status)
                              VALUES (@BuildingID, @ProjectID, @FlatNumber, @FloorNumber, @SizeSqFt,
                                      @BedroomCount, @BathroomCount, @AttachedBathrooms, @HasKitchen, @HasHall, @BalconyCount, @Facing, @BasePrice, 'Available')",
                            new SqlParameter("@BuildingID", model.BuildingID),
                            new SqlParameter("@ProjectID", projectId),
                            new SqlParameter("@FlatNumber", flatNumber),
                            new SqlParameter("@FloorNumber", floor),
                            new SqlParameter("@SizeSqFt", template.SizeSqFt.Value),
                            new SqlParameter("@BedroomCount", template.BedroomCount.Value),
                            new SqlParameter("@BathroomCount", template.BathroomCount.Value),
                            new SqlParameter("@AttachedBathrooms", (object)template.AttachedBathrooms ?? DBNull.Value),
                            new SqlParameter("@HasKitchen", true),
                            new SqlParameter("@HasHall", template.HasHall),
                            new SqlParameter("@BalconyCount", (object)template.BalconyCount ?? DBNull.Value),
                            new SqlParameter("@Facing", (object)template.Facing ?? DBNull.Value),
                            new SqlParameter("@BasePrice", template.BasePrice.Value));

                        created++;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Database Error: " + ex.Message });
                }
                TempData["Error"] = "Error generating units: " + ex.Message;
                return RedirectToAction("Index");
            }

            string successMsg = skipped > 0
                ? string.Format("Generated {0} new units ({1} already existed and were skipped).", created, skipped)
                : string.Format("Generated {0} new units.", created);

            if (Request.IsAjaxRequest())
            {
                TempData["Success"] = successMsg;
                return Json(new { success = true, redirectUrl = Url.Action("Index", "Unit") });
            }

            TempData["Success"] = successMsg;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            DataRow row = DbHelper.QuerySingleRow(
                @"SELECT UnitID, BuildingID, FlatNumber, FloorNumber, SizeSqFt,
                         BedroomCount, BathroomCount, AttachedBathrooms, HasKitchen, HasHall, BalconyCount, Facing, BasePrice, Status
                  FROM Units WHERE UnitID = @UnitID",
                new SqlParameter("@UnitID", id));

            if (row == null)
                return HttpNotFound();

            // Only an Available unit's details can be edited -- once booked/sold, its record
            // is tied to a live booking/payment, so a direct URL visit is bounced back with
            // an error instead of ever rendering the form.
            if (row["Status"].ToString() != "Available")
            {
                TempData["Error"] = "Booked or Sold units cannot be edited.";
                return RedirectToAction("Index");
            }

            var model = new UnitModel
            {
                UnitID = (int)row["UnitID"],
                BuildingID = (int)row["BuildingID"],
                FlatNumber = row["FlatNumber"].ToString(),
                FloorNumber = (int)row["FloorNumber"],
                SizeSqFt = (decimal)row["SizeSqFt"],
                BedroomCount = (int)row["BedroomCount"],
                BathroomCount = (int)row["BathroomCount"],
                AttachedBathrooms = row["AttachedBathrooms"] == DBNull.Value ? (int?)null : (int)row["AttachedBathrooms"],
                HasKitchen = (bool)row["HasKitchen"],
                HasHall = (bool)row["HasHall"],
                BalconyCount = row["BalconyCount"] == DBNull.Value ? (int?)null : (int)row["BalconyCount"],
                Facing = row["Facing"] == DBNull.Value ? null : row["Facing"].ToString(),
                BasePrice = (decimal)row["BasePrice"],
                Status = row["Status"].ToString()
            };

            ViewBag.BuildingOptions = GetBuildingOptions();
            ViewBag.FacingOptions = GetFacingOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(UnitModel model)
        {
            if (!IsAuthorized("Admin"))
                return RedirectToAction("AccessDenied", "Account");

            // Status changes only through booking/payment logic, never through this form --
            // always read the current value from the DB and ignore whatever (if anything)
            // was posted for it, so a manipulated request can't force a status change.
            object currentStatusRaw = DbHelper.ExecuteScalar(
                "SELECT Status FROM Units WHERE UnitID = @UnitID",
                new SqlParameter("@UnitID", model.UnitID));

            if (currentStatusRaw == null || currentStatusRaw == DBNull.Value)
                return HttpNotFound();

            model.Status = currentStatusRaw.ToString();
            ModelState.Remove("Status");

            // Same rule as the GET: a Booked/Sold unit can't be edited, even via a
            // hand-crafted POST straight to this action bypassing the (hidden) Edit link.
            if (model.Status != "Available")
            {
                TempData["Error"] = "Booked or Sold units cannot be edited.";
                return RedirectToAction("Index");
            }

            // Resolve the Building's Project so ProjectID can never be spoofed independently of BuildingID.
            DataRow building = DbHelper.QuerySingleRow(
                @"SELECT b.ProjectID, p.TotalAreaSqFt
                  FROM Buildings b
                  JOIN Projects p ON p.ProjectID = b.ProjectID
                  WHERE b.BuildingID = @BuildingID",
                new SqlParameter("@BuildingID", model.BuildingID));

            if (building == null)
            {
                ModelState.AddModelError("BuildingID", "Select a valid building.");
            }
            else if (building["TotalAreaSqFt"] != DBNull.Value)
            {
                decimal projectTotalArea = Convert.ToDecimal(building["TotalAreaSqFt"]);
                if (model.SizeSqFt >= projectTotalArea)
                {
                    ModelState.AddModelError("SizeSqFt",
                        $"Unit size ({model.SizeSqFt:N0} SqFt) is larger than or equal to the project total area ({projectTotalArea:N0} SqFt). Unit size must be less than the project size.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.BuildingOptions = GetBuildingOptions();
                ViewBag.FacingOptions = GetFacingOptions();
                ViewBag.OpenModalId = "editModal_" + model.UnitID;
                ViewData["EditFormModel_" + model.UnitID] = model;
                return View("Index", BuildUnitList());
            }

            model.ProjectID = (int)building["ProjectID"];

            // Respect UNIQUE(BuildingID, FlatNumber), excluding this row.
            object existing = DbHelper.ExecuteScalar(
                "SELECT UnitID FROM Units WHERE BuildingID = @BuildingID AND FlatNumber = @FlatNumber AND UnitID <> @UnitID",
                new SqlParameter("@BuildingID", model.BuildingID),
                new SqlParameter("@FlatNumber", model.FlatNumber),
                new SqlParameter("@UnitID", model.UnitID));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("FlatNumber", "That flat number already exists in the selected building.");
                ViewBag.BuildingOptions = GetBuildingOptions();
                ViewBag.FacingOptions = GetFacingOptions();
                ViewBag.OpenModalId = "editModal_" + model.UnitID;
                ViewData["EditFormModel_" + model.UnitID] = model;
                return View("Index", BuildUnitList());
            }

            DbHelper.Execute(
                @"UPDATE Units
                  SET BuildingID = @BuildingID, ProjectID = @ProjectID, FlatNumber = @FlatNumber, FloorNumber = @FloorNumber,
                      SizeSqFt = @SizeSqFt, BedroomCount = @BedroomCount, BathroomCount = @BathroomCount, AttachedBathrooms = @AttachedBathrooms,
                      HasKitchen = @HasKitchen, HasHall = @HasHall, BalconyCount = @BalconyCount,
                      Facing = @Facing, BasePrice = @BasePrice, Status = @Status
                  WHERE UnitID = @UnitID",
                new SqlParameter("@BuildingID", model.BuildingID),
                new SqlParameter("@ProjectID", model.ProjectID),
                new SqlParameter("@FlatNumber", model.FlatNumber),
                new SqlParameter("@FloorNumber", model.FloorNumber),
                new SqlParameter("@SizeSqFt", model.SizeSqFt),
                new SqlParameter("@BedroomCount", model.BedroomCount),
                new SqlParameter("@BathroomCount", model.BathroomCount),
                new SqlParameter("@AttachedBathrooms", (object)model.AttachedBathrooms ?? DBNull.Value),
                new SqlParameter("@HasKitchen", model.HasKitchen),
                new SqlParameter("@HasHall", model.HasHall),
                new SqlParameter("@BalconyCount", (object)model.BalconyCount ?? DBNull.Value),
                new SqlParameter("@Facing", (object)model.Facing ?? DBNull.Value),
                new SqlParameter("@BasePrice", model.BasePrice),
                new SqlParameter("@Status", model.Status),
                new SqlParameter("@UnitID", model.UnitID));

            return RedirectToAction("Index");
        }

        internal static UnitModel MapUnit(DataRow row)
        {
            return new UnitModel
            {
                UnitID = (int)row["UnitID"],
                BuildingID = (int)row["BuildingID"],
                BuildingName = row["BuildingName"].ToString(),
                ProjectID = (int)row["ProjectID"],
                ProjectName = row["ProjectName"].ToString(),
                FlatNumber = row["FlatNumber"].ToString(),
                FloorNumber = (int)row["FloorNumber"],
                SizeSqFt = (decimal)row["SizeSqFt"],
                BedroomCount = (int)row["BedroomCount"],
                BathroomCount = (int)row["BathroomCount"],
                AttachedBathrooms = row["AttachedBathrooms"] == DBNull.Value ? (int?)null : (int)row["AttachedBathrooms"],
                HasKitchen = (bool)row["HasKitchen"],
                HasHall = (bool)row["HasHall"],
                BalconyCount = row["BalconyCount"] == DBNull.Value ? (int?)null : (int)row["BalconyCount"],
                Facing = row["Facing"] == DBNull.Value ? null : row["Facing"].ToString(),
                BasePrice = (decimal)row["BasePrice"],
                Status = row["Status"].ToString(),
                EstimatedCompletionDate = row["EstimatedCompletionDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["EstimatedCompletionDate"],
                CustomizationReady = row.Table.Columns.Contains("CustomizationReady") && row["CustomizationReady"] != DBNull.Value && Convert.ToInt32(row["CustomizationReady"]) == 1
            };
        }
    }
}
