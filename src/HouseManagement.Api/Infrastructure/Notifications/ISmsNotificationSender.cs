namespace HouseManagement.Api.Infrastructure.Notifications;

public sealed record SmsNotificationMessage(string To, string Body);

public interface ISmsNotificationSender
{
    Task SendAsync(SmsNotificationMessage message, CancellationToken cancellationToken = default);
}
