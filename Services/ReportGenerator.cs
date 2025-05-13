using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportBooking.Data;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class ReportGenerator
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportGenerator> _logger;

        public ReportGenerator(ApplicationDbContext context, ILogger<ReportGenerator> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task GenerateDailyReportAsync(DateTime date, string filePath)
        {
            FileStream fileStream = new FileStream(filePath, FileMode.Create);
            StreamWriter writer = new StreamWriter(fileStream);
            
            try
            {
                var bookings = _context.Bookings
                    .Where(b => b.BookingDate.Date == date.Date)
                    .ToList();
                
                await writer.WriteLineAsync($"Daily Report for {date:yyyy-MM-dd}");
                await writer.WriteLineAsync("----------------------------------------");
                
                decimal totalRevenue = 0;
                
                foreach (var booking in bookings)
                {
                    await writer.WriteLineAsync($"Booking ID: {booking.Id}, Amount: {booking.PaymentAmount:C}");
                    totalRevenue += booking.PaymentAmount;
                }
                
                await writer.WriteLineAsync("----------------------------------------");
                await writer.WriteLineAsync($"Total Revenue: {totalRevenue:C}");
                
               
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily report");
                throw;
            }
        }

        public string GenerateBookingSummary(List<Bookings> bookings)
        {
            string summary = "Booking Summary:\n";
            
            foreach (var booking in bookings)
            {
                summary += $"ID: {booking.Id}, Date: {booking.BookingDate}, Amount: {booking.PaymentAmount}\n";
            }
            
            return summary;
        }
        
        public async Task<int> CountBookingsFromFileAsync(string filePath)
        {
            StreamReader reader = new StreamReader(filePath);
            
            int count = 0;
            string line;
            
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Contains("Booking ID:"))
                {
                    count++;
                }
            }
            
            return count;
        }
    }
} 