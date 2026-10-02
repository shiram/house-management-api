namespace HouseManagement.Api.Infrastructure.Payments;

// Shared, DI-managed cache for the Pesapal bearer token (T402). PesapalPaymentGateway is
// registered as a typed HttpClient service, which the HttpClientFactory creates fresh per
// resolution, so caching the token on the gateway instance itself would not actually avoid a
// RequestToken call per payment. This singleton is injected into the gateway instead, so the
// token is requested at most once per refresh window across the whole application, and
// concurrent callers racing a refresh share the same in-flight request rather than each starting
// their own.
public sealed class PesapalTokenCache
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(
        Func<CancellationToken, Task<(string Token, DateTimeOffset? ExpiresAt)>> requestToken,
        CancellationToken cancellationToken)
    {
        if (IsValid())
        {
            return _token!;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsValid())
            {
                return _token!;
            }

            var (token, expiresAt) = await requestToken(cancellationToken);
            _token = token;
            // Refresh a little before the real expiry so a token is never used right at its edge;
            // default to a conservative 5-minute lifetime if Pesapal omits an expiry.
            _expiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsValid()
    {
        return _token != null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(30);
    }
}
