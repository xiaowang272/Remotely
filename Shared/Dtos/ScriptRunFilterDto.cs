namespace Remotely.Shared.Dtos;

/// <summary>
/// DTO for filtering script run queries.
/// </summary>
public class ScriptRunFilterDto
{
    /// <summary>
    /// Filter by saved script ID.
    /// </summary>
    public Guid? SavedScriptId { get; set; }

    /// <summary>
    /// Filter by device ID.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Filter by start date (inclusive).
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Filter by end date (inclusive).
    /// </summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// The page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
