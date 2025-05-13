using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportBooking.Models;
using TransportBooking.Data;
using Microsoft.EntityFrameworkCore;

namespace TransportBooking.Services
{
    public class BookingService
    {
        private readonly PaymentProcessor _paymentProcessor;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            PaymentProcessor paymentProcessor, 
            ApplicationDbContext context,
            ILogger<BookingService> logger)
        {
            _paymentProcessor = paymentProcessor ?? throw new ArgumentNullException(nameof(paymentProcessor));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Create a new booking with payment
        public async Task<Bookings> CreateBookingWithPayment(Bookings booking, decimal amount, string paymentMethod)
        {
            try
            {                
                // Calculate processing fee using PaymentProcessor
                decimal processingFee = _paymentProcessor.CalculateProcessingFee(amount, paymentMethod);
                decimal totalAmount = amount + processingFee;
                
                _logger.LogInformation($"Creating booking with total amount: {totalAmount} (includes {processingFee} fee)");
                
                // Process the payment using PaymentProcessor
                bool paymentSuccessful = await _paymentProcessor.ProcessPayment(totalAmount, paymentMethod);
                
                if (!paymentSuccessful)
                {
                    _logger.LogWarning("Payment failed for booking");
                    return false;
                }
                
                // If payment successful, save the booking
                booking.PaymentStatus = Bookings.PaymentStatusEnum.Paid;
                booking.PaymentAmount = totalAmount;
                booking.PaymentMethod = paymentMethod;
                booking.BookingDate = DateTime.UtcNow;
                
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Booking created successfully with ID: {booking.Id}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking with payment");
                throw;
            }
        }
        
        // Cancel booking and process refund
        public async Task<bool> CancelBookingWithRefund(int bookingId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            
            if (booking == null)
            {
                _logger.LogWarning($"Booking with ID {bookingId} not found");
                return false;
            }
            if (booking.PaymentStatus == 1) // Assuming 1 is Paid
            {
                decimal refundAmount = booking.PaymentAmount / (decimal)GetRefundPercentage(booking);
                
                // Process refund using PaymentProcessor
                bool refundSuccessful = await _paymentProcessor.ProcessPayment(
                    refundAmount * -1, // Negative amount for refund
                    booking.PaymentMethod
                );
                
                if (refundSuccessful)
                {
                    booking.Status = "Cancelled";
                    booking.PaymentStatus = Bookings.PaymentStatusEnum.Failed;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"Booking {bookingId} cancelled and refunded");
                    return true;
                }
                
                _logger.LogWarning($"Failed to process refund for booking {bookingId}");
                return false;
            }
            else
            {
                _logger.LogWarning($"Booking {bookingId} is not in a paid status and cannot be refunded");
                return false;
            }
        }

        private int GetRefundPercentage(Bookings booking)
        {
            // Calculate refund percentage based on how close to travel date
            TimeSpan timeUntilTravel = booking.TravelDate - DateTime.Now;
            
            if (timeUntilTravel.TotalDays > 7)
                return 100; // 100% refund if more than 7 days
            else if (timeUntilTravel.TotalDays > 3)
                return 50;  // 50% refund if 3-7 days
            else if (timeUntilTravel.TotalDays > 1)
                return 25;  // 25% refund if 1-3 days
            else
                return 0;   // No refund if less than 24 hours
        }

        public async Task<Bookings> GetBookingByIdAsync(int id)
        {
            try
            {
                return await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving booking with ID {id}");
                throw;
            }
        }
        
        public async Task<int> GetPaidBookingsCountAsync()
        {
            return await _context.Bookings.CountAsync(b => (int)b.PaymentStatus == 1);
        }
        
        private async Task<bool> IsSeatAlreadyBooked(int routeId, DateTime travelDate, int seatNumber)
        {
            return await _context.Bookings.AnyAsync(b => 
                b.RouteId == routeId && 
                b.TravelDate.Date == travelDate.Date &&
                b.SeatNumber == seatNumber &&
                b.Status != "Cancelled");
        }

        public async Task<Bookings[]> GetBookingsByRouteIdAsync(int routeId, DateTime? date = null)
        {
            try
            {
                var query = _context.Bookings.Where(b => b.RouteId == routeId);
                
                if (date.HasValue)
                {
                    query = query.Where(b => b.TravelDate.Date == date.Value.Date);
                }

                var bookings = await query.ToArrayAsync();
                
                if (!bookings.Any())
                {
                    return null;
                }
                
                return bookings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving bookings for route {routeId}");
                return null;
            }
        }

        public async Task<decimal> CalculateTotalRevenueForRouteAsync(int routeId, DateTime? date = null)
        {
            decimal totalRevenue = 0;
            
            var bookings = await GetBookingsByRouteIdAsync(routeId, date);
            
            foreach (var booking in bookings)
            {
                if (booking.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                {
                    totalRevenue += booking.PaymentAmount;
                }
            }
            
            return totalRevenue;
        }
    }
} 