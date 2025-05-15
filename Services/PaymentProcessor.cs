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
        private readonly UserPreferenceService _userPreferenceService;
        private readonly PaymentProviderService _paymentProviderService;

        public PaymentProcessor(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PaymentProcessor> logger,
            HttpClient httpClient,
            NotificationService notificationService,
            UserPreferenceService userPreferenceService,
            PaymentProviderService paymentProviderService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
            _notificationService = notificationService;
            _userPreferenceService = userPreferenceService;
            _paymentProviderService = paymentProviderService;
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
                        TransactionId = string.Empty
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
                        TransactionId = string.Empty
                    };
                }

                // Calculate processing fee
                decimal processingFee = CalculateProcessingFee(booking.PaymentAmount, paymentDetails.PaymentMethod);
                decimal totalAmount = booking.PaymentAmount + processingFee;

                // Process payment through gateway
                var paymentResult = await ProcessPaymentWithGateway(paymentDetails, totalAmount);
                
                if (paymentResult.Success)
                {
                    // Create payment record
                    var payment = new Payment
                    {
                        BookingId = bookingId,
                        Amount = booking.PaymentAmount,
                        ProcessingFee = processingFee,
                        TotalAmount = totalAmount,
                        PaymentMethod = paymentDetails.PaymentMethod,
                        TransactionId = paymentResult.TransactionId,
                        AuthorizationCode = paymentResult.AuthorizationCode,
                        PaymentDate = DateTime.UtcNow,
                        Status = "Completed"
                    };

                    _context.Payments.Add(payment);
            
                    // Update booking status
                    booking.PaymentStatus = Bookings.PaymentStatusEnum.Paid;
            
                    await _context.SaveChangesAsync();
            
                    // Send booking confirmation which includes payment details
                    var userPreferences = await _userPreferenceService.GetUserPreferencesAsync(booking.UserId.ToString());
                    if (userPreferences.ReceiveBookingConfirmations)
                    {
                        await _notificationService.SendBookingConfirmationAsync(bookingId);
                    }
            
                    return new PaymentResult
                    {
                        Success = true,
                        TransactionId = paymentResult.TransactionId,
                        AuthorizationCode = paymentResult.AuthorizationCode,
                        ProcessingFee = processingFee,
                        TotalAmount = totalAmount
                    };
                }
                else
                {
                    // Log payment failure
                    _logger.LogWarning($"Payment failed for booking {bookingId}: {paymentResult.ErrorMessage}");
            
                    // Send payment failed notification
                    await _notificationService.SendPaymentFailedNotificationAsync(bookingId, paymentResult.ErrorMessage);
            
                    return paymentResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing payment for booking {bookingId}");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred while processing your payment",
                    TransactionId = string.Empty
                };
            }
        }

        /// <summary>
        /// Processes a refund for a booking
        /// </summary>
        /// <param name="bookingId">The ID of the booking to refund</param>
        /// <param name="refundAmount">The amount to refund</param>
        /// <param name="reason">The reason for the refund</param>
        /// <returns>A PaymentResult object with the result of the refund processing</returns>
        public async Task<PaymentResult> ProcessRefund(int bookingId, decimal refundAmount, string reason)
        {
            try
            {
                // Get the booking and its payment
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                {
                    _logger.LogWarning($"Booking with ID {bookingId} not found when processing refund");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "Booking not found",
                        TransactionId = null
                    };
                }

                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == bookingId && p.Status == "Completed");

                if (payment == null)
                {
                    _logger.LogWarning($"No completed payment found for booking {bookingId}");
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "No payment found for this booking",
                        TransactionId = null
                    };
                }

                // Validate refund amount
                if (refundAmount <= 0 || refundAmount > payment.Amount)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid refund amount",
                        TransactionId = null
                    };
                }

                // Prepare refund request
                var refundRequest = new RefundRequest
                {
                    OriginalTransactionId = payment.TransactionId,
                    Amount = refundAmount,
                    Reason = reason,
                    CustomerEmail = booking.User.email
                };

                // Process refund through payment provider service
                var gatewayResponse = await _paymentProviderService.ProcessRefundAsync(refundRequest);

                if (gatewayResponse.Success && await _paymentProviderService.VerifyRefundStatus(gatewayResponse.TransactionId))
                {
                    // Create refund record
                    var refund = new Models.Payment
                    {
                        BookingId = booking.Id,
                        Amount = -refundAmount, // Negative amount for refunds
                        ProcessingFee = 0,
                        TotalAmount = -refundAmount,
                        PaymentMethod = payment.PaymentMethod,
                        TransactionId = gatewayResponse.TransactionId,
                        AuthorizationCode = gatewayResponse.AuthorizationCode,
                        PaymentDate = DateTime.UtcNow,
                        Status = "Refunded",
                        RelatedPaymentId = payment.Id
                    };

                    // Update booking status if full refund
                    if (refundAmount >= payment.Amount)
                    {
                        booking.Status = "Refunded";
                        booking.PaymentStatus = Bookings.PaymentStatusEnum.Refunded;
                    }
                    else
                    {
                        booking.Status = "PartiallyRefunded";
                    }

                    _context.Payments.Add(refund);
                    await _context.SaveChangesAsync();

                    // Send refund confirmation
                    await _notificationService.SendRefundConfirmationAsync(bookingId, refundAmount);

                    return new PaymentResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        TransactionId = gatewayResponse.TransactionId,
                        ProcessingFee = 0,
                        TotalAmount = refundAmount
                    };
                }
                else
                {
                    // Send refund failed notification
                    await _notificationService.SendRefundFailedNotificationAsync(bookingId, gatewayResponse.ErrorMessage);

                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = gatewayResponse.ErrorMessage,
                        TransactionId = null
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing refund for booking {bookingId}");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing the refund",
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

        public async Task<bool> ProcessPaymentAsync(string userId, decimal amount, string paymentMethod)
        {
            // Check if user has saved payment methods
            if (_userPreferenceService.HasSavedPaymentMethod(userId) && paymentMethod == "saved")
            {
                var preferences = await _userPreferenceService.GetUserPreferencesAsync(userId);
                // Use saved payment method from preferences
                paymentMethod = preferences.DefaultPaymentMethod;
            }
            
            // ... existing payment processing code ...
            
            return true;
        }

        private async Task<PaymentResult> ProcessPaymentWithGateway(PaymentDetails paymentDetails, decimal amount)
        {
            try
            {
                // Create payment request directly (no need for PaymentGatewayRequest)
                var paymentRequest = new PaymentRequest
                {
                    Amount = amount,
                    Currency = "USD",
                    PaymentMethod = paymentDetails.PaymentMethod,
                    CardNumber = paymentDetails.CardNumber,
                    CardHolderName = paymentDetails.CardHolderName,
                    ExpiryMonth = paymentDetails.ExpiryMonth,
                    ExpiryYear = paymentDetails.ExpiryYear,
                    CVV = paymentDetails.CVV,
                    Description = "Transport booking payment",
                    CustomerEmail = paymentDetails.CustomerEmail,
                    BillingAddress = paymentDetails.BillingAddress
                };

                // Process payment through gateway
                var gatewayResponse = await _paymentProviderService.ProcessPaymentAsync(paymentRequest);

                if (gatewayResponse.Success)
                {
                    return new PaymentResult
                    {
                        Success = true,
                        TransactionId = gatewayResponse.TransactionId,
                        AuthorizationCode = gatewayResponse.AuthorizationCode,
                        ErrorMessage = string.Empty
                    };
                }
                else
                {
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = gatewayResponse.ErrorMessage,
                        TransactionId = string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment through gateway");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing the payment",
                    TransactionId = string.Empty
                };
            }
        }
    }

    // Models for payment processing
    public class PaymentDetails
    {
        public string CardNumber { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CVV { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public BillingAddress BillingAddress { get; set; } = new BillingAddress();
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
        public string ErrorMessage { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string AuthorizationCode { get; set; } = string.Empty;
        public decimal ProcessingFee { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
} 