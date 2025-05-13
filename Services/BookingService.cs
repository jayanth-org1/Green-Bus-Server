using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TransportBooking.Data;
using TransportBooking.Models;

namespace TransportBooking.Services
{
    public class BookingService
    {
        private readonly ApplicationDbContext _context;

        public BookingService(ApplicationDbContext context)
        {
            _context = context;
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
            decimal refundAmount = booking.PaymentAmount * refundPercentage;

            // Update booking status
            booking.Status = "Cancelled";
            booking.PaymentStatus = Bookings.PaymentStatusEnum.Refunded;

            // Process refund (in a real app, this would integrate with payment provider)
            // ProcessRefund(booking.UserId, refundAmount);

            await _context.SaveChangesAsync();
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
            return await _context.Bookings
                .Where(b => b.RouteId == routeId && 
                       b.PaymentStatus == Bookings.PaymentStatusEnum.Paid)
                .SumAsync(b => b.PaymentAmount);
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
            // - Less than 3 days: 0% refund
            if (daysUntilTravel > 7)
                return 1.0m;
            else if (daysUntilTravel >= 3)
                return 0.5m;
            else
                return 0.0m;
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