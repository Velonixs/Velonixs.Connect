using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Route("api/businesses")]
public sealed class BusinessesController(IBusinessService businessService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBusinesses(CancellationToken cancellationToken)
    {
        return Ok(await businessService.GetBusinessesAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBusiness(Guid id, CancellationToken cancellationToken)
    {
        var business = await businessService.GetBusinessAsync(id, cancellationToken);
        return business is null ? NotFound() : Ok(business);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBusiness(
        [FromBody] CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = await businessService.CreateBusinessAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBusiness), new { id = business.Id }, business);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBusiness(
        Guid id,
        [FromBody] UpdateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = await businessService.UpdateBusinessAsync(id, request, cancellationToken);
        return business is null ? NotFound() : Ok(business);
    }
}
