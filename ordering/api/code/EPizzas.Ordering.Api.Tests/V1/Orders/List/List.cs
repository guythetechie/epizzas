using EPizzas.Common;
using EPizzas.Ordering.Api.V1.Orders.List;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Flurl;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.Tests.V1.Orders.List;

public class ListTests
{
    [Property]
    public Property Invalid_continuation_token_fails()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, continuationToken) = x;
            fixture = fixture with
            {
                ListOrders = async (_, _) => await ValueTask.FromResult(new ListError.ContinuationTokenNotFound())
            };

            // Act
            using var response = await fixture.SendRequest(continuationToken, cancellationToken);

            // Assert
            response.Should().Be400BadRequest()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidContinuationToken");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Valid_request_succeeds()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, continuationToken) = x;

            // Act
            using var response = await fixture.SendRequest(continuationToken, cancellationToken);

            // Assert
            var continuationTokenOption = Prelude.Optional(continuationToken).Map(x => new ContinuationToken(x));
            var expectedResult = await fixture.ListOrders(continuationTokenOption, cancellationToken)
                                              .Map(either => either.IfLeft(_ => throw new InvalidOperationException()));

            var expectedBody = expectedResult.Resources
                                             .Map(x =>
                                             {
                                                 var (order, eTag) = x;

                                                 return new
                                                 {
                                                     pizzas = order.Pizzas.Map(pizza => new
                                                     {
                                                         size = pizza.Size.ToString(),
                                                         toppings = pizza.Toppings.Map(topping => new
                                                         {
                                                             type = topping.Type.ToString(),
                                                             amount = topping.Amount.ToString()
                                                         }),
                                                     }),
                                                     status = order.Status.ToString(),
                                                     eTag = eTag.Value
                                                 };
                                             });

            response.Should().Be200Ok()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        var actualValues = jsonObject.GetJsonObjectArrayProperty("value")
                                                     .Map(jsonObject => new
                                                     {
                                                         pizzas = jsonObject.GetJsonObjectArrayProperty("pizzas")
                                                                            .Map(pizzaJson => new
                                                                            {
                                                                                size = pizzaJson.GetStringProperty("size"),
                                                                                toppings = pizzaJson.GetJsonObjectArrayProperty("toppings")
                                                                                                    .Map(toppingJson => new
                                                                                                    {
                                                                                                        type = toppingJson.GetStringProperty("type"),
                                                                                                        amount = toppingJson.GetStringProperty("amount")
                                                                                                    })
                                                                            }),
                                                         status = jsonObject.GetStringProperty("status"),
                                                         eTag = jsonObject.GetStringProperty("eTag")
                                                     });
                        expectedBody.Should().BeEquivalentTo(actualValues);

                        expectedResult.ContinuationToken
                                      .Iter(token => jsonObject.Should().ContainKey("nextLink"));
                    });
        });
    }

    private static Gen<(Fixture Fixture, string? ContinuationToken)> GenerateValidRequest()
    {
        return from listResult in from orders in Gen.Zip(ModelGenerator.Order, CommonGenerator.ETag)
                                                    .SeqOf()
                                                    .DistinctBy(x => x.Item1.Id.Value)
                                  from continuationTokenOption in orders.Any() ? CommonGenerator.ContinuationToken.OptionOf() : Gen.Constant(Option<ContinuationToken>.None)
                                  select new ListResult
                                  {
                                      Resources = orders.Freeze(),
                                      ContinuationToken = continuationTokenOption
                                  }
               let fixture = new Fixture
               {
                   ListOrders = async (_, _) => await ValueTask.FromResult(listResult)
               }
               from continuationToken in CommonGenerator.ContinuationToken
                                                        .Select(x => x.Value)
                                                        .OrNull()
               select (fixture, continuationToken);

    }

    private sealed record Fixture
    {
        public required ListOrders ListOrders { get; init; }

        public async ValueTask<HttpResponseMessage> SendRequest(string? continuationToken, CancellationToken cancellationToken)
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var uri = new Uri($"/v1/orders", UriKind.Relative).SetQueryParam("continuationToken", continuationToken)
                                                              .ToUri();

            return await client.GetAsync(uri, cancellationToken);
        }

        private WebFactory CreateFactory()
        {
            return new WebFactory
            {
                ConfigureServices = services => services.AddSingleton(ListOrders)
            };
        }
    }
}