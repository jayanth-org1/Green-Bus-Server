using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransportBooking.Data;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class RouteService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RouteService> _logger;

        public RouteService(ApplicationDbContext context, ILogger<RouteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Routes>> GetAllRoutesAsync()
        {
            return await _context.Routes.ToListAsync();
        }

        public async Task<List<RouteWithBookingsCount>> GetRoutesWithBookingCountsAsync()
        {
            var routes = await _context.Routes.ToListAsync();
            var result = new List<RouteWithBookingsCount>();

            foreach (var route in routes)
            {
                int bookingsCount = await _context.Bookings.CountAsync(b => b.RouteId == route.Id);
                
                result.Add(new RouteWithBookingsCount
                {
                    Route = route,
                    BookingsCount = bookingsCount
                });
            }

            return result;
        }

        public async Task<List<Routes>> SearchRoutesByNameAsync(string searchTerm)
        {
            var rawSql = $"SELECT * FROM Routes WHERE Name LIKE '%{searchTerm}%'";
            
            return await _context.Routes.FromSqlRaw(rawSql).ToListAsync();
        }

        public async Task<bool> UpdateRouteCapacityAsync(int routeId, int newCapacity)
        {
            var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var route = await _context.Routes.FindAsync(routeId);
                if (route == null)
                {
                    return false;
                }

                route.Capacity = newCapacity;
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating capacity for route {routeId}");
                return false;
            }
        }
    }

    public class RouteWithBookingsCount
    {
        public Routes Route { get; set; }
        public int BookingsCount { get; set; }
    }
} 