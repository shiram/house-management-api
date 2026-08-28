using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using HouseManagement.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;
    private readonly IBookingStatusService _bookingStatuses;

    public BookingsController(IBookingService bookings, IBookingStatusService bookingStatuses)
    {
        _bookings = bookings;
        _bookingStatuses = bookingStatuses;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAnonymous([FromBody] CreateAnonymousBookingRequest request)
    {
        var result = await _bookings.CreateAnonymousAsync(request);
        if (result.Booking == null)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var booking = result.Booking;
        var response = ApiResponseFactory.Create(this, ToDto(booking), "Booking request created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(CreateAnonymous), new { id = booking.Id }, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var booking = await _bookings.GetByIdAsync(id);
        if (booking == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(booking), "Booking retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _bookingStatuses.CancelAsync(id);
        if (result.Booking == null)
        {
            if (result.Error == "The requested booking was not found.")
            {
                return NotFound();
            }

            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(
            this,
            ToDto(result.Booking),
            "Booking cancelled",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        var result = await _bookingStatuses.RejectAsync(id);
        if (result.Booking == null)
        {
            if (result.Error == "The requested booking was not found.")
            {
                return NotFound();
            }

            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(
            this,
            ToDto(result.Booking),
            "Booking rejected",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        var result = await _bookingStatuses.ConfirmAsync(id);
        if (result.Booking == null)
        {
            if (result.Error == "The requested booking was not found.")
            {
                return NotFound();
            }

            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(
            this,
            ToDto(result.Booking),
            "Booking confirmed",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await _bookingStatuses.CompleteAsync(id);
        if (result.Booking == null)
        {
            if (result.Error == "The requested booking was not found.")
            {
                return NotFound();
            }

            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(
            this,
            ToDto(result.Booking),
            "Booking completed",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] BookingStatus? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var bookings = await _bookings.GetListAsync(status, page, pageSize);
        var response = ApiResponseFactory.Create(
            this,
            bookings.Select(ToDto),
            "Bookings retrieved",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    private static BookingDto ToDto(Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            Reference = booking.Reference,
            ServiceId = booking.ServiceId,
            ServiceCode = booking.Service.Code,
            ServiceName = booking.Service.Name,
            ScheduledStart = booking.ScheduledStart,
            ScheduledEnd = booking.ScheduledEnd,
            Status = booking.Status,
            Address = new ServiceAddressRequest
            {
                Line1 = booking.ServiceAddress.Line1,
                Line2 = booking.ServiceAddress.Line2,
                City = booking.ServiceAddress.City,
                Region = booking.ServiceAddress.Region,
                PostalCode = booking.ServiceAddress.PostalCode,
                Country = booking.ServiceAddress.Country
            },
            Notes = booking.Notes,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.UpdatedAt
        };
    }
}
