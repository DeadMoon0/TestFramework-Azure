using System.Collections.Generic;

namespace TestFramework.Azure.Configuration;

internal class ConfigStore<TConfig>
{
    private readonly Dictionary<string, TConfig> _config = [];

    internal void AddConfig(string identifier, TConfig config)
    {
        _config[identifier] = config;
    }

    internal TConfig GetConfig(string identifier)
    {
        return _config[identifier];
    }
}