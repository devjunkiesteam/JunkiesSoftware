using System.Text.Json;
using System.Text.Json.Serialization;
using JunkiesSoftware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace JunkiesSoftware;

public class UpdateAccountStatusFunction
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientEndpoint;
    private string _smartCreditToken;
    private DateTime _tokenExpirationTime;

    public UpdateAccountStatusFunction(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _clientEndpoint = _configuration["SmartcreditEndpoint"];
        _smartCreditToken = null;
        _tokenExpirationTime = DateTime.MinValue;
    }

    [Function("ProcessGoHighLevelRequest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
        FunctionContext context)
    {
        _ = context.GetLogger("ProcessGoHighLevelRequest");

        // Step 1: Retrieve data from the request
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var requestData = JsonSerializer.Deserialize<GoHighLevelRequest>(requestBody);

        // Step 2: Ensure SmartCredit token is available and valid
        if (string.IsNullOrEmpty(_smartCreditToken) || DateTime.UtcNow >= _tokenExpirationTime)
        {
            // Retrieve a new SmartCredit token
            await AcquireTokenAsync();
        }

        // Step 3: Retrieve client with their email address
        var client = await RetrieveClientAsync(requestData.EmailAddress);

        // Step 4: Update customer status with SmartCredit API
        var response = await UpdateCustomerStatusAsync(client.CustomerToken, requestData.Status);

        // Step 5: Return the response body
        var responseContent = JsonSerializer.Deserialize<SmartCreditResponse>(response);

        var responseMessage = req.CreateResponse();
        await responseMessage.WriteAsJsonAsync(responseContent);

        return responseMessage;
    }

    private async Task AcquireTokenAsync()
    {
        // Implement logic to acquire SmartCredit token
        // This method should retrieve a new token from the SmartCredit API

        // Example implementation using HttpClient to make a request to SmartCredit API
        var httpClient = _httpClientFactory.CreateClient();

        var clientId = _configuration["ClientKey"];
        var clientSecret = _configuration["ClientSecret"];

        var tokenEndpoint = $"{_clientEndpoint}/login";

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
         {
            { "client_id", clientId },
            { "client_secret", clientSecret }
         })
        };

        var tokenResponse = await httpClient.SendAsync(tokenRequest);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to acquire SmartCredit token: {tokenResponse.StatusCode}");
        }

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<SmartCreditTokenResponse>(tokenContent);

        _smartCreditToken = tokenData.AccessToken;
        _tokenExpirationTime = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

    }

    private async Task<Client> RetrieveClientAsync(string emailAddress)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_smartCreditToken}");
        var clientEndpoint = $"{_clientEndpoint}/v1/customers?email={emailAddress}"; // Replace with actual client endpoint
        var clientResponse = await httpClient.GetAsync(clientEndpoint);

        clientResponse.EnsureSuccessStatusCode();
        var clientData = await clientResponse.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Client>(clientData);
    }

    private async Task<string> UpdateCustomerStatusAsync(string customerToken, string status)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_smartCreditToken}");
        var updateEndpoint = $"{_clientEndpoint}customer/account/status"; // Replace with actual update endpoint with Prod Url
        var updateRequest = new UpdateRequest { Status = status };
        var updateContent = new StringContent(JsonSerializer.Serialize(updateRequest), System.Text.Encoding.UTF8, "application/json");
        var updateResponse = await httpClient.PutAsync($"{updateEndpoint}/{customerToken}", updateContent);

        updateResponse.EnsureSuccessStatusCode();
        return await updateResponse.Content.ReadAsStringAsync();
    }

    public class SmartCreditTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; }

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }

        // Add any other properties from the token response if needed
    }

}
