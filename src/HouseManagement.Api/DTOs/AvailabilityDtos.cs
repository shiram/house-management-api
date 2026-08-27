using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

public sealed class AvailabilitySlotDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class AvailabilityExceptionDto
{
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Reason { get; set; }
}

public sealed class AvailabilityDto
{
    public int HouseHelpId { get; set; }
    public IEnumerable<AvailabilitySlotDto> WeeklySlots { get; set; } = Array.Empty<AvailabilitySlotDto>();
    public IEnumerable<AvailabilityExceptionDto> Exceptions { get; set; } = Array.Empty<AvailabilityExceptionDto>();
}
