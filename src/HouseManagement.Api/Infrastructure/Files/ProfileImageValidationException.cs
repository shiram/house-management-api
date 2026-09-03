namespace HouseManagement.Api.Infrastructure.Files;

public sealed class ProfileImageValidationException : InvalidOperationException
{
    public ProfileImageValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Profile image validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
