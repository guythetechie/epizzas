using common;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using LanguageExt;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;

namespace api.integration.tests;

internal sealed class HttpResponseMessageAssertions(HttpResponseMessage subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<HttpResponseMessage, HttpResponseMessageAssertions>(subject, assertionChain)
{
    private readonly AssertionChain assertionChain = assertionChain;
    protected override string Identifier { get; } = "HTTP response message";


    public AndConstraint<HttpResponseMessageAssertions> BeSuccessful([StringSyntax("CompositeFormat")] string because = "",
                                                                     params object[] becauseArgs)
    {
        assertionChain.BecauseOf(because, becauseArgs);

        switch (Subject)
        {
            case { IsSuccessStatusCode: true }:
                return new AndConstraint<HttpResponseMessageAssertions>(this);
            default:
                assertionChain.FailWith("Expected {context:HTTP response message} to be successful, but its status code was {0}.",
                                        Subject.StatusCode);
                return new AndConstraint<HttpResponseMessageAssertions>(this);
        }
    }
}

internal sealed class OptionAssertions<T>(Option<T> subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<Option<T>, OptionAssertions<T>>(subject, assertionChain)
{
    private readonly AssertionChain assertionChain = assertionChain;

    protected override string Identifier { get; } = "option";

    public AndWhichConstraint<OptionAssertions<T>, T> BeSome([StringSyntax("CompositeFormat")] string because = "",
                                                             params object[] becauseArgs)
    {
        assertionChain.BecauseOf(because, becauseArgs);

        return Subject.Match(value => new AndWhichConstraint<OptionAssertions<T>, T>(this, value),
                             () =>
                             {
                                 assertionChain.FailWith("Expected {context:option} to be Some, but it is None.");
                                 return new AndWhichConstraint<OptionAssertions<T>, T>(this, []);
                             });
    }

    public AndWhichConstraint<OptionAssertions<T>, T> BeNone([StringSyntax("CompositeFormat")] string because = "",
                                                             params object[] becauseArgs)
    {
        assertionChain.BecauseOf(because, becauseArgs);

        return Subject.Match(value =>
        {
            assertionChain.FailWith("Expected {context:option} to be None, but it is Some {0}.", value);
            return new AndWhichConstraint<OptionAssertions<T>, T>(this, []);
        },
                             () => new AndWhichConstraint<OptionAssertions<T>, T>(this, []));
    }
}

internal sealed class JsonResultAssertions<T>(JsonResult<T> subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<JsonResult<T>, JsonResultAssertions<T>>(subject, assertionChain)
{
    private readonly AssertionChain assertionChain = assertionChain;

    protected override string Identifier { get; } = "JSON result";

    public AndWhichConstraint<JsonResultAssertions<T>, T> BeSuccess([StringSyntax("CompositeFormat")] string because = "",
                                                                    params object[] becauseArgs)
    {
        assertionChain.BecauseOf(because, becauseArgs);

        return Subject.Match(success => new AndWhichConstraint<JsonResultAssertions<T>, T>(this, success),
                             error =>
                             {
                                 assertionChain.FailWith("Expected {context:JSON result} to be a success, but it failed with error {0}.", error);
                                 return new AndWhichConstraint<JsonResultAssertions<T>, T>(this, []);
                             });
    }

    public AndWhichConstraint<JsonResultAssertions<T>, JsonError> BeError([StringSyntax("CompositeFormat")] string because = "",
                                                                          params object[] becauseArgs)
    {
        assertionChain.BecauseOf(because, becauseArgs);

        return Subject.Match(success =>
        {
            assertionChain.FailWith("Expected {context:JSON result} to be an error, but it succeeded with value {0}.", success);
            return new AndWhichConstraint<JsonResultAssertions<T>, JsonError>(this, []);
        },
                            error => new AndWhichConstraint<JsonResultAssertions<T>, JsonError>(this, error));
    }
}

internal static class AssertionExtensions
{
    public static JsonResultAssertions<T> Should<T>(this JsonResult<T> subject) =>
        new(subject, AssertionChain.GetOrCreate());

    public static OptionAssertions<T> Should<T>(this Option<T> subject) =>
        new(subject, AssertionChain.GetOrCreate());

    public static HttpResponseMessageAssertions Should(this HttpResponseMessage subject) =>
        new(subject, AssertionChain.GetOrCreate());
}