using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class PaymentProviderService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentProviderService> _logger;
        private readonly HttpClient _httpClient;

        public PaymentProviderService(
            IConfiguration configuration,
            ILogger<PaymentProviderService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Processes a payment through the payment gateway
        /// </summary>
        /// <param name="paymentRequest">The payment request details</param>
        /// <returns>A payment gateway response</returns>
        public async Task<PaymentGatewayResponse> ProcessPaymentAsync(PaymentRequest request)
        {
            try
            {
                _logger.LogInformation($"Processing payment of {request.Amount} {request.Currency} for {request.CustomerEmail}");
                
                // In a real implementation, this would call an external payment gateway API
                // For this example, we'll simulate a successful payment
                
                // Simulate processing time
                await Task.Delay(500);
                
                // Return a successful response
                return new PaymentGatewayResponse
                {
                    Success = true,
                    TransactionId = Guid.NewGuid().ToString(),
                    AuthorizationCode = GenerateRandomAuthCode(),
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment");
                return new PaymentGatewayResponse
                {
                    Success = false,
                    ErrorMessage = "Payment processing failed: " + ex.Message,
                    TransactionId = string.Empty,
                    AuthorizationCode = string.Empty
                };
            }
        }

        /// <summary>
        /// Processes a refund through the payment gateway
        /// </summary>
        /// <param name="refundRequest">The refund request details</param>
        /// <returns>A payment gateway response</returns>
        public async Task<PaymentGatewayResponse> ProcessRefundAsync(RefundRequest refundRequest)
        {
            try
            {
                // Get payment gateway configuration
                string paymentGatewayUrl = _configuration["PaymentGateway:RefundApiUrl"];
                string paymentGatewayApiKey = _configuration["PaymentGateway:ApiKey"];

                if (string.IsNullOrEmpty(paymentGatewayUrl) || 
                    string.IsNullOrEmpty(paymentGatewayApiKey))
                {
                    _logger.LogError("Payment gateway refund configuration is missing");
                    return new PaymentGatewayResponse
                    {
                        Success = false,
                        ErrorMessage = "Payment gateway configuration error",
                        TransactionId = null
                    };
                }

                // Convert to JSON
                var content = new StringContent(
                    JsonSerializer.Serialize(refundRequest),
                    Encoding.UTF8,
                    "application/json");

                // Add API key to headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", paymentGatewayApiKey);

                // In a real application, we would send the request to the payment gateway
                // For this example, we'll simulate a successful refund
                // var response = await _httpClient.PostAsync(paymentGatewayUrl, content);
                
                // Simulate refund gateway response (95% success rate)
                bool refundSuccessful = new Random().Next(100) < 95;
                string transactionId = refundSuccessful ? Guid.NewGuid().ToString() : null;

                if (refundSuccessful)
                {
                    return new PaymentGatewayResponse
                    {
                        Success = true,
                        ErrorMessage = null,
                        TransactionId = transactionId,
                        AuthorizationCode = GenerateAuthorizationCode(),
                        ProcessedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    return new PaymentGatewayResponse
                    {
                        Success = false,
                        ErrorMessage = "Refund was declined by the payment processor",
                        TransactionId = null
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund through gateway");
                return new PaymentGatewayResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing the refund",
                    TransactionId = null
                };
            }
        }

        // Helper methods
        private bool SimulatePaymentGatewayResponse(PaymentGatewayRequest request)
        {
            // For testing purposes, we'll simulate payment failures for specific scenarios
            
            // Simulate a declined card if the card number ends with "0000"
            if (request.CardNumber != null && request.CardNumber.EndsWith("0000"))
                return false;
            
            // Simulate a declined card if the amount is exactly 666.66
            if (Math.Abs(request.Amount - 666.66m) < 0.01m)
                return false;
            
            // Simulate a random failure with 5% probability
            if (new Random().Next(100) < 5)
                return false;
            
            // Otherwise, payment is successful
            return true;
        }

        private string GenerateAuthorizationCode()
        {
            // Generate a random authorization code (6 alphanumeric characters)
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateRandomAuthCode()
        {
            // Implement the logic to generate a random authorization code
            // This is a placeholder and should be replaced with the actual implementation
            return Guid.NewGuid().ToString().Substring(0, 8); // Simplified example
        }
    }

    // Models for payment gateway interactions
    public class PaymentGatewayRequest
    {
        public string MerchantId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string PaymentMethod { get; set; }
        public string CardNumber { get; set; }
        public string CardHolderName { get; set; }
        public int? ExpiryMonth { get; set; }
        public int? ExpiryYear { get; set; }
        public string CVV { get; set; }
        public string Description { get; set; }
        public string CustomerEmail { get; set; }
        public BillingAddress BillingAddress { get; set; }
    }

    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string PaymentMethod { get; set; }
        public string CardNumber { get; set; }
        public string CardHolderName { get; set; }
        public int? ExpiryMonth { get; set; }
        public int? ExpiryYear { get; set; }
        public string CVV { get; set; }
        public string Description { get; set; }
        public string CustomerEmail { get; set; }
        public BillingAddress BillingAddress { get; set; }
    }

    public class RefundRequest
    {
        public string OriginalTransactionId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
        public string CustomerEmail { get; set; }
    }

    public class PaymentGatewayResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionId { get; set; }
        public string AuthorizationCode { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
} 