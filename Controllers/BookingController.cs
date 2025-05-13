using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TransportBooking.Models;
using TransportBooking.Services;

namespace TransportBooking.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly BookingService _bookingService;
        private readonly ILogger<BookingController> _logger;

        public BookingController(BookingService bookingService, ILogger<BookingController> logger)
        {
            _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var booking = new Bookings
                {
                    UserId = request.UserId,
                    RouteId = request.RouteId,
                    TravelDate = request.TravelDate,
                    SeatNumber = request.SeatNumber
                };

                bool result = await _bookingService.CreateBookingWithPayment(
                    booking, 
                    request.Amount, 
                    request.PaymentMethod
                );

                if (result)
                {
                    return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
                }
                else
                {
                    return BadRequest("Payment processing failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                return StatusCode(500, "An error occurred while creating the booking");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(int id)
        {
            try
            {
                var booking = await _bookingService.GetBookingByIdAsync(id);
                
                if (booking == null)
                {
                    return NotFound();
                }
                
                return Ok(booking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving booking with ID {id}");
                return StatusCode(500, "An error occurred while retrieving the booking");
            }
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            try
            {
                bool result = await _bookingService.CancelBookingWithRefund(id);
                
                if (result)
                {
                    return Ok(new { message = "Booking cancelled and refunded successfully" });
                }
                else
                {
                    return BadRequest("Failed to cancel booking or process refund");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling booking with ID {id}");
                return StatusCode(500, "An error occurred while cancelling the booking");
            }
        }
    }
} 