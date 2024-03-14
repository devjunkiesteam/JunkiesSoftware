using System.Text.Json;
using JunkiesSoftware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JunkiesSoftware;

public partial class UpdateAccountStatusFunction
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
   private readonly string _clientEndpoint;

    public UpdateAccountStatusFunction(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
         _clientEndpoint = _configuration["SmartcreditEndpoint"];
        
    }

    [Function("ProcessGoHighLevelRequest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessGoHighLevelRequest");

        // Step 1: Retrieve data from the request
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var requestData = JsonSerializer.Deserialize<GoHighLevelRequest>(requestBody);

        // Step 2: Acquire token from SmartCredit
        var token = await AcquireTokenAsync();

        // Step 3: Retrieve client with their email address
        var client = await RetrieveClientAsync(requestData.EmailAddress, token);

        // Step 4: Update customer status with SmartCredit API
        var response = await UpdateCustomerStatusAsync(client.CustomerToken, requestData.Status, token);

        // Step 5: Return the response body
        var responseContent = JsonSerializer.Deserialize<SmartCreditResponse>(response);

        var responseMessage = req.CreateResponse();
        await responseMessage.WriteAsJsonAsync(responseContent);

        return responseMessage;
    }

   private async Task<string> AcquireTokenAsync()
    {
        // Replace dummy values with actual ClientKey and ClientSecret
        var clientKey = _configuration["ClientKey"];
        var clientSecret = _configuration["ClientSecret"];
        
        var httpClient = _httpClientFactory.CreateClient();
        var tokenEndpoint = $"{_clientEndpoint}/login"; // Replace with actual token endpoint
        var tokenResponse = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string?, string?>("client_id", clientKey),
            new KeyValuePair<string?, string?>("client_secret", clientSecret)
        }));

        tokenResponse.EnsureSuccessStatusCode();
        return await tokenResponse.Content.ReadAsStringAsync();
    }

   private async Task<Client> RetrieveClientAsync(string emailAddress, string token)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var clientEndpoint = $"{_clientEndpoint}/v1/customers?email={emailAddress}"; // Replace with actual client endpoint
        var clientResponse = await httpClient.GetAsync(clientEndpoint);

        clientResponse.EnsureSuccessStatusCode();
        var clientData = await clientResponse.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Client>(clientData);
    }

    private async Task<string> UpdateCustomerStatusAsync(string customerToken, string status, string token)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var updateEndpoint = $"{_clientEndpoint}customer/account/status"; // Replace with actual update endpoint
        var updateRequest = new UpdateRequest { Status = status };
        var updateContent = new StringContent(JsonSerializer.Serialize(updateRequest), System.Text.Encoding.UTF8, "application/json");
        var updateResponse = await httpClient.PutAsync($"{updateEndpoint}/{customerToken}", updateContent);

        updateResponse.EnsureSuccessStatusCode();
        return await updateResponse.Content.ReadAsStringAsync();
    }
}
