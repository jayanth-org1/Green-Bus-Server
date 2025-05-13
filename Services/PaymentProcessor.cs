using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransportBooking.Data;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class PaymentProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentProcessor> _logger;
        private readonly HttpClient _httpClient;
        private readonly NotificationService _notificationService;

        public PaymentProcessor(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PaymentProcessor> logger,
            HttpClient httpClient,
            NotificationService notificationService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Processes a payment for a booking
        /// </summary>
        /// <param name="bookingId">The ID of the booking</param>
        /// <param name="paymentDetails">Payment details including card information</param>
        /// <returns>A PaymentResult object with the result of the payment processing</returns>
        public async Task<PaymentResult> ProcessPayment(int bookingId, PaymentDetails paymentDetails)
        {
            try
            {
                // Get the booking
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                {
                    _logger.LogWarning($"Booking with ID {bookingId} not found when processing payment");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "Booking not found",
                        TransactionId = null
                    };
                }

                // Validate payment details
                var validationResult = ValidatePaymentDetails(paymentDetails);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning($"Invalid payment details for booking {bookingId}: {validationResult.ErrorMessage}");
                    
                    // Send payment failed notification
                    await _notificationService.SendPaymentFailedNotificationAsync(bookingId, validationResult.ErrorMessage);
                    
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = validationResult.ErrorMessage,
                        TransactionId = null
                    };
                }

                // Calculate processing fee
                decimal processingFee = CalculateProcessingFee(booking.PaymentAmount, paymentDetails.PaymentMethod);
                decimal totalAmount = booking.PaymentAmount + processingFee;

                // Get payment gateway configuration
                string paymentGatewayUrl = _configuration["PaymentGateway:ApiUrl"];
                string paymentGatewayApiKey = _configuration["PaymentGateway:ApiKey"];
                string paymentGatewayMerchantId = _configuration["PaymentGateway:MerchantId"];

                if (string.IsNullOrEmpty(paymentGatewayUrl) || 
                    string.IsNullOrEmpty(paymentGatewayApiKey) || 
                    string.IsNullOrEmpty(paymentGatewayMerchantId))
                {
                    _logger.LogError("Payment gateway configuration is missing");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "Payment gateway configuration error",
                        TransactionId = null
                    };
                }

                // Prepare payment request
                var paymentRequest = new
                {
                    MerchantId = paymentGatewayMerchantId,
                    Amount = totalAmount,
                    Currency = "USD",
                    PaymentMethod = paymentDetails.PaymentMethod,
                    CardNumber = MaskCardNumber(paymentDetails.CardNumber),
                    CardHolderName = paymentDetails.CardHolderName,
                    ExpiryMonth = paymentDetails.ExpiryMonth,
                    ExpiryYear = paymentDetails.ExpiryYear,
                    CVV = "***", // Don't log or send actual CVV
                    Description = $"Payment for booking #{bookingId}",
                    CustomerEmail = booking.User.email,
                    BillingAddress = new
                    {
                        paymentDetails.BillingAddress.AddressLine1,
                        paymentDetails.BillingAddress.AddressLine2,
                        paymentDetails.BillingAddress.City,
                        paymentDetails.BillingAddress.State,
                        paymentDetails.BillingAddress.PostalCode,
                        paymentDetails.BillingAddress.Country
                    }
                };

                // Convert to JSON
                var content = new StringContent(
                    JsonSerializer.Serialize(paymentRequest),
                    Encoding.UTF8,
                    "application/json");

                // Add API key to headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", paymentGatewayApiKey);

                // In a real application, we would send the request to the payment gateway
                // For this example, we'll simulate a successful payment
                // var response = await _httpClient.PostAsync(paymentGatewayUrl, content);
                
                // Simulate payment gateway response
                bool paymentSuccessful = SimulatePaymentGatewayResponse(paymentDetails);
                string transactionId = paymentSuccessful ? Guid.NewGuid().ToString() : null;

                if (paymentSuccessful)
                {
                    // Update booking with payment information
                    booking.PaymentStatus = Bookings.PaymentStatusEnum.Paid;
                    booking.Status = "Confirmed";
                    booking.PaymentMethod = paymentDetails.PaymentMethod;
                    
                    // Create payment record
                    var payment = new Models.Payment
                    {
                        BookingId = booking.Id,
                        Amount = booking.PaymentAmount,
                        ProcessingFee = processingFee,
                        TotalAmount = totalAmount,
                        PaymentMethod = paymentDetails.PaymentMethod,
                        TransactionId = transactionId,
                        PaymentDate = DateTime.UtcNow,
                        Status = "Completed"
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Send booking confirmation
                    await _notificationService.SendBookingConfirmationAsync(bookingId);

                    return new PaymentResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        TransactionId = transactionId,
                        ProcessingFee = processingFee,
                        TotalAmount = totalAmount
                    };
                }
                else
                {
                    // Update booking status
                    booking.PaymentStatus = Bookings.PaymentStatusEnum.Failed;
                    await _context.SaveChangesAsync();

                    // Send payment failed notification
                    await _notificationService.SendPaymentFailedNotificationAsync(bookingId, "Payment was declined by the payment processor");

                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "Payment was declined by the payment processor",
                        TransactionId = null
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing payment for booking {bookingId}");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing the payment",
                    TransactionId = null
                };
            }
        }

        /// <summary>
        /// Validates payment details
        /// </summary>
        /// <param name="paymentDetails">The payment details to validate</param>
        /// <returns>A validation result</returns>
        public ValidationResult ValidatePaymentDetails(PaymentDetails paymentDetails)
        {
            // Check for null
            if (paymentDetails == null)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Payment details are required" };
            }

            // Validate card number (basic validation)
            if (string.IsNullOrWhiteSpace(paymentDetails.CardNumber))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Card number is required" };
            }

            // Remove spaces and dashes
            string cardNumber = paymentDetails.CardNumber.Replace(" ", "").Replace("-", "");
            
            // Check if card number contains only digits
            if (!System.Text.RegularExpressions.Regex.IsMatch(cardNumber, @"^\d+$"))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Card number must contain only digits" };
            }

            // Check card number length (most cards are 13-19 digits)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Card number length is invalid" };
            }

            // Validate card holder name
            if (string.IsNullOrWhiteSpace(paymentDetails.CardHolderName))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Card holder name is required" };
            }

            // Validate expiry date
            if (paymentDetails.ExpiryMonth < 1 || paymentDetails.ExpiryMonth > 12)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Invalid expiry month" };
            }

            int currentYear = DateTime.Now.Year % 100; // Get last two digits of year
            if (paymentDetails.ExpiryYear < currentYear || paymentDetails.ExpiryYear > currentYear + 20)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Invalid expiry year" };
            }

            // Check if card is expired
            if (paymentDetails.ExpiryYear == currentYear && paymentDetails.ExpiryMonth < DateTime.Now.Month)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Card has expired" };
            }

            // Validate CVV
            if (string.IsNullOrWhiteSpace(paymentDetails.CVV))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "CVV is required" };
            }

            // CVV should be 3-4 digits
            if (!System.Text.RegularExpressions.Regex.IsMatch(paymentDetails.CVV, @"^\d{3,4}$"))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "CVV must be 3 or 4 digits" };
            }

            // Validate payment method
            if (string.IsNullOrWhiteSpace(paymentDetails.PaymentMethod))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Payment method is required" };
            }

            // Check if payment method is supported
            string[] supportedMethods = { "CreditCard", "DebitCard", "PayPal" };
            if (!Array.Exists(supportedMethods, method => method.Equals(paymentDetails.PaymentMethod, StringComparison.OrdinalIgnoreCase)))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Unsupported payment method" };
            }

            // Validate billing address
            if (paymentDetails.BillingAddress == null)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Billing address is required" };
            }

            if (string.IsNullOrWhiteSpace(paymentDetails.BillingAddress.AddressLine1))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Billing address line 1 is required" };
            }

            if (string.IsNullOrWhiteSpace(paymentDetails.BillingAddress.City))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "City is required" };
            }

            if (string.IsNullOrWhiteSpace(paymentDetails.BillingAddress.PostalCode))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Postal code is required" };
            }

            if (string.IsNullOrWhiteSpace(paymentDetails.BillingAddress.Country))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "Country is required" };
            }

            // All validations passed
            return new ValidationResult { IsValid = true, ErrorMessage = null };
        }

        /// <summary>
        /// Calculates the processing fee for a payment
        /// </summary>
        /// <param name="amount">The payment amount</param>
        /// <param name="paymentMethod">The payment method</param>
        /// <returns>The processing fee</returns>
        public decimal CalculateProcessingFee(decimal amount, string paymentMethod)
        {
            // Different payment methods may have different fee structures
            switch (paymentMethod.ToLower())
            {
                case "creditcard":
                    // 2.9% + $0.30 fixed fee
                    return Math.Round(amount * 0.029m + 0.30m, 2);
                
                case "debitcard":
                    // 1.5% + $0.30 fixed fee
                    return Math.Round(amount * 0.015m + 0.30m, 2);
                
                case "paypal":
                    // 3.49% + $0.49 fixed fee
                    return Math.Round(amount * 0.0349m + 0.49m, 2);
                
                default:
                    // Default fee structure
                    return Math.Round(amount * 0.025m + 0.30m, 2);
            }
        }

        // Helper methods
        private string MaskCardNumber(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
                return cardNumber;

            // Remove spaces and dashes
            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");
            
            // Keep only the last 4 digits
            return "XXXX-XXXX-XXXX-" + cardNumber.Substring(cardNumber.Length - 4);
        }

        private bool SimulatePaymentGatewayResponse(PaymentDetails paymentDetails)
        {
            // For testing purposes, we'll simulate payment failures for specific scenarios
            
            // Simulate a declined card if the card number ends with "0000"
            if (paymentDetails.CardNumber.EndsWith("0000"))
                return false;
            
            // Simulate a declined card if the CVV is "000"
            if (paymentDetails.CVV == "000")
                return false;
            
            // Simulate a random failure with 5% probability
            if (new Random().Next(100) < 5)
                return false;
            
            // Otherwise, payment is successful
            return true;
        }
    }

    // Models for payment processing
    public class PaymentDetails
    {
        public string CardNumber { get; set; }
        public string CardHolderName { get; set; }
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CVV { get; set; }
        public string PaymentMethod { get; set; }
        public BillingAddress BillingAddress { get; set; }
    }

    public class BillingAddress
    {
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionId { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
} 