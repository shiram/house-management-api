namespace HouseManagement.Api.Models;

// The context that causes a surcharge to apply to a booking.
public enum SurchargeTriggerType
{
    // The requested scheduled start falls on a Saturday or Sunday.
    Weekend = 0,

    // The requested scheduled start falls on a configured public holiday.
    Holiday = 1,

    // The requested scheduled start's time-of-day falls within a configured after-hours window.
    AfterHours = 2,

    // The requested scheduled start is within a configured minimum lead time from now.
    Urgent = 3,

    // The booking address's city or region matches a configured location value.
    Location = 4
}
