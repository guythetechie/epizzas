using EPizzas.Common;
using EPizzas.Ordering.Api.V1.Orders.Cancel;
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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.Tests.V1.Orders.Cancel;

public class CancelTests
{
    [Property]
    public Property Missing_if_match_header_fails()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId, _) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, default, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.Should().HaveStatusCode(HttpStatusCode.PreconditionRequired)
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be(nameof(ErrorCode.InvalidConditionalHeader));
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Mismatching_if_match_header_fails()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId, ifMatchHeader) = x;

            fixture = fixture with
            {
                CancelOrder = async (_, _, _) => await ValueTask.FromResult(new CancelError.ETagMismatch())
            };

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, cancellationToken);

            // Assert
            response.Should().Be412PreconditionFailed()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be(nameof(ErrorCode.ETagMismatch));
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }

    [Property]
    public Property Specifying_multiple_If_Match_headers_fails()
    {
        var generator = from x in GenerateValidRequest()
                        from ifMatchHeader in CommonGenerator.ETag
                                                             .ArrayOf()
                                                             .Where(x => x.Length > 1)
                                                             .Select(x => x.Map(eTag => eTag.Value).ToArray())
                                                             .Select(x => new StringValues(x))
                        select (x.Fixture, x.Id, ifMatchHeader);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId, ifMatchHeader) = x;

            fixture = fixture with
            {
                CancelOrder = async (_, _, _) => await ValueTask.FromResult(new CancelError.ETagMismatch())
            };

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, cancellationToken);

            // Assert
            response.Should().Be400BadRequest()
                    .And
                    .Satisfy<JsonObject>(jsonObject =>
                    {
                        jsonObject.GetStringProperty("code").Should().Be(nameof(ErrorCode.InvalidConditionalHeader));
                        jsonObject.TryGetStringProperty("message").Should().BeRight();
                    });
        });
    }


    [Property]
    public Property Existing_order_returns_NoContent()
    {
        var arbitrary = GenerateValidRequest().ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var (fixture, orderId, ifMatchHeader) = x;

            // Act
            using var response = await fixture.SendRequest(orderId, ifMatchHeader, cancellationToken);

            // Assert
            response.Should().Be204NoContent();
        });
    }

    private static Gen<(Fixture Fixture, string Id, StringValues IfMatchHeader)> GenerateValidRequest()
    {
        return from fixture in GenerateFixture()
               from orderId in ModelGenerator.OrderId
               from eTag in CommonGenerator.ETag
               select (fixture, orderId.Value, new StringValues(eTag.Value));
    }

    private static Gen<Fixture> GenerateFixture()
    {
        return Gen.Constant(new Fixture
        {
            CancelOrder = async (orderId, eTag, cancellationToken) =>
            {
                await ValueTask.CompletedTask;
                return Prelude.unit;
            }
        });
    }

    private sealed record Fixture
    {
        public required CancelOrder CancelOrder { get; init; }

        public async ValueTask<HttpResponseMessage> SendRequest(string orderId, StringValues ifMatchHeader, CancellationToken cancellationToken)
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            ifMatchHeader.Iter(header =>
            {
                var headerValue = EntityTagHeaderValue.Parse(header!);
                client.DefaultRequestHeaders.IfMatch.Add(headerValue);
            });

            var uri = new Uri($"/v1/orders/{orderId}", UriKind.Relative);

            return await client.DeleteAsync(uri, cancellationToken);
        }

        private WebFactory CreateFactory()
        {
            return new WebFactory
            {
                ConfigureServices = services => services.AddSingleton(CancelOrder)
            };
        }
    }
}