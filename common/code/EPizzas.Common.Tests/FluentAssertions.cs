using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using System;
using System.Linq;

namespace EPizzas.Common.Tests;

internal static class StringAssertionsExtensions
{
    public static AndConstraint<StringAssertions> BeGuid(this StringAssertions assertions, string because = "", params object[] becauseArgs)
    {
        var subject = assertions.Subject;
        var isGuid = Guid.TryParse(subject, out var _);

        Execute.Assertion
           .ForCondition(isGuid)
           .BecauseOf(because, becauseArgs)
           .FailWith("Expected {context:value} to be a GUID{reason}, but found {0}.", subject);

        return new AndConstraint<StringAssertions>(assertions);
    }

    public static AndConstraint<StringAssertions> BeWhiteSpace(this StringAssertions assertions, string because = "", params object[] becauseArgs)
    {
        var subject = assertions.Subject;
        var isWhiteSpace = subject is not null && string.IsNullOrWhiteSpace(subject);

        Execute.Assertion
           .ForCondition(isWhiteSpace)
           .BecauseOf(because, becauseArgs)
           .FailWith("Expected {context:value} to be whitespace{reason}, but found {0}.", subject);

        return new AndConstraint<StringAssertions>(assertions);
    }

    public static AndConstraint<StringAssertions> BeAlphaNumeric(this StringAssertions assertions, string because = "", params object[] becauseArgs)
    {
        var subject = assertions.Subject;
        var isAlphaNumeric = subject?.All(char.IsLetterOrDigit) == true;

        Execute.Assertion
           .ForCondition(isAlphaNumeric)
           .BecauseOf(because, becauseArgs)
           .FailWith("Expected all characters in {context:value} to be alphanumeric{reason}, but found {0}.", subject);

        return new AndConstraint<StringAssertions>(assertions);
    }
}