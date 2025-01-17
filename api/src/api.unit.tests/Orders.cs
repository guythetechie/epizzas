using common;
using CsCheck;
using FluentAssertions;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace api.unit.tests;

public class CancelOrderTests
{
    [Fact]
    public async ValueTask Empty_order_id_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        from orderId in Generator.EmptyOrWhitespaceString
                        select fixture with
                        {
                            OrderId = orderId
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async ValueTask Empty_or_missing_If_Match_header_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        from ifMatch in Generator.EmptyOrWhitespaceString.Null()
                        select fixture with
                        {
                            IfMatch = ifMatch
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async ValueTask Non_existing_order_succeeds()
    {
        var generator = from fixture in Fixture.Generate()
                        select fixture with
                        {
                            CancelResult = CosmosError.NotFound.Instance
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        });
    }

    [Fact]
    public async ValueTask ETag_mismatch_should_fail()
    {
        var generator = from fixture in Fixture.Generate()
                        select fixture with
                        {
                            CancelResult = CosmosError.ETagMismatch.Instance
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.PreconditionFailed);
        });
    }

    [Fact]
    public async ValueTask ValidRequestSucceeds()
    {
        var generator = Fixture.Generate();

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeOfType<NoContent>();
        });
    }

    private sealed record Fixture
    {
        public required string OrderId { get; init; }
        public required string? IfMatch { get; init; }
        public required Either<CosmosError, Unit> CancelResult { get; init; }

        public async ValueTask<IResult> Run(CancellationToken cancellationToken) =>
            await OrderHandlers.Cancel(OrderId,
                                       (_, _, _) => ValueTask.FromResult(CancelResult),
                                       IfMatch,
                                       cancellationToken);

        public static Gen<Fixture> Generate() =>
            from orderId in Generator.OrderId
            from eTag in Generator.ETag
            select new Fixture
            {
                OrderId = orderId.ToString(),
                IfMatch = eTag.ToString(),
                CancelResult = Unit.Default
            };
    }
}

public class CreateOrderTests
{
    [Fact]
    public async ValueTask Invalid_body_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        from invalidBody in Generator.JsonNode.Null()
                        select fixture with
                        {
                            Body = invalidBody
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async ValueTask Already_existing_order_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        select fixture with
                        {
                            CreateResult = CosmosError.AlreadyExists.Instance
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        });
    }

    [Fact]
    public async ValueTask ValidRequestSucceeds()
    {
        var generator = Fixture.Generate();

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        });
    }

    private sealed record Fixture
    {
        public JsonNode? Body { get; init; }
        public required Either<CosmosError, Unit> CreateResult { get; init; }
        public DateTimeOffset Now { get; init; }

        public async ValueTask<IResult> Run(CancellationToken cancellationToken)
        {
            using var stream = Body switch
            {
                null => null,
                _ => JsonNodeModule.ToStream(Body)
            };

            return await OrderHandlers.Create((_, _) => ValueTask.FromResult(CreateResult),
                                              () => Now,
                                              stream,
                                              cancellationToken);
        }

        public static Gen<Fixture> Generate() =>
            from order in Generator.Order
            let orderJson = Order.Serialize(order)
            from now in Gen.DateTimeOffset
            select new Fixture
            {
                Body = orderJson,
                CreateResult = Unit.Default,
                Now = now
            };
    }
}

public class GetOrderByIdTests
{
    [Fact]
    public async ValueTask Empty_order_id_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        from orderId in Generator.EmptyOrWhitespaceString
                        select fixture with
                        {
                            OrderId = orderId
                        };
        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async ValueTask Missing_order_id_fails()
    {
        var generator = from fixture in Fixture.Generate()
                        select fixture with
                        {
                            Order = Option<(Order, ETag)>.None
                        };

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        });
    }

    [Fact]
    public async ValueTask ValidRequestSucceeds()
    {
        var generator = Fixture.Generate();

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.OK);

            result.Should().BeAssignableTo<IValueHttpResult>()
                  .Which.Value.Should().Satisfy<object?>(value =>
                  {
                      var returnedJson = JsonSerializer.SerializeToNode(value)
                                                       .AsJsonObject()
                                                       .ThrowIfFail();

                      returnedJson.Should().ContainKey("eTag");
                      returnedJson.Should().ContainKey("status");
                      returnedJson.Should().ContainKey("pizzas");
                  });
        });
    }

    private sealed record Fixture
    {
        public required string OrderId { get; init; }
        public required Option<(Order, ETag)> Order { get; init; }

        public async ValueTask<IResult> Run(CancellationToken cancellationToken) =>
            await OrderHandlers.GetById(OrderId,
                                        (_, _) => ValueTask.FromResult(Order),
                                        cancellationToken);

        public static Gen<Fixture> Generate() =>
            from order in Generator.Order
            from eTag in Generator.ETag
            from OrderId in Generator.OrderId
            select new Fixture
            {
                OrderId = OrderId.ToString(),
                Order = (order, eTag)
            };
    }
}

public class ListOrderTests
{
    [Fact]
    public async ValueTask ValidRequestSucceeds()
    {
        var generator = Fixture.Generate();

        await generator.SampleAsync(async fixture =>
        {
            var result = await fixture.Run(CancellationToken.None);

            result.Should().BeAssignableTo<IStatusCodeHttpResult>()
                  .Which.StatusCode.Should().Be((int)HttpStatusCode.OK);

            result.Should().BeAssignableTo<IValueHttpResult>()
                  .Which.Value.Should().Satisfy<object?>(value =>
                  {
                      var returnedJson = JsonSerializer.SerializeToNode(value)
                                                       .AsJsonObject()
                                                       .ThrowIfFail();

                      var returnedValues = returnedJson.GetJsonArrayProperty("values").ThrowIfFail();

                      returnedValues.Should().HaveSameCount(fixture.Orders);
                  });
        });
    }
    private sealed record Fixture
    {
        public required ImmutableArray<(Order, ETag)> Orders { get; init; }

        public async ValueTask<IResult> Run(CancellationToken cancellationToken) =>
            await OrderHandlers.List(_ => ValueTask.FromResult(Orders), cancellationToken);

        public static Gen<Fixture> Generate() =>
            from orders in Generator.Order
                                    .Select(Generator.ETag)
                                    .ImmutableArrayOf()
            select new Fixture
            {
                Orders = orders
            };
    }
}