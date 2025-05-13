using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransportBooking.Models
{
    public class Bookings
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int RouteId { get; set; }
        
        [Required]
        public DateTime TravelDate { get; set; }
        
        public DateTime BookingDate { get; set; }
        
        [Required]
        public int SeatNumber { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaymentAmount { get; set; }
        
        public string PaymentMethod { get; set; }
        
        // Rename the enum to avoid naming conflict with the property
        public enum PaymentStatusEnum
        {
            Pending,   // 0
            Paid,      // 1
            Failed,    // 2
            Refunded,  // 3
            Processing // 4
        }
        
        // Use the renamed enum type for the property
        public PaymentStatusEnum PaymentStatus { get; set; }
        
        public string Status { get; set; } = "Pending";
        
        // Navigation properties
        public User User { get; set; }
    }
} 