namespace HouseManagement.Api.Models;

// A pricing version starts as a manager-authored draft and becomes an immutable, effective-dated
// revision once published. Published versions are never edited; corrections require a new version.
public enum PricingVersionStatus
{
    Draft = 0,
    Published = 1
}
