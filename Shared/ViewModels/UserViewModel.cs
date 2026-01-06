namespace Remotely.Shared.ViewModels;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool IsAdministrator { get; set; }
    public bool IsServerAdmin { get; set; }
    public string? OrganizationID { get; set; }
}
