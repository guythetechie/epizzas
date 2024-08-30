using LanguageExt;
using Microsoft.Extensions.Configuration;

namespace common;

public static class ConfigurationExtensions
{
    public static Option<string> TryGetValue(this IConfiguration configuration, string key) =>
        configuration.GetOptionalSection(key)
                     .Bind(section => Prelude.Optional(section.Value));

    public static Option<IConfigurationSection> GetOptionalSection(this IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);

        return section.Exists()
                ? Option<IConfigurationSection>.Some(section)
                : Option<IConfigurationSection>.None;
    }
}