using Remotely.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for creating a new script schedule.
/// </summary>
public class CreateScriptScheduleDto
{
    /// <summary>
    /// The name of the schedule.
    /// </summary>
    [Required(ErrorMessage = "Schedule name is required.")]
    public required string Name { get; set; }

    /// <summary>
    /// The ID of the saved script to execute.
    /// </summary>
    [Required(ErrorMessage = "SavedScriptId is required.")]
    public Guid SavedScriptId { get; set; }

    /// <summary>
    /// The start time for the schedule.
    /// </summary>
    [Required(ErrorMessage = "StartAt is required.")]
    public DateTimeOffset StartAt { get; set; }

    /// <summary>
    /// The repeat interval for the schedule.
    /// </summary>
    [Required(ErrorMessage = "Interval is required.")]
    public RepeatInterval Interval { get; set; }

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
    public bool RunOnNextConnect { get; set; } = true;
}
