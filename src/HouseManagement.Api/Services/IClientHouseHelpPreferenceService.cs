using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IClientHouseHelpPreferenceService
{
    Task<ClientHouseHelpPreferenceResult> AddAsync(int clientUserId, int houseHelpId);
    Task<bool> RemoveAsync(int clientUserId, int houseHelpId);
    Task<IReadOnlyList<ClientHouseHelpPreference>> GetForClientAsync(int clientUserId, int? page = null, int? pageSize = null);
}

public sealed record ClientHouseHelpPreferenceResult(ClientHouseHelpPreference? Preference, string? Error);
