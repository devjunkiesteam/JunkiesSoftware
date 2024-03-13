using System.Text;
using Newtonsoft.Json;

namespace JunkiesSoftware
{
    public static class SmartCreditApiClient
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string authToken;

        public static async Task<string> GetAuthTokenAsync(string clientKey, string clientSecret)
        {
            if (string.IsNullOrEmpty(authToken))
            {

                // Build the request body
                var requestBody = new
                {
                    clientKey,
                    clientSecret
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                // Make the POST request to obtain the JWT token
                HttpResponseMessage response = await httpClient.PostAsync("https://stage-pws.consumerdirect.app/login", content);

                if (response.IsSuccessStatusCode)
                {
                    // Read and store the JWT token from the response
                    var responseContent = await response.Content.ReadAsStringAsync();
                    authToken = JsonConvert.DeserializeObject<dynamic>(responseContent).Authorization;
                }
                else
                {
                    throw new Exception($"Failed to obtain JWT token: {response.StatusCode}");
                }
            }

            return authToken;
        }

        public static async Task<string> MakeAuthenticatedRequestAsync(string apiUrl, HttpMethod method, string jsonBody = null)
        {
            string token = await GetAuthTokenAsync("", "");

            // Add JWT token to request headers
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Build the request content
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Make the API request
            HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(method, apiUrl) { Content = content });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new Exception($"Failed to make authenticated request: {response.StatusCode}");
            }
        }
    }
}