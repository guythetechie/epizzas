using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.V1.Orders.Get;

public delegate ValueTask<Option<(Order Order, ETag ETag)>> FindOrder(OrderId orderId, CancellationToken cancellationToken);

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", Handler.Handle);
    }
}

internal static class Handler
{
    public static async ValueTask<IResult> Handle(string orderId, [FromServices] FindOrder findOrder, CancellationToken cancellationToken)
    {
        var result = from id in TryGetOrderId(orderId)
                     select FindOrder(id, findOrder, cancellationToken);

        return await result.Coalesce();
    }

    private static Either<IResult, OrderId> TryGetOrderId(string orderId)
    {
        return Common.TryGetOrderId(orderId)
                     .MapLeft(GetIdErrorToResult);
    }

    private static IResult GetIdErrorToResult(string error)
    {
        return TypedResults.BadRequest(new
        {
            code = nameof(ErrorCode.InvalidId),
            message = error
        });
    }

    private static async ValueTask<IResult> FindOrder(OrderId orderId, FindOrder findOrder, CancellationToken cancellationToken)
    {
        var option = await findOrder(orderId, cancellationToken);

        return option.Match(x => GetSuccessfulResponse(x.Order, x.ETag),
                            GetNotFoundResponse);
    }

    private static IResult GetSuccessfulResponse(Order order, ETag eTag)
    {
        var json = Serialization.Serialize(order);
        json.Add("eTag", eTag.Value);
        json.Remove("id");

        return TypedResults.Ok(json);
    }

    private static IResult GetNotFoundResponse()
    {
        return TypedResults.NotFound(new
        {
            code = nameof(ErrorCode.ResourceNotFound),
            message = "Order with ID was not found."
        });
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {

    }
}