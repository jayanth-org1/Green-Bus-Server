using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TransportBooking.Models
{
    public class Vehicle
    {
        public Vehicle()
        {
            // Initialize collections to avoid null reference exceptions
            Bookings = new List<Bookings>();
        }
        
        public int Id { get; set; }
        
        [Required]
        public string RegistrationNumber { get; set; } = string.Empty;
        
        [Required]
        public string Model { get; set; } = string.Empty;
        
        public int Capacity { get; set; }
        
        public bool IsActive { get; set; }
        
        public virtual ICollection<Bookings> Bookings { get; set; }
    }
} 