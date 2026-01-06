using Remotely.Shared.Enums;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for updating an existing script schedule.
/// All fields are optional - only provided fields will be updated.
/// </summary>
public class UpdateScriptScheduleDto
{
    /// <summary>
    /// The name of the schedule.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The ID of the saved script to execute.
    /// </summary>
    public Guid? SavedScriptId { get; set; }

    /// <summary>
    /// The start time for the schedule.
    /// </summary>
    public DateTimeOffset? StartAt { get; set; }

    /// <summary>
    /// The repeat interval for the schedule.
    /// </summary>
    public RepeatInterval? Interval { get; set; }

    /// <summary>
    /// The list of device IDs to run the script on.
    /// </summary>
    public List<string>? DeviceIds { get; set; }

    /// <summary>
    /// The list of device group IDs to run the script on.
    /// </summary>
    public List<string>? DeviceGroupIds { get; set; }

    /// <summary>
    /// Whether to run the script when a device connects if it missed the scheduled time.
    /// </summary>
    public bool? RunOnNextConnect { get; set; }
}
