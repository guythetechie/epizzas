using EPizzas.Common;
using EPizzas.Ordering.Api.V1.Orders.Put;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.Tests.V1.Orders.Cancel;

public class PutTests
{
    [Property]
    public Property Invalid_request_body_fails()
    {
        var generator = from request in GenerateValidRequest()
                        from json in Generator.JsonNode.OrNull()
                        select (request.OrderId, json, request.IfMatchHeader, request.IfNoneMatchHeader, request.Fixture);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().HaveStatusCode(HttpStatusCode.BadRequest)
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidJsonBody");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Missing_conditional_headers_fail()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, _, _, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, default, default, requestJson, cancellationToken);

            // Assert
            response.Should().HaveStatusCode(HttpStatusCode.PreconditionRequired)
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidConditionalHeader");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Specifying_both_If_Match_and_If_None_Match_headers_fails()
    {
        var generator = from request in GenerateValidRequest()
                        from ifMatchHeader in CommonGenerator.ETag
                        from ifNoneMatchHeader in CommonGenerator.ETag
                        select (request.OrderId, request.RequestJson, new StringValues(ifMatchHeader.Value), new StringValues(ifNoneMatchHeader.Value), request.Fixture);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().Be400BadRequest()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidConditionalHeader");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Specifying_non_wildcard_If_None_Match_header_fails()
    {
        var generator = from request in GenerateValidCreateRequest()
                        from ifNoneMatchHeader in CommonGenerator.ETag.Where(x => x.Value != ETag.All.Value)
                        select (request.OrderId, request.RequestJson, request.IfMatchHeader, new StringValues(ifNoneMatchHeader.Value), request.Fixture);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().Be400BadRequest()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidConditionalHeader");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Specifying_multiple_If_Match_headers_fails()
    {
        var generator = from request in GenerateValidCreateRequest()
                        from ifMatchHeader in CommonGenerator.ETag
                                                             .ArrayOf()
                                                             .Where(x => x.Length > 1)
                                                             .Select(x => x.Map(eTag => eTag.Value).ToArray())
                                                             .Select(x => new StringValues(x))
                        select (request.OrderId, request.RequestJson, ifMatchHeader, request.IfNoneMatchHeader, request.Fixture);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().Be400BadRequest()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be("InvalidConditionalHeader");
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Valid_update_request_succeeds()
    {
        var arbitrary = GenerateValidUpdateRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().Be200Ok()
                    .And
                    .Satisfy<JsonObject>(responseJson =>
                    {
                        var responseJsonObject = responseJson.TryAsJsonObject()
                                                             .IfNoneThrow("Response should be a JSON object.");
                        responseJsonObject.Should().ContainKey("status");
                        responseJsonObject.Should().ContainKey("eTag");

                        var requestJsonObject = requestJson.TryAsJsonObject()
                                                           .IfNoneThrow("Request should be a JSON object.");
                        var requestPizzas = requestJsonObject.GetJsonObjectArrayProperty("pizzas")
                                                             .Map(pizzaJson => new
                                                             {
                                                                 size = pizzaJson.GetStringProperty("size"),
                                                                 toppings = pizzaJson.GetJsonObjectArrayProperty("toppings")
                                                                                     .Map(toppingJson => new
                                                                                     {
                                                                                         type = toppingJson.GetStringProperty("type"),
                                                                                         amount = toppingJson.GetStringProperty("amount")
                                                                                     })
                                                             });
                        var responsePizzas = responseJsonObject.GetJsonObjectArrayProperty("pizzas")
                                                               .Map(pizzaJson => new
                                                               {
                                                                   size = pizzaJson.GetStringProperty("size"),
                                                                   toppings = pizzaJson.GetJsonObjectArrayProperty("toppings")
                                                                                    .Map(toppingJson => new
                                                                                    {
                                                                                        type = toppingJson.GetStringProperty("type"),
                                                                                        amount = toppingJson.GetStringProperty("amount")
                                                                                    })
                                                               });
                        requestPizzas.Should().BeEquivalentTo(responsePizzas);
                    });
        });
    }

    [Property]
    public Property Valid_create_request_succeeds()
    {
        var arbitrary = GenerateValidCreateRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (orderId, requestJson, ifMatchHeader, ifNoneMatchHeader, fixture) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, ifNoneMatchHeader, requestJson, cancellationToken);

            // Assert
            response.Should().Be201Created()
                    .And
                    .Satisfy<JsonObject>(responseJson =>
                    {
                        var responseJsonObject = responseJson.TryAsJsonObject()
                                                             .IfNoneThrow("Response should be a JSON object.");
                        responseJsonObject.Should().ContainKey("status");
                        responseJsonObject.Should().ContainKey("eTag");

                        var requestJsonObject = requestJson.TryAsJsonObject()
                                                           .IfNoneThrow("Request should be a JSON object.");
                        var requestPizzas = requestJsonObject.GetJsonObjectArrayProperty("pizzas")
                                                             .Map(pizzaJson => new
                                                             {
                                                                 size = pizzaJson.GetStringProperty("size"),
                                                                 toppings = pizzaJson.GetJsonObjectArrayProperty("toppings")
                                                                                     .Map(toppingJson => new
                                                                                     {
                                                                                         type = toppingJson.GetStringProperty("type"),
                                                                                         amount = toppingJson.GetStringProperty("amount")
                                                                                     })
                                                             });
                        var responsePizzas = responseJsonObject.GetJsonObjectArrayProperty("pizzas")
                                                               .Map(pizzaJson => new
                                                               {
                                                                   size = pizzaJson.GetStringProperty("size"),
                                                                   toppings = pizzaJson.GetJsonObjectArrayProperty("toppings")
                                                                                    .Map(toppingJson => new
                                                                                    {
                                                                                        type = toppingJson.GetStringProperty("type"),
                                                                                        amount = toppingJson.GetStringProperty("amount")
                                                                                    })
                                                               });
                        requestPizzas.Should().BeEquivalentTo(responsePizzas);
                    })
                    .And
                    .HaveHeader("Location");
        });
    }

    private static Gen<(string OrderId, JsonNode? RequestJson, StringValues IfMatchHeader, StringValues IfNoneMatchHeader, Fixture Fixture)> GenerateValidRequest()
    {
        return Gen.OneOf(GenerateValidCreateRequest(), GenerateValidUpdateRequest());
    }

    private static Gen<(string OrderId, JsonNode? RequestJson, StringValues IfMatchHeader, StringValues IfNoneMatchHeader, Fixture Fixture)> GenerateValidCreateRequest()
    {
        return from order in ModelGenerator.Order
               let eTag = ETag.All
               let requestJson = new JsonObject
               {
                   ["pizzas"] = order.Pizzas
                                     .Map(pizza => JsonSerializer.SerializeToNode(pizza))
                                     .ToJsonArray()
               }
               from newETag in CommonGenerator.ETag
               let fixture = new Fixture
               {
                   CreateOrder = async (order, cancellationToken) => await ValueTask.FromResult(newETag),
                   FindOrder = (_, _) => throw new NotImplementedException(),
                   UpdateOrder = (_, _, _) => throw new NotImplementedException()
               }
               select (order.Id.Value, requestJson as JsonNode, StringValues.Empty, new StringValues("*"), fixture);
    }

    private static Gen<(string OrderId, JsonNode? RequestJson, StringValues IfMatchHeader, StringValues IfNoneMatchHeader, Fixture Fixture)> GenerateValidUpdateRequest()
    {
        return from originalOrder in ModelGenerator.Order
               from originalETag in CommonGenerator.ETag
               from newOrder in ModelGenerator.Order
               let requestJson = new JsonObject
               {
                   ["pizzas"] = newOrder.Pizzas
                                        .Map(pizza => JsonSerializer.SerializeToNode(pizza))
                                        .ToJsonArray()
               }
               from newETag in CommonGenerator.ETag
               let fixture = new Fixture
               {
                   CreateOrder = (_, _) => throw new NotImplementedException(),
                   FindOrder = async (_, _) => await ValueTask.FromResult(originalOrder),
                   UpdateOrder = async (_, _, _) => await ValueTask.FromResult(newETag)
               }
               select (originalOrder.Id.Value, requestJson as JsonNode, new StringValues(originalETag.Value), StringValues.Empty, fixture);
    }

    private sealed record Fixture
    {
        public required FindOrder FindOrder { get; init; }
        public required CreateOrder CreateOrder { get; init; }
        public required UpdateOrder UpdateOrder { get; init; }

        public async ValueTask<HttpResponseMessage> SendRequest(string orderId, StringValues ifMatchHeader, StringValues ifNoneMatchHeader, JsonNode? json, CancellationToken cancellationToken)
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            ifMatchHeader.Iter(header =>
            {
                var headerValue = EntityTagHeaderValue.Parse(new ETag(header!).Value);
                client.DefaultRequestHeaders.IfMatch.Add(headerValue);
            });

            ifNoneMatchHeader.Iter(header =>
            {
                var headerValue = EntityTagHeaderValue.Parse(new ETag(header!).Value);
                client.DefaultRequestHeaders.IfNoneMatch.Add(headerValue);
            });

            var uri = new Uri($"/v1/orders/{orderId}", UriKind.Relative);
            using var content = JsonContent.Create(json);

            return await client.PutAsync(uri, content, cancellationToken);
        }

        private WebFactory CreateFactory()
        {
            return new WebFactory
            {
                ConfigureServices = services => services.AddSingleton(FindOrder)
                                                        .AddSingleton(CreateOrder)
                                                        .AddSingleton(UpdateOrder)
            };
        }
    }
}