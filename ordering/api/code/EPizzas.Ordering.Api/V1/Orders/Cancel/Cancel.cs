using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.V1.Orders.Cancel;

public abstract record CancelError
{
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    public sealed record ETagMismatch : CancelError;
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
}

public delegate ValueTask<Either<CancelError, Unit>> CancelOrder(OrderId orderId, ETag eTag, CancellationToken cancellationToken);

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/{orderId}", Handler.Handle);
    }
}

internal static class Handler
{
    public static async ValueTask<IResult> Handle(string orderId, HttpRequest request, [FromServices] CancelOrder cancelOrder, CancellationToken cancellationToken)
    {
        request.Headers.TryGetValue("If-Match", out var ifMatchHeader);

        var result = from id in TryGetOrderId(orderId)
                     from ifMatchETag in TryGetIfMatchETag(ifMatchHeader)
                     select CancelOrder(id, ifMatchETag, cancelOrder, cancellationToken);

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

    private static Either<IResult, ETag> TryGetIfMatchETag(StringValues ifMatchHeader)
    {
        return ifMatchHeader.ToArray() switch
        {
            [] => TypedResults.Json(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "Must specify 'If-Match' header."
            }, statusCode: StatusCodes.Status428PreconditionRequired),
            [var ifMatch] when string.IsNullOrWhiteSpace(ifMatch) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'If-Match' header cannot be empty."
            }),
            [var ifMatch] => new ETag(ifMatch!),
            _ => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'Must specify exactly one 'If-Match' header."
            })
        };
    }

    private static async ValueTask<IResult> CancelOrder(OrderId orderId, ETag eTag, CancelOrder cancelOrder, CancellationToken cancellationToken)
    {
        var option = await cancelOrder(orderId, eTag, cancellationToken);

        return option.Match(_ => GetSuccessfulResponse(),
                            GetErrorResponse);
    }

    private static IResult GetSuccessfulResponse()
    {
        return TypedResults.NoContent();
    }

    private static IResult GetErrorResponse(CancelError cancelError)
    {
        return cancelError switch
        {
            CancelError.ETagMismatch => TypedResults.Json(new
            {
                code = nameof(ErrorCode.ETagMismatch),
                message = "The 'If-Match' header's eTag doesn't match the server's. Another request might have already updated the order."
            }, statusCode: StatusCodes.Status412PreconditionFailed),
            _ => throw new NotImplementedException()
        };
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {

    }
}