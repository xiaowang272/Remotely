using System.ComponentModel.DataAnnotations;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for executing a saved script on multiple devices.
/// </summary>
public class ExecuteSavedScriptDto
{
    /// <summary>
    /// The ID of the saved script to execute.
    /// </summary>
    [Required(ErrorMessage = "SavedScriptId is required.")]
    public Guid SavedScriptId { get; set; }

    /// <summary>
    /// The list of device IDs to execute the script on.
    /// </summary>
    [Required(ErrorMessage = "DeviceIds is required.")]
    [MinLength(1, ErrorMessage = "At least one device ID is required.")]
    public List<string> DeviceIds { get; set; } = new();
}
