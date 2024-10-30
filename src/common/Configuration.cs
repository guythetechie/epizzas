using LanguageExt;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace common;

public static class ConfigurationExtensions
{
    public static string GetValueOrThrow(this IConfiguration configuration, string key) =>
        configuration.GetValue(key)
                     .IfNone(() => throw new InvalidOperationException($"Configuration key '{key}' not found."));

    public static Option<string> GetValue(this IConfiguration configuration, string key) =>
        GetSection(configuration, key)
            .Where(section => section.Value is not null)
            .Select(section => section.Value!);

    public static Option<IConfigurationSection> GetSection(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(key);

        return section.Exists()
                ? Option<IConfigurationSection>.Some(section)
                : Option<IConfigurationSection>.None;
    }

    public static IConfigurationBuilder AddUserSecretsWithLowestPriority(this IConfigurationBuilder builder, Assembly assembly, bool optional = true) =>
        builder.AddWithLowestPriority(b => b.AddUserSecrets(assembly, optional));

    private static IConfigurationBuilder AddWithLowestPriority(this IConfigurationBuilder builder, Func<IConfigurationBuilder, IConfigurationBuilder> adder)
    {
        // Configuration sources added last have the highest priority. We empty existing sources,
        // add the new sources, and then add the existing sources back.
        var adderSources = adder(new ConfigurationBuilder()).Sources;
        var existingSources = builder.Sources;
        var sources = adderSources.Concat(existingSources)
                                  .ToImmutableArray();

        builder.Sources.Clear();
        sources.Iter(source => builder.Add(source));

        return builder;
    }
}