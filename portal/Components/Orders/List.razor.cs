using common;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace portal.Components.Orders;

public partial class List(ListOrders listOrders) : ComponentBase
{
    private FrozenDictionary<OrderId, (Order, ETag)> orders = FrozenDictionary<OrderId, (Order, ETag)>.Empty;
    private GridItemsProvider<GridItem>? itemsProvider;
    private bool gridIsLoading;

    protected override void OnInitialized()
    {
        itemsProvider = GetProvider;
    }

    private async ValueTask<GridItemsProviderResult<GridItem>> GetProvider(GridItemsProviderRequest<GridItem> request)
    {
        gridIsLoading = true;
        StateHasChanged();
        await PopulateOrders(request.CancellationToken);

        var items = orders.Values.Select(x => GridItem.From(x.Item1));
        var count = orders.Count;
        gridIsLoading = false;
        StateHasChanged();

        return GridItemsProviderResult.From([.. items], count);
    }

    private async ValueTask PopulateOrders(CancellationToken cancellationToken)
    {
        var ordersDictionary = await listOrders(cancellationToken).ToDictionaryAsync(x => x.Order.Id, cancellationToken);

        orders = ordersDictionary.ToFrozenDictionary();
    }

    private sealed record GridItem
    {
        public required string Id { get; init; }
        public required string Status { get; init; }
        public required string LastModifiedBy { get; init; }
        public required DateTimeOffset LastModifiedOn { get; init; }

        public static GridItem From(Order order)
        {
            var id = order.Id.ToString();
            var (status, lastModifiedBy, lastModifiedOn) = order.Status switch
            {
                OrderStatus.Created created =>
                    (nameof(OrderStatus.Created), created.By, created.Date),
                OrderStatus.Cancelled cancelled =>
                    (nameof(OrderStatus.Cancelled), cancelled.By, cancelled.Date),
                _ => throw new NotImplementedException()
            };

            return new GridItem
            {
                Id = id,
                Status = status,
                LastModifiedBy = lastModifiedBy,
                LastModifiedOn = lastModifiedOn
            };
        }
    }
}