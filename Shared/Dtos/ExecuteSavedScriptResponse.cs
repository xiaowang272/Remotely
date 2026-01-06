namespace Remotely.Shared.Dtos;

/// <summary>
/// Response DTO for saved script execution.
/// </summary>
public class ExecuteSavedScriptResponse
{
    /// <summary>
    /// The ID of the script run record created for tracking.
    /// </summary>
    public int ScriptRunId { get; set; }

    /// <summary>
    /// The number of devices the script will be executed on.
    /// </summary>
    public int DeviceCount { get; set; }

    /// <summary>
    /// The current status of the script execution.
    /// </summary>
    public string Status { get; set; } = "Queued";
}
