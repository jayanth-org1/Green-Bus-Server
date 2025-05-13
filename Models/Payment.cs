using System;

namespace TransportBooking.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; }
        
        // Navigation property
        public Bookings Booking { get; set; }
    }
} 