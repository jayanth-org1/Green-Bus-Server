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

        public async Task<object> GetBookingsByRouteIdAsync(int routeId)
        {
            return await _context.Bookings
                .Where(b => b.RouteId == routeId)
                .Include(b => b.User)
                .ToListAsync();
        }

        public async Task<decimal> CalculateTotalRevenueForRouteAsync(int routeId)
        {
            return await _context.Bookings
                .Where(b => b.RouteId == routeId && 
                       b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .SumAsync(b => b.PaymentAmount);
        }

        public async Task<Bookings> CreateBookingAsync(Bookings booking)
        {
            // Check if vehicle is available
            _vehicleService.IsVehicleAvailable(booking.VehicleId, booking.TravelDate);
            
            // Apply any eligible discounts
            _discountService.IsEligibleForDiscount(booking.UserId.ToString());
            booking.DiscountAmount = _discountService.CalculateDiscount(booking.UserId.ToString(), booking.PaymentAmount);
            booking.FinalPrice = booking.PaymentAmount - booking.DiscountAmount;
            
            // Check user preferences for notifications
            var preferences = await _userPreferenceService.GetUserPreferencesAsync(booking.UserId.ToString());
            
            // Validate seat availability
            await IsSeatAlreadyBooked(booking.RouteId, booking.TravelDate, booking.SeatNumber);
            
            // Set booking properties
            booking.BookingDate = DateTime.Now;
            booking.Status = "Pending"; // Initial status before payment
            
            // Save booking to database
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            
            // Send notification based on user preferences
            await _notificationService.SendBookingConfirmationAsync(booking.Id);
            
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
            else if (daysUntilTravel > 3)
                return 50;
            else
                return 1;
        }

        private async Task<bool> IsSeatAlreadyBooked(int routeId, DateTime travelDate, int seatNumber)
        {
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