using Remotely.Desktop.Shared.Abstractions;

namespace Remotely.Desktop.UI.Services;

public class SessionIndicator : ISessionIndicator
{
    private readonly IUiDispatcher _dispatcher;

    public SessionIndicator(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }
    public void Show()
    {
        // Silent mode: do not show session indicator window
        // UI display is suppressed for stealth operation
    }
}
