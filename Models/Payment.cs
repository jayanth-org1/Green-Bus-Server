using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransportBooking.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        [Required]
        public decimal Amount { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal TotalAmount { get; set; }
        [Required]
        public string PaymentMethod { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string? AuthorizationCode { get; set; }
        public DateTime PaymentDate { get; set; }
        [Required]
        public string Status { get; set; } = string.Empty;
        // For refunds, reference the original payment
        public int? RelatedPaymentId { get; set; }
        [ForeignKey("BookingId")]
        public virtual Bookings? Booking { get; set; }
        [ForeignKey("RelatedPaymentId")]
        public virtual Payment? RelatedPayment { get; set; }
    }
} 