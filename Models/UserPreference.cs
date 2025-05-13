using System;
using System.ComponentModel.DataAnnotations;

namespace TransportBooking.Models
{
    public class UserPreference
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        public bool ReceiveBookingConfirmations { get; set; } = true;
        
        public bool ReceivePromotionalEmails { get; set; } = false;
        
        public string DefaultPaymentMethod { get; set; }
        
        public string PreferredLanguage { get; set; } = "en";
    }
} 