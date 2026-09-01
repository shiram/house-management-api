namespace HouseManagement.Api.Infrastructure.Notifications;

public sealed record EmailNotificationMessage(string To, string Subject, string Body);

public interface IEmailNotificationSender
{
    Task SendAsync(EmailNotificationMessage message, CancellationToken cancellationToken = default);
}
