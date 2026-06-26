using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class CustomerService(RestaurantConnectDbContext dbContext) : ICustomerService
{
    public async Task<CustomerSummaryResponse?> GetCustomerAsync(
        Guid id,
        bool includeOrderCount = false,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CustomerSummaryResponse(
                x.Id,
                x.RestaurantId,
                x.PhoneNumber,
                x.Name,
                x.LastAddress,
                includeOrderCount ? dbContext.Orders.Count(order => order.CustomerId == x.Id) : 0,
                x.LastInteractionAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CustomerSummaryResponse>> GetRestaurantCustomersAsync(
        Guid restaurantId,
        int take = 100,
        bool includeOrderCount = false,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId)
            .OrderByDescending(x => x.LastInteractionAt)
            .Take(take)
            .Select(x => new CustomerSummaryResponse(
                x.Id,
                x.RestaurantId,
                x.PhoneNumber,
                x.Name,
                x.LastAddress,
                includeOrderCount ? dbContext.Orders.Count(order => order.CustomerId == x.Id) : 0,
                x.LastInteractionAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CustomerSummaryResponse?> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        customer.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        customer.LastAddress = string.IsNullOrWhiteSpace(request.LastAddress) ? null : request.LastAddress.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        var orderCount = await dbContext.Orders.CountAsync(x => x.CustomerId == customer.Id, cancellationToken);
        return new CustomerSummaryResponse(
            customer.Id,
            customer.RestaurantId,
            customer.PhoneNumber,
            customer.Name,
            customer.LastAddress,
            orderCount,
            customer.LastInteractionAt);
    }
}
