using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using NUnit.Framework;

namespace EPizzas.Common.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class GeneratorTests
{
    [FsCheck.NUnit.Property()]
    public Property GuidString_is_a_GUID()
    {
        var arbitrary = Generator.GuidString.ToArbitrary();

        return Prop.ForAll(arbitrary, stringValue => stringValue.Should().BeGuid());
    }

    [FsCheck.NUnit.Property()]
    public Property WhiteSpaceString_is_whitespace()
    {
        var arbitrary = Generator.WhiteSpaceString.ToArbitrary();

        return Prop.ForAll(arbitrary, stringValue => stringValue.Should().BeWhiteSpace());
    }

    [FsCheck.NUnit.Property()]
    public Property AlphaNumericString_is_alphanumeric()
    {
        var arbitrary = Generator.AlphaNumericString.ToArbitrary();

        return Prop.ForAll(arbitrary, stringValue => stringValue.Should().BeAlphaNumeric());
    }

    [FsCheck.NUnit.Property()]
    public Property NonEmptyOrWhiteSpaceString_is_not_empty_or_whitespace()
    {
        var arbitrary = Generator.NonEmptyOrWhiteSpaceString.ToArbitrary();

        return Prop.ForAll(arbitrary, stringValue => stringValue.Should().NotBeNullOrWhiteSpace().And.NotBeEmpty());
    }
}
