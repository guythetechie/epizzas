using EPizzas.Common;
using Flurl;
using LanguageExt;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.V1.Orders.List;

public sealed record ListResult
{
    public required IReadOnlyList<(Order Order, ETag ETag)> Resources { get; init; }
    public Option<ContinuationToken> ContinuationToken { get; init; } = Option<ContinuationToken>.None;
}


public abstract record ListError
{
    public sealed record ContinuationTokenNotFound : ListError;
}

public delegate ValueTask<Either<ListError, ListResult>> ListOrders(Option<ContinuationToken> continuationToken, CancellationToken cancellationToken);

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/", Handler.Handle);
    }
}

internal static class Handler
{
    public static async ValueTask<IResult> Handle(HttpRequest request, string? continuationToken, [FromServices] ListOrders listOrders, CancellationToken cancellationToken)
    {
        var continuationTokenOption = Prelude.Optional(continuationToken)
                                             .Where(token => string.IsNullOrWhiteSpace(token) is false)
                                             .Map(token => new ContinuationToken(token));

        var result = await listOrders(continuationTokenOption, cancellationToken);

        return result.Match(listResult => GetSuccessfulResponse(listResult, request.GetUri()),
                            GetErrorResponse);
    }

    private static IResult GetSuccessfulResponse(ListResult listResult, Uri requestUri)
    {
        var json = new JsonObject
        {
            ["value"] = listResult.Resources
                                  .Map(x =>
                                  {
                                      var (order, eTag) = x;
                                      var orderJson = Order.Converter.Serialize(order);
                                      orderJson.Add("eTag", eTag.Value);
                                      orderJson.Remove("id");

                                      return orderJson;
                                  })
                                  .ToJsonArray()
        };

        listResult.ContinuationToken
                  .Iter(continuationToken =>
                  {
                      var formattedUri = requestUri.RemoveQuery()
                                                   .SetQueryParam("continuationToken", continuationToken.Value);

                      json.AddProperty("nextLink", formattedUri.ToString());
                  });

        return TypedResults.Ok(json);
    }

    private static IResult GetErrorResponse(ListError error)
    {
        return error switch
        {
            ListError.ContinuationTokenNotFound => TypedResults.BadRequest(new
            {
                code = new ErrorCode.InvalidContinuationToken().ToString(),
                message = "Continuation token was not found on server."
            }),
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