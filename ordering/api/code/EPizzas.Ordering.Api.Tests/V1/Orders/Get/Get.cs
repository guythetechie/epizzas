using EPizzas.Common;
using EPizzas.Ordering.Api.V1.Orders;
using EPizzas.Ordering.Api.V1.Orders.Get;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.Tests.V1.Orders.Get;

public class GetTests
{
    [Property]
    public Property Missing_order_fails()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId) = x;
            fixture = fixture with
            {
                FindOrder = async (_, _) => await ValueTask.FromResult(Prelude.None)
            };

            // Act
            using var response = await fixture.SendRequest(orderId, cancellationToken);

            // Assert
            response.Should().Be404NotFound()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be(nameof(ErrorCode.ResourceNotFound));
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Existing_order_succeeds()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, cancellationToken);

            // Assert
            var (order, eTag) = await fixture.FindOrder(new(orderId), cancellationToken)
                                             .Map(option => option.Should().BeSome().Subject);

            response.Should().Be200Ok()
                    .And
                    .BeAs(new
                    {
                        pizzas = order.Pizzas.Map(pizza => new
                        {
                            size = Serialization.Serialize(pizza.Size).ToString(),
                            toppings = pizza.Toppings.Map(topping => new
                            {
                                type = Serialization.Serialize(topping.Type).ToString(),
                                amount = Serialization.Serialize(topping.Amount).ToString()
                            }),
                        }),
                        status = Serialization.Serialize(order.Status).ToString(),
                        eTag = eTag.Value
                    });
        });
    }

    private static Gen<(Fixture fixture, string OrderId)> GenerateValidRequest()
    {
        return from order in ModelGenerator.Order
               from eTag in CommonGenerator.ETag
               let fixture = new Fixture
               {
                   FindOrder = async (_, _) => await ValueTask.FromResult((order, eTag)),
               }
               select (fixture, order.Id.Value);
    }

    private sealed record Fixture
    {
        public required FindOrder FindOrder { get; init; }

        public async ValueTask<HttpResponseMessage> SendRequest(string orderId, CancellationToken cancellationToken)
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var uri = new Uri($"/v1/orders/{orderId}", UriKind.Relative);

            return await client.GetAsync(uri, cancellationToken);
        }

        private WebFactory CreateFactory()
        {
            return new WebFactory
            {
                ConfigureServices = services => services.AddSingleton(FindOrder)
            };
        }
    }
}