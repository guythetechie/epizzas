using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using LanguageExt;
using NUnit.Framework;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace EPizzas.Common.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class OptionExtensionsTests
{
    [FsCheck.NUnit.Property()]
    public Property IfNoneThrow_returns_value_if_option_is_Some()
    {
        var arbitrary = Generator.GenerateDefault<object>()
                                 .Where(x => x is not null)
                                 .ToArbitrary();

        return Prop.ForAll(arbitrary, value =>
        {
            // Arrange
            var option = Option<object>.Some(value);

            // Act
            var result = option.IfNoneThrow(string.Empty);

            // Assert
            result.Should().Be(value);
        });
    }

    [FsCheck.NUnit.Property()]
    public Property IfNoneThrow_throws_if_option_is_None()
    {
        var arbitrary = Generator.AlphaNumericString.ToArbitrary();

        return Prop.ForAll(arbitrary, errorMessage =>
        {
            // Arrange
            var option = Option<int>.None;

            // Act
            var action = () => option.IfNoneThrow(errorMessage);

            // Assert
            action.Should().Throw<Exception>().WithMessage(errorMessage);
        });
    }
}

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class IAsyncEnumerableExtensionsTests
{
    [FsCheck.NUnit.Property()]
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

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class IDictionaryExtensionsTests
{
    [FsCheck.NUnit.Property()]
    public Property Find_returns_Some_with_value_if_key_exists()
    {
        var generator = from dictionary in GenerateDictionaryItems().Where(x => x.Count > 0)
                        from kvp in Gen.Elements(dictionary.AsEnumerable())
                        select (dictionary, kvp.Key, kvp.Value);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (dictionary, key, value) = x;

            // Act
            var option = dictionary.Find(key);

            // Assert
            option.Should().BeSome().Which.Should().Be(value);
        });
    }

    [FsCheck.NUnit.Property()]
    public Property Find_returns_None_if_key_does_not_exist()
    {
        var generator = from dictionary in GenerateDictionaryItems()
                        from nonExistingKey in Generator.GenerateDefault<string>().Where(key => dictionary.ContainsKey(key) is false)
                        select (dictionary, nonExistingKey);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (dictionary, nonExistingKey) = x;

            // Act
            var option = dictionary.Find(nonExistingKey);

            // Assert
            option.Should().BeNone();
        });
    }

    private static Gen<ImmutableDictionary<string, int>> GenerateDictionaryItems()
    {
        return Gen.Zip(Generator.AlphaNumericString, Generator.GenerateDefault<int>())
                  .SeqOf()
                  .DistinctBy(x => x.Item1)
                  .Select(x => x.ToImmutableDictionary(x => x.Item1, x => x.Item2));
    }
}