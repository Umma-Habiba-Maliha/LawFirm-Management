using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LawFirmManagement.Services
{
    public class SSLCommerzService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _clientFactory;

        public SSLCommerzService(IConfiguration config, IHttpClientFactory clientFactory)
        {
            _config = config;
            _clientFactory = clientFactory;
        }

        // Updated signature to accept caseId and paymentStage
        public async Task<string> InitiatePayment(string totalAmount, string transactionId, string cusName, string cusEmail, string cusPhone, string returnUrlBase, string caseId, string paymentStage)
        {
            var storeId = _config["SSLCommerz:StoreId"];
            var storePass = _config["SSLCommerz:StorePass"];
            var isSandbox = bool.Parse(_config["SSLCommerz:IsSandbox"]);

            string baseUrl = isSandbox
                ? "https://sandbox.sslcommerz.com/gwprocess/v4/api.php"
                : "https://securepay.sslcommerz.com/gwprocess/v4/api.php";

            var postData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("store_id", storeId),
                new KeyValuePair<string, string>("store_passwd", storePass),
                new KeyValuePair<string, string>("total_amount", totalAmount),
                new KeyValuePair<string, string>("currency", "BDT"),
                new KeyValuePair<string, string>("tran_id", transactionId),
                
                // RETURN URLS (Where SSLCommerz sends the user back)
                new KeyValuePair<string, string>("success_url", $"{returnUrlBase}/Client/PaymentSuccess"),
                new KeyValuePair<string, string>("fail_url", $"{returnUrlBase}/Client/PaymentFail"),
                new KeyValuePair<string, string>("cancel_url", $"{returnUrlBase}/Client/PaymentCancel"),
                
                // PASS-THROUGH DATA (Crucial for identifying the payment on return)
                new KeyValuePair<string, string>("value_a", caseId),        // Sending Case ID
                new KeyValuePair<string, string>("value_b", paymentStage),  // Sending Stage (Advance/Final)
                
                // CUSTOMER INFO (Required)
                new KeyValuePair<string, string>("cus_name", cusName),
                new KeyValuePair<string, string>("cus_email", cusEmail),
                new KeyValuePair<string, string>("cus_phone", cusPhone ?? "01700000000"),
                new KeyValuePair<string, string>("cus_add1", "Dhaka"), // Mock address
                new KeyValuePair<string, string>("cus_city", "Dhaka"),
                new KeyValuePair<string, string>("cus_country", "Bangladesh"),
                
                // PRODUCT INFO
                new KeyValuePair<string, string>("shipping_method", "NO"),
                new KeyValuePair<string, string>("product_name", "Legal Fees"),
                new KeyValuePair<string, string>("product_category", "Service"),
                new KeyValuePair<string, string>("product_profile", "general")
            };

            var client = _clientFactory.CreateClient();
            var content = new FormUrlEncodedContent(postData);

            var response = await client.PostAsync(baseUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            // SSLCommerz returns JSON. We extract the Gateway URL.
            try
            {
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                if (jsonResponse.status == "SUCCESS")
                {
                    return jsonResponse.GatewayPageURL;
                }
            }
            catch
            {
                // Handle JSON parsing errors or unexpected responses
                return "";
            }

            return "";
        }
    }
}