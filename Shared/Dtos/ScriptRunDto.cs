using Remotely.Shared.Enums;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for returning script run summary information.
/// </summary>
public class ScriptRunDto
{
    /// <summary>
    /// The unique identifier of the script run.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date and time when the script was executed.
    /// </summary>
    public DateTimeOffset RunAt { get; set; }

    /// <summary>
    /// The user or system that initiated the script run.
    /// </summary>
    public string? Initiator { get; set; }

    /// <summary>
    /// The type of input that triggered the script run.
    /// </summary>
    public ScriptInputType InputType { get; set; }

    /// <summary>
    /// The ID of the saved script that was executed.
    /// </summary>
    public Guid? SavedScriptId { get; set; }

    /// <summary>
    /// The name of the saved script that was executed.
    /// </summary>
    public string? SavedScriptName { get; set; }

    /// <summary>
    /// The total number of devices the script was executed on.
    /// </summary>
    public int DeviceCount { get; set; }

    /// <summary>
    /// The number of devices where the script executed successfully.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// The number of devices where the script execution failed.
    /// </summary>
    public int FailureCount { get; set; }
}
