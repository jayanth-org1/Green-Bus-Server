using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransportBooking.Models
{
    public class Routes
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Origin { get; set; }
        
        [Required]
        public string Destination { get; set; }
        
        [Required]
        public DateTime DepartureTime { get; set; }
        
        [Required]
        public DateTime ArrivalTime { get; set; }
        
        [Required]
        public int Capacity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasePrice { get; set; }
        
        public int AvailableSeats { get; set; }
    }
} 