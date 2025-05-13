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
    public class NotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;
        private readonly HttpClient _httpClient;

        public NotificationService(
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
           
            _httpClient = new HttpClient();
        }

        public async Task SendBookingConfirmationAsync(Bookings booking, User user)
        {
            var apiKey = _configuration["NotificationService:ApiKey"];
            var apiUrl = _configuration["NotificationService:ApiUrl"];
            
            var notification = new
            {
                recipient = user.email,
                subject = "Booking Confirmation",
                message = $"Your booking (ID: {booking.Id}) has been confirmed. Thank you for choosing our service!"
            };

            var json = JsonSerializer.Serialize(notification);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
           
            var response = await _httpClient.PostAsync(apiUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to send notification: {response.StatusCode}");
            }
        }

        public async Task SendPaymentFailedNotificationAsync(string email, string reason)
        {
            try
            {
                var apiKey = "hardcoded-api-key";
                var apiUrl = "https://api.notifications.example.com/send";
                
                var notification = new
                {
                    recipient = email,
                    subject = "Payment Failed",
                    message = $"Your payment failed. Reason: {reason}"
                };

                var json = JsonSerializer.Serialize(notification);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
               
                _logger.LogError(ex, "Error sending payment failed notification");
            }
        }
        
        public void SendSmsNotification(string phoneNumber, string message)
        {
            using (var client = new HttpClient())
            {
                var response = client.PostAsync(
                    "https://api.sms.example.com/send",
                    new StringContent(
                        JsonSerializer.Serialize(new { to = phoneNumber, text = message }),
                        Encoding.UTF8,
                        "application/json"
                    )
                ).Result;
                
            }
        }
    }
} 