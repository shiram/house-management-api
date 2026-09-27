namespace HouseManagement.Api.Models;

// One per-unit rate snapshot belonging to an immutable ServicePricingVersion.
public sealed class ServicePricingVersionUnit
{
    public int Id { get; set; }
    public int ServicePricingVersionId { get; set; }
    public ServicePricingVersion PricingVersion { get; set; } = null!;
    public string UnitName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
}
