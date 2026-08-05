using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Velonixs.Connect.Api.Controllers;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Shared.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MetaCatalogSyncControllerAuthorizationTests
{
    [Theory]
    [InlineData(AppRoles.PlatformAdmin, true)]
    [InlineData(AppRoles.BusinessOwner, false)]
    [InlineData(AppRoles.BusinessManager, false)]
    public void CanManageMetaCatalog_RequiresPlatformAdminEvenForTheAssignedBusiness(
        string role,
        bool expected)
    {
        using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var businessId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, role),
            new Claim(AppClaimTypes.BusinessId, businessId.ToString())
        ],
        authenticationType: "test"));
        var access = new ApiBusinessAccessService(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = principal }
            },
            dbContext);

        Assert.Equal(expected, access.CanManageMetaCatalog(businessId));
    }

    [Fact]
    public void CatalogControlEndpoints_DeclarePlatformAdminAuthorizationAndRemainBusinessScoped()
    {
        var controllerType = typeof(MetaCatalogSyncController);
        var controllerAuthorization = controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        Assert.Equal(AppRoles.PlatformAdmin, controllerAuthorization.Roles);

        var controlActions = new[]
        {
            nameof(MetaCatalogSyncController.GetSettings),
            nameof(MetaCatalogSyncController.SaveSettings),
            nameof(MetaCatalogSyncController.GetQueue),
            nameof(MetaCatalogSyncController.GetLogs),
            nameof(MetaCatalogSyncController.SyncAll),
            nameof(MetaCatalogSyncController.Retry)
        };

        foreach (var actionName in controlActions)
        {
            var action = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(action);
            Assert.Contains(action!.GetParameters(), parameter => parameter.Name == "businessId");

            var route = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Single().Template;
            Assert.Contains("{businessId:guid}", route);
        }
    }
}
