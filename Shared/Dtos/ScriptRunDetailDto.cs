namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for returning detailed script run information including all results.
/// </summary>
public class ScriptRunDetailDto : ScriptRunDto
{
    /// <summary>
    /// The list of script results for each device.
    /// </summary>
    public List<ScriptRunResultDto> Results { get; set; } = new();
}

/// <summary>
/// DTO for returning individual script result information.
/// </summary>
public class ScriptRunResultDto
{
    /// <summary>
    /// The unique identifier of the script result.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the device where the script was executed.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the device where the script was executed.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Whether the script execution had errors.
    /// </summary>
    public bool HadErrors { get; set; }

    /// <summary>
    /// The standard output from the script execution.
    /// </summary>
    public string[]? StandardOutput { get; set; }

    /// <summary>
    /// The error output from the script execution.
    /// </summary>
    public string[]? ErrorOutput { get; set; }

    /// <summary>
    /// The duration of the script execution.
    /// </summary>
    public TimeSpan RunTime { get; set; }

    /// <summary>
    /// The timestamp when the script result was recorded.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }
}
