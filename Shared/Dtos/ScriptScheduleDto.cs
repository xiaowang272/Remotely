using Remotely.Shared.Enums;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for returning script schedule information.
/// </summary>
public class ScriptScheduleDto
{
    /// <summary>
    /// The unique identifier of the schedule.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The name of the schedule.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the saved script to execute.
    /// </summary>
    public Guid SavedScriptId { get; set; }

    /// <summary>
    /// The name of the saved script.
    /// </summary>
    public string? SavedScriptName { get; set; }

    /// <summary>
    /// The date and time when the schedule was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The start time for the schedule.
    /// </summary>
    public DateTimeOffset StartAt { get; set; }

    /// <summary>
    /// The next scheduled run time.
    /// </summary>
    public DateTimeOffset NextRun { get; set; }

    /// <summary>
    /// The last time the schedule was executed.
    /// </summary>
    public DateTimeOffset? LastRun { get; set; }

    /// <summary>
    /// The repeat interval for the schedule.
    /// </summary>
    public RepeatInterval Interval { get; set; }

    /// <summary>
    /// Whether to run the script when a device connects if it missed the scheduled time.
    /// </summary>
    public bool RunOnNextConnect { get; set; }

    /// <summary>
    /// The list of device IDs to run the script on.
    /// </summary>
    public List<string> DeviceIds { get; set; } = new();

    /// <summary>
    /// The list of device group IDs to run the script on.
    /// </summary>
    public List<string> DeviceGroupIds { get; set; } = new();

    /// <summary>
    /// The name of the user who created the schedule.
    /// </summary>
    public string? CreatorName { get; set; }
}
