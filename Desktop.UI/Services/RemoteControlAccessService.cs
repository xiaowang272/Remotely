using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace Remotely.Desktop.UI.Services;

public class RemoteControlAccessService : IRemoteControlAccessService
{
    private readonly ILogger<RemoteControlAccessService> _logger;

    public RemoteControlAccessService(
        IViewModelFactory viewModelFactory,
        IUiDispatcher dispatcher,
        ILogger<RemoteControlAccessService> logger)
    {
        // viewModelFactory and dispatcher kept for DI compatibility but unused in silent mode
        _logger = logger;
    }

    public bool IsPromptOpen => false; // Silent mode: never shows prompt

    public async Task<PromptForAccessResult> PromptForAccess(string requesterName, string organizationName)
    {
        // Silent mode: auto-approve without showing prompt window
        _logger.LogInformation("Remote control access auto-approved for {RequesterName} from {OrganizationName} (silent mode)", 
            requesterName, organizationName);
        return await Task.FromResult(PromptForAccessResult.Accepted);
    }
}
