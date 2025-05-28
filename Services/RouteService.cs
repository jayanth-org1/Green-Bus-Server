using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TransportBooking.Data;
using TransportBooking.Models;
using Route = TransportBooking.Models.Route;

namespace TransportBooking.Services
{
    public class RouteService
    {
        private readonly ApplicationDbContext _context;

        public RouteService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets all available routes
        /// </summary>
        public async Task<List<Route>> GetAllRoutesAsync()
        {
            return await _context.Routes
                .OrderBy(r => r.DepartureTime)
                .ToListAsync();
        }

        /// <summary>
        /// Gets routes with their booking counts
        /// </summary>
        public async Task<List<RouteWithBookingCount>> GetRoutesWithBookingCountsAsync()
        {
            var routes = await _context.Routes.ToListAsync();
            var result = new List<RouteWithBookingCount>();

            foreach (var route in routes)
            {
                // Inefficient N+1 query - should use a join instead
                var bookingCount = await _context.Bookings
                    .Where(b => b.RouteId == route.Id && b.Status != "Cancelled")
                    .ToListAsync();

                result.Add(new RouteWithBookingCount
                {
                    Route = route,
                    BookingCount = bookingCount.Count()
                });
            }

            return result;
        }

        /// <summary>
        /// Searches routes by name (origin, destination or route name)
        /// </summary>
        public async Task<List<Route>> SearchRoutesByNameAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllRoutesAsync();

            searchTerm = searchTerm.ToLower();

            return await _context.Routes
                .Where(r => r.Origin.ToLower().Contains(searchTerm) ||
                           r.Destination.ToLower().Contains(searchTerm) ||
                           r.Name.ToLower().Contains(searchTerm))
                .OrderBy(r => r.DepartureTime)
                .ToListAsync();
        }

        /// <summary>
        /// Updates the capacity of a route
        /// </summary>
        public async Task<bool> UpdateRouteCapacityAsync(int routeId, int newCapacity)
        {
            var route = await _context.Routes.FindAsync(routeId);
            
            if (route == null)
                return false;

            // Check if the new capacity is less than the current booking count
            var currentBookingCount = await _context.Bookings
                .CountAsync(b => b.RouteId == routeId && b.Status != "Cancelled");

            if (newCapacity <= currentBookingCount)
                throw new InvalidOperationException($"Cannot reduce capacity below current booking count ({currentBookingCount})");

            route.Capacity = newCapacity;
            await _context.SaveChangesAsync();
            
            return true;
        }
    }

    // Helper class for returning routes with booking counts
    public class RouteWithBookingCount
    {
        public Route Route { get; set; }
        public int BookingCount { get; set; }
        public int AvailableSeats => Route.Capacity - BookingCount;
    }
} 