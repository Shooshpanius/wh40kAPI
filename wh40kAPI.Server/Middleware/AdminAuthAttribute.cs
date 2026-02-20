using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace wh40kAPI.Server.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedHash = config["AdminAuth:PasswordHash"] ?? "";

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Admin-Password", out var passwordHeader))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedHash = ComputeSha256(passwordHeader.ToString());

        // Use constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedHash.ToLowerInvariant()),
                Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant())))
        {
            context.Result = new UnauthorizedResult();
            return;
        }
    }

    public static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
