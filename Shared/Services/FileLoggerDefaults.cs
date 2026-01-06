using Remotely.Shared.Primitives;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Services;
public static class FileLoggerDefaults
{
    private static readonly SemaphoreSlim _logLock = new(1, 1);

    public static string LogsFolderPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var logsPath = @"C:\Windows\Logs";

                if (EnvironmentHelper.IsDebug)
                {
                    logsPath += "_Debug";
                }
                return logsPath;
            }

            throw new PlatformNotSupportedException();
        }
    }

    public static async Task<IDisposable> AcquireLock(CancellationToken cancellationToken = default)
    {
        await _logLock.WaitAsync(cancellationToken);
        return new CallbackDisposable(() => _logLock.Release());
    }

    public static string GetLogPath(string componentName) => 
        Path.Combine(LogsFolderPath, componentName, $"LogFile_{DateTime.Now:yyyy-MM-dd}.log");

}
