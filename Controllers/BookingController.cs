using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransportBooking.Data;
using TransportBooking.Models;
using TransportBooking.Services;

namespace TransportBooking.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly BookingService _bookingService;

        public BookingController(BookingService bookingService)
        {
            _bookingService = bookingService;
        }

        // POST: api/Booking
        [HttpPost]
        public async Task<ActionResult<Bookings>> CreateBooking(Bookings booking)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdBooking = await _bookingService.CreateBookingWithPayment(booking, booking.PaymentAmount);
                return CreatedAtAction(nameof(GetBooking), new { id = createdBooking.Id }, createdBooking);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/Booking/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Bookings>> GetBooking(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);

            if (booking == null)
            {
                return NotFound();
            }

            return booking;
        }

        // PUT: api/Booking/{id}/cancel
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            try
            {
                await _bookingService.CancelBookingWithRefund(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/Booking/route/{routeId}
        [HttpGet("route/{routeId}")]
        public async Task<ActionResult> GetBookingsByRoute(int routeId)
        {
            var bookings = await _bookingService.GetBookingsByRouteIdAsync(routeId);
            return Ok(bookings);
        }

        // GET: api/Booking/route/{routeId}/revenue
        [HttpGet("route/{routeId}/revenue")]
        public async Task<ActionResult> GetRouteRevenue(int routeId)
        {
            var revenue = await _bookingService.CalculateTotalRevenueForRouteAsync(routeId);
            return Ok(new { Revenue = revenue });
        }

        // GET: api/Booking/count
        [HttpGet("count")]
        public async Task<ActionResult> GetPaidBookingsCount()
        {
            var count = await _bookingService.GetPaidBookingsCountAsync();
            return Ok(new { Count = count });
        }
    }
} 