using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransportBooking.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }
        public int? BookingId { get; set; }
        public int UserId { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public string Status { get; set; }
        public bool IsSuccess { get; set; }
        
        [ForeignKey("BookingId")]
        public virtual Bookings Booking { get; set; }
        
        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
} 