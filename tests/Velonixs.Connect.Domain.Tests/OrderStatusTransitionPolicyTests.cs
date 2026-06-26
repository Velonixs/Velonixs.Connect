using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Domain.Services;
using Xunit;

namespace Velonixs.Connect.Domain.Tests;

public sealed class OrderStatusTransitionPolicyTests
{
    [Theory]
    [InlineData(OrderStatuses.Confirmed)]
    [InlineData(OrderStatuses.Rejected)]
    [InlineData(OrderStatuses.Cancelled)]
    public void PendingConfirmation_AllowsExpectedNextStatuses(string nextStatus)
    {
        Assert.True(OrderStatusTransitionPolicy.CanTransition(
            OrderStatuses.PendingConfirmation,
            nextStatus));
    }

    [Fact]
    public void ConfirmedOrder_RequiresEstimatedMinutes()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrderStatusTransitionPolicy.ValidateTransition(
                OrderStatuses.PendingConfirmation,
                OrderStatuses.Confirmed,
                comment: null,
                estimatedMinutes: null));

        Assert.Equal("Estimated time is required when confirming an order.", exception.Message);
    }

    [Fact]
    public void RejectedOrder_RequiresReason()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrderStatusTransitionPolicy.ValidateTransition(
                OrderStatuses.PendingConfirmation,
                OrderStatuses.Rejected,
                comment: " ",
                estimatedMinutes: null));

        Assert.Equal("Rejection reason is required.", exception.Message);
    }

    [Fact]
    public void DeliveredOrder_CannotBeSetDirectlyFromPendingConfirmation()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            OrderStatusTransitionPolicy.ValidateTransition(
                OrderStatuses.PendingConfirmation,
                OrderStatuses.Delivered,
                comment: null,
                estimatedMinutes: null));

        Assert.Equal(
            "Order cannot move from PendingConfirmation to Delivered.",
            exception.Message);
    }

    [Fact]
    public void Normalize_ReturnsCanonicalStatusName()
    {
        Assert.Equal(
            OrderStatuses.ReadyForPickup,
            OrderStatusTransitionPolicy.Normalize("readyforpickup"));
    }
}
