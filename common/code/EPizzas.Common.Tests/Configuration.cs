using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace EPizzas.Common.Tests;

public class ConfigurationTests
{
    [Property(DisplayName = "GetValue throws if the configuration key does not exist")]
    public Property GetValue_throws_if_configuration_key_does_not_exist()
    {
        var generator = from fixture in GenerateFixture()
                        from nonExistingKey in Generator.NonEmptyOrWhiteSpaceString
                                                        .Where(key => fixture.Items
                                                                             .Filter(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
                                                                             .HeadOrNone()
                                                                             .IsNone)
                        select (fixture, nonExistingKey);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (fixture, nonExistingKey) = x;
            var configuration = fixture.ToConfiguration();

            // Act
            var action = () => configuration.GetValue(nonExistingKey);

            // Assert
            action.Should().Throw<Exception>();
        });
    }

    [Property(DisplayName = "TryGetValue returns None if the configuration key does not exist")]
    public Property TryGetValue_returns_None_if_configuration_key_does_not_exist()
    {
        var generator = from fixture in GenerateFixture()
                        from nonExistingKey in Generator.NonEmptyOrWhiteSpaceString
                                                        .Where(key => fixture.Items
                                                                             .Filter(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
                                                                             .HeadOrNone()
                                                                             .IsNone)
                        select (fixture, nonExistingKey);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (fixture, nonExistingKey) = x;
            var configuration = fixture.ToConfiguration();

            // Act
            var result = configuration.TryGetValue(nonExistingKey);

            // Assert
            result.Should().BeNone();
        });
    }

    [Property(DisplayName = "TryGetValue returns None if the configuration value is null")]
    public Property TryGetValue_returns_None_if_configuration_value_is_null()
    {
        var generator = from fixture in GenerateFixture().Where(fixture => fixture.Items
                                                                                  .Find(kvp => kvp.Value is null)
                                                                                  .IsSome)
                        from keyWithNullValue in Gen.Elements(fixture.Items
                                                                     .Filter(kvp => kvp.Value is null)
                                                                     .Map(kvp => kvp.Key))
                        select (fixture, keyWithNullValue);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (fixture, keyWithNullValue) = x;
            var configuration = fixture.ToConfiguration();

            // Act
            var result = configuration.TryGetValue(keyWithNullValue);

            // Assert
            result.Should().BeNone();
        });
    }

    [Property(DisplayName = "TryGetValue returns the value if the configuration key has a non-null value")]
    public Property TryGetValue_returns_the_value_if_configuration_key_has_a_non_null_value()
    {
        var generator = from fixture in GenerateFixture().Where(fixture => fixture.Items
                                                                                  .Find(pair => pair.Value is not null)
                                                                                  .IsSome)
                        from existingKey in Gen.Elements(fixture.Items
                                                                .Filter(kvp => kvp.Value is not null)
                                                                .Map(kvp => kvp.Key))
                        select (fixture, existingKey);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, x =>
        {
            // Arrange
            var (fixture, existingKey) = x;
            var configuration = fixture.ToConfiguration();

            // Act
            var result = configuration.TryGetValue(existingKey);

            // Assert
            var expectedValue = fixture.Items[existingKey];
            result.Should().BeSome(value => value.Should().Be(expectedValue));
        });
    }

    private static Gen<Fixture> GenerateFixture()
    {
        return from map in Generator.NonEmptyOrWhiteSpaceString
                                    .Zip(Generator.GenerateDefault<string>().OrNull())
                                    .SeqOf()
                                    .DistinctBy(x => x.Item1.ToUpperInvariant())
                                    .Select(x => x.ToHashMap())
               select new Fixture
               {
                   Items = map
               };
    }

    private sealed record Fixture
    {
        public HashMap<string, string?> Items { get; init; }

        public IConfiguration ToConfiguration()
        {
            var keyValuePairs = Items.Map(x => KeyValuePair.Create(x.Key, x.Value));

            return new ConfigurationBuilder()
                        .AddInMemoryCollection(keyValuePairs)
                        .Build();
        }
    }
}