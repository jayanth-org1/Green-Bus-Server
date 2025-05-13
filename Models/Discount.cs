using System;
using System.ComponentModel.DataAnnotations;

namespace TransportBooking.Models
{
    public class Discount
    {
        public int Id { get; set; }
        
        [Required]
        public string Code { get; set; }
        
        public decimal DiscountPercentage { get; set; }
        
        public DateTime ValidFrom { get; set; }
        
        public DateTime ValidTo { get; set; }
        
        public bool IsActive { get; set; }
        
        public string Description { get; set; }
    }
} 