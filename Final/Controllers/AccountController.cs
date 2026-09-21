using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Final.Data;
using Final.Models;
using Final.Security;

namespace Final.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public ActionResult Login()
        {
            if (Session["UserRole"] != null)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            DataRow user = DbHelper.QuerySingleRow(   
                "SELECT UserID, FullName, Email, PasswordHash, Role, IsActive FROM Users WHERE Email = @Email",
                new SqlParameter("@Email", model.Email));

            if (user == null || !(bool)user["IsActive"] ||
                !PasswordHasher.Verify(model.Password, user["PasswordHash"].ToString()))
            {
                ModelState.AddModelError("Password", "Invalid email or password.");
                return View(model);
            }

            Session["UserID"] = (int)user["UserID"];
            Session["FullName"] = user["FullName"].ToString();
            Session["UserRole"] = user["Role"].ToString();

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public ActionResult Signup()
        {
            if (Session["UserRole"] != null)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        // Public self-signup. Always creates a Client account -- there is no Role field on
        // this form, and the INSERT below hardcodes 'Client' so a manipulated POST can never
        // create an Admin/Project Manager/Accounts Officer account through this endpoint.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Signup(SignupViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Respect UNIQUE(Email).
            object existing = DbHelper.ExecuteScalar(
                "SELECT UserID FROM Users WHERE Email = @Email",
                new SqlParameter("@Email", model.Email));

            if (existing != null && existing != DBNull.Value)
            {
                ModelState.AddModelError("Email", "An account with that email already exists.");
                return View(model);
            }

            string passwordHash = PasswordHasher.Hash(model.Password);

            object newUserId = DbHelper.ExecuteScalar(
                @"INSERT INTO Users (FullName, Email, PasswordHash, Role, Phone)
                  OUTPUT INSERTED.UserID
                  VALUES (@FullName, @Email, @PasswordHash, 'Client', @Phone)",
                new SqlParameter("@FullName", model.FullName),
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@Phone", (object)model.Phone ?? DBNull.Value));

            // Log the new account straight in, same session fields Login sets.
            Session["UserID"] = Convert.ToInt32(newUserId);
            Session["FullName"] = model.FullName;
            Session["UserRole"] = "Client";

            return RedirectToAction("Index", "Dashboard");
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        public ActionResult AccessDenied()
        {
            return View();
        }
    }
}
