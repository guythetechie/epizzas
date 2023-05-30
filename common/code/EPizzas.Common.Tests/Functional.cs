using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit;

namespace EPizzas.Common.Tests;

public class OptionExtensionsTests
{
    [Fact]
    public void IfNoneThrow_returns_value_if_option_is_Some()
    {
        // Arrange
        var value = Generator.GenerateDefault<string>().Sample();
        var option = Option<string>.Some(value);
        var errorMessage = Generator.NonEmptyOrWhiteSpaceString.Sample();

        // Act
        var result = option.IfNoneThrow(errorMessage);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void IfNoneThrow_throws_if_option_is_None()
    {
        // Arrange
        var option = Option<int>.None;
        var errorMessage = Generator.NonEmptyOrWhiteSpaceString.Sample();

        // Act
        var action = () => option.IfNoneThrow(errorMessage);

        // Assert
        action.Should().Throw<Exception>().WithMessage(errorMessage);
    }
}

public class IAsyncEnumerableExtensionsTests
{
    [Property]
    public Property ToSeq_enumerates_enumerable()
    {
        var arbitrary = Generator.GenerateDefault<string>()
                                 .ListOf()
                                 .ToArbitrary();

        return Prop.ForAll(arbitrary, async list =>
        {
            // Arrange
            var asyncEnumerable = list.ToAsyncEnumerable();

            // Act
            var seq = await asyncEnumerable.ToSeq(CancellationToken.None);

            // Assert
            seq.Should().BeEquivalentTo(list, options => options.WithStrictOrdering());
        });
    }
}

public class IDictionaryExtensionsTests
{
    [Fact]
    public void Find_returns_Some_with_value_if_key_exists()
    {
        // Arrange
        var (key, value) = ("key", 1);
        var dictionary = CreateDictionary(key, value);

        // Act
        var option = dictionary.Find(key);

        // Assert
        option.Should().Be(value);
    }

    [Fact]
    public void Find_returns_None_if_key_does_not_exist()
    {
        // Arrange
        var dictionary = ImmutableDictionary<string, int>.Empty;

        // Act
        var option = dictionary.Find("nonExistingKey");

        // Assert
        option.Should().BeNone();
    }

    private static ImmutableDictionary<TKey, TValue> CreateDictionary<TKey, TValue>(TKey key, TValue value) where TKey : notnull
    {
        return ImmutableDictionary<TKey, TValue>.Empty.Add(key, value);
    }
}