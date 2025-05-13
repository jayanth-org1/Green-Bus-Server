using System;
using System.ComponentModel.DataAnnotations;

namespace TransportBooking.Models
{
    public class BookingRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int RouteId { get; set; }
        
        [Required]
        public DateTime TravelDate { get; set; }
        
        [Required]
        public int SeatNumber { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string PaymentMethod { get; set; }
    }
} 