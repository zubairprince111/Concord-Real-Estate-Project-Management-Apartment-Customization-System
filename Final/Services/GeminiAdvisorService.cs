using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Final.Services
{
    // Wraps a single Gemini generateContent call for the Client Customize modal's AI
    // Advisor box -- same request/response shape as HomeController.Chat's landing-page
    // assistant (same model, same endpoint), just fed this booking's customizable
    // products as context instead of the public Projects table.
    public class GeminiAdvisorService
    {
        public async Task<string> AskAsync(string systemContext, string question)
        {
            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("The AI advisor isn't configured yet. Please contact us directly.");

            var requestBody = new JObject(
                new JProperty("contents", new JArray(
                    new JObject(
                        new JProperty("role", "user"),
                        new JProperty("parts", new JArray(
                            new JObject(new JProperty("text", systemContext + "\n\nClient question: " + question))
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
                    throw new InvalidOperationException("Sorry, the AI advisor is temporarily unavailable. Please try again later.");

                JObject result = JObject.Parse(responseBody);
                string reply = (string)result.SelectToken("candidates[0].content.parts[0].text");

                if (string.IsNullOrWhiteSpace(reply))
                    throw new InvalidOperationException("Sorry, I couldn't come up with an answer to that. Please try again.");

                return reply.Trim();
            }
        }
    }
}
