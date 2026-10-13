using System.Security.Claims;

namespace Scheletro.Class;

public static class AuthConfiguration
{
    public const string CookieScheme = "CookieAuth";
    public const string AdminRole = "Admin";
    public const string DefaultUserRole = "User";
    public const string AdminPolicy = "AdminOnly";
    public const string NonAdminPolicy = "NonAdminOnly";
    public const string AccessChannelClaimType = "KFlowAccessChannel";
    public const string MobileAccessChannel = "Mobile";

    public static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return DefaultUserRole;
        }

        var normalizedRole = role.Trim();
        return IsAdminRole(normalizedRole) ? AdminRole : normalizedRole;
    }

    public static bool IsAdminRole(string? role) =>
        string.Equals(role?.Trim(), AdminRole, StringComparison.OrdinalIgnoreCase);

    public static bool HasAdminRole(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        user.Claims.Any(claim => claim.Type == ClaimTypes.Role && IsAdminRole(claim.Value));

    public static string GetRole(ClaimsPrincipal? user) =>
        NormalizeRole(user?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value);

    public static string GetDisplayName(ClaimsPrincipal? user)
    {
        var fullName = string.Join(
            " ",
            new[]
            {
                user?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value,
                user?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user?.Identity?.Name?.Trim() ?? "Utente";
    }

    public static string GetLandingPage(ClaimsPrincipal? user) =>
        GetLandingPage(GetRole(user));

    public static string GetLandingPage(string? role) =>
        "/Client/KanbanBoard.cshtml";
}
