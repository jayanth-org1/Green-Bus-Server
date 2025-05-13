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
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;
        private readonly HttpClient _httpClient;

        public NotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<NotificationService> logger,
            HttpClient httpClient)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Sends a booking confirmation notification to the user
        /// </summary>
        /// <param name="bookingId">The ID of the booking</param>
        /// <returns>True if the notification was sent successfully</returns>
        public async Task<bool> SendBookingConfirmationAsync(int bookingId)
        {
            try
            {
                // Get booking details with user information
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                {
                    _logger.LogWarning($"Booking with ID {bookingId} not found when sending confirmation");
                    return false;
                }

                // Get route information
                var route = await _context.Routes.FindAsync(booking.RouteId);
                if (route == null)
                {
                    _logger.LogWarning($"Route with ID {booking.RouteId} not found when sending confirmation for booking {bookingId}");
                    return false;
                }

                // Prepare email content
                string subject = "Your Booking Confirmation";
                string body = $@"
                    <h2>Booking Confirmation</h2>
                    <p>Dear {booking.User.username},</p>
                    <p>Your booking has been confirmed with the following details:</p>
                    <ul>
                        <li><strong>Booking ID:</strong> {booking.Id}</li>
                        <li><strong>Route:</strong> {route.Origin} to {route.Destination}</li>
                        <li><strong>Travel Date:</strong> {booking.TravelDate:dddd, MMMM d, yyyy}</li>
                        <li><strong>Departure Time:</strong> {route.DepartureTime:h:mm tt}</li>
                        <li><strong>Seat Number:</strong> {booking.SeatNumber}</li>
                        <li><strong>Amount Paid:</strong> ${booking.PaymentAmount:F2}</li>
                    </ul>
                    <p>Thank you for choosing our service!</p>
                ";

                // Send email notification
                bool emailSent = await SendEmailAsync(booking.User.email, subject, body);

                // If user has a phone number, also send SMS
                if (!string.IsNullOrEmpty(booking.User.phone))
                {
                    string smsMessage = $"Your booking #{booking.Id} from {route.Origin} to {route.Destination} on {booking.TravelDate:MM/dd/yyyy} is confirmed. Seat: {booking.SeatNumber}";
                    await SendSmsNotification(booking.User.phone, smsMessage);
                }

                // Log the notification
                await LogNotificationAsync(booking.Id, booking.User.id, "Booking Confirmation", emailSent);

                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending booking confirmation for booking {bookingId}");
                return false;
            }
        }

        /// <summary>
        /// Sends a payment failed notification to the user
        /// </summary>
        /// <param name="bookingId">The ID of the booking</param>
        /// <param name="paymentErrorMessage">The error message from the payment processor</param>
        /// <returns>True if the notification was sent successfully</returns>
        public async Task<bool> SendPaymentFailedNotificationAsync(int bookingId, string paymentErrorMessage)
        {
            try
            {
                // Get booking details with user information
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                {
                    _logger.LogWarning($"Booking with ID {bookingId} not found when sending payment failed notification");
                    return false;
                }

                // Get route information
                var route = await _context.Routes.FindAsync(booking.RouteId);
                if (route == null)
                {
                    _logger.LogWarning($"Route with ID {booking.RouteId} not found when sending payment failed notification for booking {bookingId}");
                    return false;
                }

                // Prepare email content
                string subject = "Payment Failed for Your Booking";
                string body = $@"
                    <h2>Payment Failed</h2>
                    <p>Dear {booking.User.username},</p>
                    <p>We were unable to process your payment for the following booking:</p>
                    <ul>
                        <li><strong>Booking ID:</strong> {booking.Id}</li>
                        <li><strong>Route:</strong> {route.Origin} to {route.Destination}</li>
                        <li><strong>Travel Date:</strong> {booking.TravelDate:dddd, MMMM d, yyyy}</li>
                        <li><strong>Amount:</strong> ${booking.PaymentAmount:F2}</li>
                    </ul>
                    <p><strong>Error:</strong> {paymentErrorMessage}</p>
                    <p>Please update your payment information or try again with a different payment method.</p>
                    <p>If you need assistance, please contact our customer support.</p>
                ";

                // Send email notification
                bool emailSent = await SendEmailAsync(booking.User.email, subject, body);

                // If user has a phone number, also send SMS
                if (!string.IsNullOrEmpty(booking.User.phone))
                {
                    string smsMessage = $"Payment failed for booking #{booking.Id}. Please update your payment information or contact customer support.";
                    await SendSmsNotification(booking.User.phone, smsMessage);
                }

                // Log the notification
                await LogNotificationAsync(booking.Id, booking.User.id, "Payment Failed", emailSent);

                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending payment failed notification for booking {bookingId}");
                return false;
            }
        }

        /// <summary>
        /// Sends an SMS notification to the specified phone number
        /// </summary>
        /// <param name="phoneNumber">The recipient's phone number</param>
        /// <param name="message">The SMS message content</param>
        /// <returns>True if the SMS was sent successfully</returns>
        public async Task<bool> SendSmsNotification(string phoneNumber, string message)
        {
            try
            {
                // Get SMS API configuration
                string smsApiUrl = _configuration["SmsService:ApiUrl"];
                string smsApiKey = _configuration["SmsService:ApiKey"];

                if (string.IsNullOrEmpty(smsApiUrl) || string.IsNullOrEmpty(smsApiKey))
                {
                    _logger.LogWarning("SMS API configuration is missing");
                    return false;
                }

                // Prepare the request payload
                var smsRequest = new
                {
                    To = phoneNumber,
                    Message = message,
                    From = _configuration["SmsService:SenderName"] ?? "TransportBooking"
                };

                // Convert to JSON
                var content = new StringContent(
                    JsonSerializer.Serialize(smsRequest),
                    Encoding.UTF8,
                    "application/json");

                // Add API key to headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", smsApiKey);

                // Send the request
                var response = await _httpClient.PostAsync(smsApiUrl, content);

                // Check if successful
                bool isSuccess = response.IsSuccessStatusCode;
                
                if (!isSuccess)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"SMS API returned error: {errorResponse}");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending SMS to {phoneNumber}");
                return false;
            }
        }

        // Private helper methods
        private async Task<bool> SendEmailAsync(string email, string subject, string htmlBody)
        {
            try
            {
                // Get email service configuration
                string emailApiUrl = _configuration["EmailService:ApiUrl"];
                string emailApiKey = _configuration["EmailService:ApiKey"];
                string senderEmail = _configuration["EmailService:SenderEmail"] ?? "noreply@transportbooking.com";
                string senderName = _configuration["EmailService:SenderName"] ?? "Transport Booking";

                if (string.IsNullOrEmpty(emailApiUrl) || string.IsNullOrEmpty(emailApiKey))
                {
                    _logger.LogWarning("Email API configuration is missing");
                    return false;
                }

                // Prepare the request payload
                var emailRequest = new
                {
                    From = new { Email = senderEmail, Name = senderName },
                    To = new[] { new { Email = email } },
                    Subject = subject,
                    HtmlContent = htmlBody
                };

                // Convert to JSON
                var content = new StringContent(
                    JsonSerializer.Serialize(emailRequest),
                    Encoding.UTF8,
                    "application/json");

                // Add API key to headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", emailApiKey);

                // Send the request
                var response = await _httpClient.PostAsync(emailApiUrl, content);

                // Check if successful
                bool isSuccess = response.IsSuccessStatusCode;
                
                if (!isSuccess)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Email API returned error: {errorResponse}");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {email}");
                return false;
            }
        }

        private async Task LogNotificationAsync(int? bookingId, int userId, string notificationType, bool isSuccess)
        {
            var notificationLog = new NotificationLog
            {
                BookingId = bookingId,
                UserId = userId,
                Type = notificationType,
                Message = $"Notification of type {notificationType} sent",
                SentAt = DateTime.UtcNow,
                Status = isSuccess ? "Sent" : "Failed",
                IsSuccess = isSuccess
            };

            _context.NotificationLogs.Add(notificationLog);
            await _context.SaveChangesAsync();
        }

        public async Task SendRefundConfirmationAsync(int bookingId, decimal refundAmount)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
            {
                _logger.LogWarning($"Booking with ID {bookingId} not found when sending refund confirmation");
                return;
            }

            var notificationLog = new NotificationLog
            {
                UserId = booking.UserId,
                Type = "RefundConfirmation",
                Message = $"Your refund of ${refundAmount} for booking #{bookingId} has been processed successfully.",
                SentAt = DateTime.UtcNow,
                Status = "Sent"
            };

            _context.NotificationLogs.Add(notificationLog);
            await _context.SaveChangesAsync();

            // In a real application, we would send an email or SMS here
            _logger.LogInformation($"Refund confirmation sent to user {booking.UserId} for booking {bookingId}");
        }

        public async Task SendRefundFailedNotificationAsync(int bookingId, string errorMessage)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
            {
                _logger.LogWarning($"Booking with ID {bookingId} not found when sending refund failed notification");
                return;
            }

            var notificationLog = new NotificationLog
            {
                UserId = booking.UserId,
                Type = "RefundFailed",
                Message = $"We encountered an issue processing your refund for booking #{bookingId}. Our team has been notified and will contact you shortly.",
                SentAt = DateTime.UtcNow,
                Status = "Sent"
            };

            _context.NotificationLogs.Add(notificationLog);
            await _context.SaveChangesAsync();

            // In a real application, we would send an email or SMS here
            _logger.LogInformation($"Refund failed notification sent to user {booking.UserId} for booking {bookingId}");
            
            // Also log the actual error for internal purposes
            _logger.LogError($"Refund failed for booking {bookingId}: {errorMessage}");
        }
    }
} 