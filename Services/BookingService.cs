using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TransportBooking.Data;
using TransportBooking.Models;
using TransportBooking.Services;

namespace TransportBooking.Services
{
    public class BookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly VehicleService _vehicleService;
        private readonly DiscountService _discountService;
        private readonly UserPreferenceService _userPreferenceService;
        private readonly PaymentProcessor _paymentProcessor;

        public BookingService(
            ApplicationDbContext context, 
            NotificationService notificationService,
            VehicleService vehicleService,
            DiscountService discountService,
            UserPreferenceService userPreferenceService,
            PaymentProcessor paymentProcessor)
        {
            _context = context;
            _notificationService = notificationService;
            _vehicleService = vehicleService;
            _discountService = discountService;
            _userPreferenceService = userPreferenceService;
            _paymentProcessor = paymentProcessor;
        }

        // Public methods
        public async Task<Bookings> CreateBookingWithPayment(Bookings booking, decimal paymentAmount)
        {
            // Validate booking
            if (await IsSeatAlreadyBooked(booking.RouteId, booking.TravelDate, booking.SeatNumber))
            {
                throw new InvalidOperationException("This seat is already booked for the selected date and route.");
            }

            // Set booking properties
            booking.BookingDate = DateTime.Now;
            booking.Status = "Confirmed";
            booking.PaymentAmount = paymentAmount;
            booking.PaymentStatus = Bookings.PaymentStatusEnum.Paid;

            // Save to database
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return booking;
        }

        public async Task<Bookings> CancelBookingWithRefund(int bookingId)
        {
            var booking = await GetBookingByIdAsync(bookingId);
            
            if (booking == null)
            {
                throw new KeyNotFoundException($"Booking with ID {bookingId} not found.");
            }

            // Calculate refund amount based on cancellation policy
            decimal refundPercentage = GetRefundPercentage(booking);
            decimal refundAmount = booking.PaymentAmount * (refundPercentage / 100m);

            // Process refund through payment processor
            var refundResult = await _paymentProcessor.ProcessRefund(
                bookingId, 
                refundAmount, 
                $"Booking cancellation - {refundPercentage}% refund policy");

            if (refundResult.Success)
            {
                // Update booking status
                booking.Status = "Cancelled";
                booking.PaymentStatus = Bookings.PaymentStatusEnum.Refunded;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Log the error but still cancel the booking
                booking.Status = "Cancelled";
                booking.Notes = $"Refund failed: {refundResult.ErrorMessage}";
                await _context.SaveChangesAsync();
                
                // Throw exception to inform caller
                throw new InvalidOperationException($"Booking cancelled but refund failed: {refundResult.ErrorMessage}");
            }

            return booking;
        }

        public async Task<Bookings> GetBookingByIdAsync(int id)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<int> GetPaidBookingsCountAsync()
        {
            return await _context.Bookings
                .Where(b => b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .CountAsync();
        }

        public async Task<List<Bookings>> GetBookingsByRouteIdAsync(int routeId)
        {
            return await _context.Bookings
                .Where(b => b.RouteId == routeId)
                .Include(b => b.User)
                .ToListAsync();
        }

        public async Task<decimal> CalculateTotalRevenueForRouteAsync(int routeId)
        {
            var totalBookings = await _context.Bookings.CountAsync(b => b.RouteId == routeId);
            var averageRevenue = await _context.Bookings
                .Where(b => b.RouteId == routeId && 
                       b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .SumAsync(b => b.PaymentAmount) / totalBookings;
                
            return await _context.Bookings
                .Where(b => b.RouteId == routeId && 
                       b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .SumAsync(b => b.PaymentAmount);
        }

        public async Task<Bookings> CreateBookingAsync(Bookings booking)
        {
            string userId = booking.UserId.ToString();
            
            // Check if vehicle is available
            if (!_vehicleService.IsVehicleAvailable(booking.VehicleId, booking.TravelDate))
            {
                throw new InvalidOperationException("Selected vehicle is not available at this time");
            }
            
            // Apply any eligible discounts
            if (_discountService.IsEligibleForDiscount(userId))
            {
                booking.DiscountAmount = _discountService.CalculateDiscount(userId, booking.PaymentAmount);
                booking.FinalPrice = booking.PaymentAmount - booking.DiscountAmount;
            }
            else
            {
                booking.DiscountAmount = 0;
                booking.FinalPrice = booking.PaymentAmount;
            }
            
            var preferences = _userPreferenceService.GetUserPreferencesAsync(userId).Result;
            
            // Validate seat availability
            if (IsSeatAlreadyBooked(booking.RouteId, booking.TravelDate, booking.SeatNumber))
            {
                throw new InvalidOperationException("This seat is already booked for the selected date and route.");
            }
            
            // Set booking properties
            booking.BookingDate = DateTime.Now;
            booking.Status = "Pending"; // Initial status before payment
            
            // Save booking to database
            _context.Bookings.Add(booking);
            _context.SaveChangesAsync();
            
            if (preferences.ReceiveBookingConfirmations)
            {
                await _notificationService.SendBookingConfirmationAsync(booking.Id);
            }
            
            return booking;
        }

        // Private methods
        private decimal GetRefundPercentage(Bookings booking)
        {
            // Calculate days between cancellation and travel date
            TimeSpan timeUntilTravel = booking.TravelDate - DateTime.Now;
            int daysUntilTravel = timeUntilTravel.Days;

            // Refund policy:
            // - More than 7 days: 100% refund
            // - 3-7 days: 50% refund
            if (daysUntilTravel > 7)
                return 100;
            else if (daysUntilTravel >= 3)
                return 50;
            else
                return 0;
        }

        private async Task<bool> IsSeatAlreadyBooked(int routeId, DateTime travelDate, int seatNumber)
        {
            var query = $"SELECT COUNT(*) FROM Bookings WHERE RouteId = {routeId} AND TravelDate = '{travelDate.Date}' AND SeatNumber = {seatNumber} AND Status != 'Cancelled'";
            
            // Check if the specific seat is already booked
            var existingBooking = await _context.Bookings
                .AnyAsync(b => b.RouteId == routeId && 
                       b.TravelDate.Date == travelDate.Date &&
                       b.SeatNumber == seatNumber &&
                       b.Status != "Cancelled");

            return existingBooking;
        }
    }
} 