using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Final.Data;
using Newtonsoft.Json.Linq;

namespace Final.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> Chat(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { error = "Please type a question first." });
            }

            try
            {
                string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return Json(new { error = "The chat assistant isn't configured yet. Please contact us directly." });
                }

                DataTable table = DbHelper.QueryTable(
                    "SELECT ProjectID, ProjectName, Location, Budget, StartDate, EstimatedCompletionDate, Status, MaxBuildings, TotalAreaSqFt FROM Projects ORDER BY ProjectName");
                // Include Status for ALL projects (not just Active) here, since visitors may ask
                // about completed or upcoming projects too -- the AI can explain the status itself.

                var projectLines = new StringBuilder();
                foreach (DataRow row in table.Rows)
                {
                    projectLines.AppendLine("Project: " + row["ProjectName"]);
                    foreach (DataColumn col in table.Columns)
                    {
                        if (col.ColumnName == "ProjectID" || col.ColumnName == "ProjectName") continue; // skip internal ID and the name already printed
                        object val = row[col.ColumnName];
                        string displayVal = val == DBNull.Value ? "not set" : val.ToString();
                        projectLines.AppendLine("  " + col.ColumnName + ": " + displayVal);
                    }
                    projectLines.AppendLine();
                }

                string systemContext = "Here is complete information about every project (active, " +
                    "completed, or upcoming) at Concord Real Estate Development. Use these details to answer " +
                    "any question a visitor asks about any project -- budget, dates, location, status, " +
                    "capacity, area, or anything else listed below:\n" + projectLines +
                    "\nIf a visitor asks something not covered by this data, let them know and suggest " +
                    "contacting us directly for more details.";

                var requestBody = new JObject(
                    new JProperty("contents", new JArray(
                        new JObject(
                            new JProperty("role", "user"),
                            new JProperty("parts", new JArray(
                                new JObject(new JProperty("text", systemContext + "\n\nVisitor question: " + message))
                            ))
                        )
                    ))
                );

                string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key=" + apiKey;

                using (var client = new HttpClient())
                using (var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return Json(new { error = "Sorry, the chat assistant is temporarily unavailable. Please try again later." });
                    }

                    JObject result = JObject.Parse(responseBody);
                    string reply = (string)result.SelectToken("candidates[0].content.parts[0].text");

                    if (string.IsNullOrWhiteSpace(reply))
                    {
                        return Json(new { error = "Sorry, I couldn't come up with an answer to that. Please try again or contact us directly." });
                    }

                    return Json(new { reply = reply.Trim() });
                }
            }
            catch (Exception)
            {
                return Json(new { error = "Sorry, something went wrong. Please try again later." });
            }
        }
    }
}