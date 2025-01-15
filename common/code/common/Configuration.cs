using LanguageExt;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace common;

public static class ConfigurationExtensions
{
    public static string GetValueOrThrow(this IConfiguration configuration, string key) =>
        configuration.GetValue(key)
                     .IfNone(() => throw new KeyNotFoundException($"Configuration key '{key}' not found."));

    public static Option<string> GetValue(this IConfiguration configuration, string key) =>
        GetSection(configuration, key)
            .Where(section => section.Value is not null)
            .Select(section => section.Value!);

    private static Option<IConfigurationSection> GetSection(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(key);

        return section.Exists()
                ? Option<IConfigurationSection>.Some(section)
                : Option<IConfigurationSection>.None;
    }
}