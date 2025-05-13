using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TransportBooking.Data;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class ReportGenerator
    {
        private readonly ApplicationDbContext _context;
        private readonly RouteService _routeService;

        public ReportGenerator(ApplicationDbContext context, RouteService routeService)
        {
            _context = context;
            _routeService = routeService;
        }

        /// <summary>
        /// Generates a daily report of bookings for a specific date
        /// </summary>
        /// <param name="date">The date to generate the report for</param>
        /// <returns>A report as a string</returns>
        public async Task<string> GenerateDailyReportAsync(DateTime date)
        {
            var bookings = await _context.Bookings
                .Include(b => b.User)
                .Where(b => b.BookingDate.Date == date.Date)
                .OrderBy(b => b.BookingDate)
                .ToListAsync();

            var routes = await _context.Routes.ToListAsync();
            var routeDict = routes.ToDictionary(r => r.Id);

            var sb = new StringBuilder();
            sb.AppendLine($"Daily Booking Report for {date.ToString("yyyy-MM-dd")}");
            sb.AppendLine("===========================================");
            sb.AppendLine();

            // Summary statistics
            var totalBookings = bookings.Count;
            var confirmedBookings = bookings.Count(b => b.Status == "Confirmed");
            var cancelledBookings = bookings.Count(b => b.Status == "Cancelled");
            var totalRevenue = bookings
                .Where(b => b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .Sum(b => b.PaymentAmount);

            sb.AppendLine($"Total Bookings: {totalBookings}");
            sb.AppendLine($"Confirmed Bookings: {confirmedBookings}");
            sb.AppendLine($"Cancelled Bookings: {cancelledBookings}");
            sb.AppendLine($"Total Revenue: ${totalRevenue:F2}");
            sb.AppendLine();

            // Bookings by route
            var bookingsByRoute = bookings
                .GroupBy(b => b.RouteId)
                .Select(g => new
                {
                    RouteId = g.Key,
                    RouteName = routeDict.ContainsKey(g.Key) ? routeDict[g.Key].Name : "Unknown Route",
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            sb.AppendLine("Bookings by Route:");
            sb.AppendLine("------------------");
            foreach (var routeGroup in bookingsByRoute)
            {
                sb.AppendLine($"{routeGroup.RouteName}: {routeGroup.Count} bookings");
            }
            sb.AppendLine();

            // Detailed booking list
            sb.AppendLine("Detailed Booking List:");
            sb.AppendLine("----------------------");
            sb.AppendLine("ID\tUser\tRoute\tTravel Date\tSeat\tAmount\tStatus");
            
            foreach (var booking in bookings)
            {
                var routeName = routeDict.ContainsKey(booking.RouteId) ? routeDict[booking.RouteId].Name : "Unknown";
                sb.AppendLine($"{booking.Id}\t{booking.User?.username ?? "Unknown"}\t{routeName}\t{booking.TravelDate:yyyy-MM-dd}\t{booking.SeatNumber}\t${booking.PaymentAmount:F2}\t{booking.Status}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a summary of bookings for a date range
        /// </summary>
        /// <param name="startDate">Start date of the range</param>
        /// <param name="endDate">End date of the range</param>
        /// <returns>A BookingSummary object with statistics</returns>
        public async Task<BookingSummary> GenerateBookingSummary(DateTime startDate, DateTime endDate)
        {
            var bookings = await _context.Bookings
                .Where(b => b.BookingDate.Date >= startDate.Date && b.BookingDate.Date <= endDate.Date)
                .ToListAsync();

            var routesWithCounts = await _routeService.GetRoutesWithBookingCountsAsync();
            
            var summary = new BookingSummary
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalBookings = bookings.Count,
                ConfirmedBookings = bookings.Count(b => b.Status == "Confirmed"),
                CancelledBookings = bookings.Count(b => b.Status == "Cancelled"),
                TotalRevenue = bookings
                    .Where(b => b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                    .Sum(b => b.PaymentAmount),
                MostPopularRoute = routesWithCounts
                    .OrderByDescending(r => r.BookingCount)
                    .FirstOrDefault()?.Route?.Name ?? "No routes found",
                AverageBookingsPerDay = bookings.Count / (endDate - startDate).Days
            };

            return summary;
        }

        /// <summary>
        /// Counts bookings from a CSV file
        /// </summary>
        /// <param name="filePath">Path to the CSV file</param>
        /// <returns>Number of valid bookings in the file</returns>
        public async Task<int> CountBookingsFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The specified booking file was not found.", filePath);

            int validBookingsCount = 0;

            try
            {
                // Read all lines from the CSV file
                string[] lines = await File.ReadAllLinesAsync(filePath);
                
                // Skip header row if it exists
                bool hasHeader = lines.Length > 0 && lines[0].Contains("RouteId") && lines[0].Contains("UserId");
                int startIndex = hasHeader ? 1 : 0;
                
                // Process each line
                for (int i = startIndex; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    
                    string[] parts = line.Split(',');
                    
                    // Validate that the line has the expected format
                    if (parts.Length >= 4 && 
                        int.TryParse(parts[0], out int routeId) && 
                        int.TryParse(parts[1], out int userId) &&
                        DateTime.TryParse(parts[2], out DateTime travelDate) &&
                        int.TryParse(parts[3], out int seatNumber))
                    {
                        // Check if the route and user exist
                        bool routeExists = await _context.Routes.AnyAsync(r => r.Id == routeId);
                        bool userExists = await _context.Users.AnyAsync(u => u.id == userId);
                        
                        if (routeExists && userExists)
                        {
                            validBookingsCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing booking file: {ex.Message}", ex);
            }
            
            return validBookingsCount;
        }
    }

    /// <summary>
    /// Class to hold booking summary information
    /// </summary>
    public class BookingSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public string MostPopularRoute { get; set; }
        public double AverageBookingsPerDay { get; set; }
    }
} 