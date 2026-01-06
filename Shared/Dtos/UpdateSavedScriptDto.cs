using Remotely.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for updating an existing saved script.
/// All fields are optional - only provided fields will be updated.
/// </summary>
public class UpdateSavedScriptDto
{
    /// <summary>
    /// The name of the script.
    /// </summary>
    [StringLength(100, ErrorMessage = "Script name cannot exceed 100 characters.")]
    public string? Name { get; set; }

    /// <summary>
    /// The content/code of the script.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The shell type for executing the script.
    /// </summary>
    public ScriptingShell? Shell { get; set; }

    /// <summary>
    /// Optional folder path for organizing scripts.
    /// </summary>
    [StringLength(200, ErrorMessage = "Folder path cannot exceed 200 characters.")]
    public string? FolderPath { get; set; }

    /// <summary>
    /// Whether the script is public to all users in the organization.
    /// </summary>
    public bool? IsPublic { get; set; }

    /// <summary>
    /// Whether the script is a quick script for easy access.
    /// </summary>
    public bool? IsQuickScript { get; set; }

    /// <summary>
    /// Whether to generate an alert when the script execution has errors.
    /// </summary>
    public bool? GenerateAlertOnError { get; set; }

    /// <summary>
    /// Whether to send an email when the script execution has errors.
    /// </summary>
    public bool? SendEmailOnError { get; set; }

    /// <summary>
    /// The email address to send error notifications to.
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? SendErrorEmailTo { get; set; }
}
