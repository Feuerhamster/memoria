using System.Security.Claims;
using Memoria.Models.Database;
using Microsoft.AspNetCore.Authorization;

namespace Memoria.Extensions;


public class TokenPermissionRequirement(EUserAppAccessTokenPermissions permissions) : IAuthorizationRequirement
{
    public EUserAppAccessTokenPermissions Permissions { get; } = permissions;
}

public class TokenPermissionHandler : AuthorizationHandler<TokenPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TokenPermissionRequirement requirement)
    {
        var permissions = context.User.GetTokenPermissions();

        if (permissions is null)
        {
            context.Fail(new AuthorizationFailureReason(this, "No token permissions claim found"));
            return Task.CompletedTask;
        }

        if (permissions.Value.HasFlag(requirement.Permissions))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(
                this,
                $"Missing permission: {requirement.Permissions}. " +
                $"Present: {permissions.Value}"));
        }

        return Task.CompletedTask;
    }
}