using System;

namespace TransportBooking.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public string NotificationType { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsSuccess { get; set; }
        
        // Navigation properties
        public Bookings Booking { get; set; }
        public User User { get; set; }
    }
} 