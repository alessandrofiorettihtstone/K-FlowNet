using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using KFlow.Class;
using KFlow.Models;

namespace KFlow.Services;

public class KFlowAuthService
{
    private readonly dbContext _db;

    public KFlowAuthService(dbContext db)
    {
        _db = db;
    }

    public Task<User?> FindByCredentialsAsync(string username, string password)
    {
        var normalizedUsername = username.Trim();
        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername && x.Password == password);
    }

    public Task<User?> FindByGuidAsync(Guid userGuid)
    {
        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == userGuid);
    }

    public ClaimsPrincipal CreatePrincipal(User user, bool isMobileSession = false)
    {
        var roleValue = AuthConfiguration.NormalizeRole(user.Role);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Guid.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, roleValue),
            new("IDUser", user.IDUser.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.Name));
        }

        if (!string.IsNullOrWhiteSpace(user.Surname))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.Surname));
        }

        if (!string.IsNullOrWhiteSpace(user.Department))
        {
            claims.Add(new Claim("Department", user.Department));
        }

        if (isMobileSession)
        {
            claims.Add(new Claim(AuthConfiguration.AccessChannelClaimType, AuthConfiguration.MobileAccessChannel));
        }

        var identity = new ClaimsIdentity(
            claims,
            AuthConfiguration.CookieScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    public static Guid? ReadLegacyLoginCookie(string? rawCookieValue)
    {
        if (string.IsNullOrWhiteSpace(rawCookieValue))
        {
            return null;
        }

        if (Guid.TryParse(rawCookieValue, out var directGuid))
        {
            return directGuid;
        }

        foreach (var part in rawCookieValue.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2
                && string.Equals(pair[0], "guid", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(Uri.UnescapeDataString(pair[1]), out var guid))
            {
                return guid;
            }
        }

        return null;
    }
}
