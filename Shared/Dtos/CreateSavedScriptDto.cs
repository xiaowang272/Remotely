using Remotely.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for creating a new saved script.
/// </summary>
public class CreateSavedScriptDto
{
    /// <summary>
    /// The name of the script.
    /// </summary>
    [Required(ErrorMessage = "Script name is required.")]
    [StringLength(100, ErrorMessage = "Script name cannot exceed 100 characters.")]
    public required string Name { get; set; }

    /// <summary>
    /// The content/code of the script.
    /// </summary>
    [Required(ErrorMessage = "Script content is required.")]
    public required string Content { get; set; }

    /// <summary>
    /// The shell type for executing the script.
    /// </summary>
    [Required(ErrorMessage = "Shell type is required.")]
    public ScriptingShell Shell { get; set; }

    /// <summary>
    /// Optional folder path for organizing scripts.
    /// </summary>
    [StringLength(200, ErrorMessage = "Folder path cannot exceed 200 characters.")]
    public string? FolderPath { get; set; }

    /// <summary>
    /// Whether the script is public to all users in the organization.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Whether the script is a quick script for easy access.
    /// </summary>
    public bool IsQuickScript { get; set; }

    /// <summary>
    /// Whether to generate an alert when the script execution has errors.
    /// </summary>
    public bool GenerateAlertOnError { get; set; }

    /// <summary>
    /// Whether to send an email when the script execution has errors.
    /// </summary>
    public bool SendEmailOnError { get; set; }

    /// <summary>
    /// The email address to send error notifications to.
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? SendErrorEmailTo { get; set; }
}
