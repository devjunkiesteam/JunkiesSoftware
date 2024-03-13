using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace JunkiesSoftware;

public partial class UpdateAccountStatusFunction
{
    private static readonly HttpClient httpClient = new HttpClient();
    private readonly ILogger<UpdateAccountStatusFunction> _logger;

    public UpdateAccountStatusFunction(ILogger<UpdateAccountStatusFunction> logger)
    {
        _logger = logger;
    }

    [Function("UpdateAccountStatus")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
         _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Parse query parameters
            string emailAddress = req.Query["email"];

            // Validate parameters
            if (string.IsNullOrEmpty(emailAddress))
            {
                return new BadRequestObjectResult("Please provide an email address.");
            }

            try
            {
                // Retrieve customer account information based on email address
                var customer = await RetrieveCustomerByEmail(emailAddress);

                if (customer != null)
                {
                    // Extract customer token
                    string customerToken = customer.CustomerToken;

                    // Update account status using customer token
                    string status = req.Query["status"];
                    string response = await UpdateAccountStatus(customerToken, status);

                    return new OkObjectResult(response);
                }
                else
                {
                    return new NotFoundObjectResult("Customer not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex.Message}");
                return new StatusCodeResult(500);
            }
    }
    private static async Task<CustomerResponse> RetrieveCustomerByEmail(string emailAddress)
        {
            // Build the request URL
            string apiUrl = $"https://stage-papi.consumerdirect.io/v1/customers?email={emailAddress}";

            // Make the GET request to retrieve customer information
            HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                // Deserialize and return the response body
                string responseBody = await response.Content.ReadAsStringAsync();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerResponse>(responseBody);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Customer not found
                return null;
            }
            else
            {
                // Handle other error cases
                throw new Exception($"Failed to retrieve customer information: {response.StatusCode}");
            }
        }

        private static async Task<string> UpdateAccountStatus(string customerToken, string status)
        {
            // Build the request URL
            string apiUrl = "https://stage-pws.consumerdirect.app/customer/account/status";

            // Build the request body
            var requestBody = new
            {
                customerToken,
                status
            };

            // Convert request body to JSON
            string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

            // Make the PUT request to update account status
            HttpResponseMessage response = await httpClient.PutAsync(apiUrl, new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                // Return the response body
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new Exception($"Failed to update account status: {response.StatusCode}");
            }
        }
}
