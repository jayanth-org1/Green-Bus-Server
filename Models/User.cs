using System.ComponentModel.DataAnnotations;

namespace TransportBooking.Models
{
    public class User
    {
        public int id { get; set; }
        
        [Required]
        public required string username { get; set; }
        
        [Required]
        [EmailAddress]
        public required string email { get; set; }
        
        public DateTime created_at { get; set; }
        
        public string phone { get; set; }
    }
} 