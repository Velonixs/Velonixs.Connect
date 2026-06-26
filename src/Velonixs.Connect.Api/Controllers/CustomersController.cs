using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/customers")]
public sealed class CustomersController(
    ICustomerService customerService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] Guid? restaurantId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var effectiveRestaurantId = restaurantId ?? access.AssignedBusinessId;

        if (effectiveRestaurantId is not Guid businessId)
        {
            return BadRequest(new ApiErrorResponse(
                "restaurant_required",
                "A restaurantId query value is required for platform administrators.",
                HttpContext.TraceIdentifier));
        }

        if (!access.CanReadBusiness(businessId))
        {
            return Forbid();
        }

        return Ok(await customerService.GetRestaurantCustomersAsync(
            businessId,
            take,
            access.IsPlatformAdmin,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageCustomerAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var customer = await customerService.UpdateCustomerAsync(id, request, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }
}
