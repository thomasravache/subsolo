using System.Globalization;
using System.Security.Claims;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Modules.Users.Presentation.Features;

/// <summary>SPEC-0019 — o id do usuário da sessão validada (claim do cookie), ou nulo se ausente.</summary>
internal static class AccountUser
{
    public static Guid? Id(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
