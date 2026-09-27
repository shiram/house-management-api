using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServicePricingVersionService
{
    // Validates the mode-specific snapshot against the service's current pricing mode and creates
    // a draft version. Returns null when the service does not exist or the snapshot is invalid.
    Task<ServicePricingVersion?> CreateDraftAsync(int serviceId, ServicePricingVersion draft);

    // Publishes a draft version, transactionally closing the previously open published version (if
    // any) at the new version's EffectiveFrom. Fails if the version does not exist, is not a draft,
    // or would overlap the currently open published window.
    Task<PricingVersionPublishResult> PublishAsync(int versionId);

    // Resolves the single published version effective for a service at the given instant, or null
    // if no published version covers that instant.
    Task<ServicePricingVersion?> GetEffectiveVersionAsync(int serviceId, DateTimeOffset atUtc);

    Task<IEnumerable<ServicePricingVersion>> GetVersionsAsync(int serviceId);
}

public sealed record PricingVersionPublishResult(ServicePricingVersion? Version, string? Error)
{
    public bool Succeeded => Version != null;
}
