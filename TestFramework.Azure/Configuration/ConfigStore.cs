using System.Collections.Generic;

namespace TestFramework.Azure.Configuration;

public class ConfigStore<TConfig>
{
    private readonly Dictionary<string, TConfig> _config = [];
    private readonly object _syncRoot = new();

    public void AddConfig(string identifier, TConfig config)
    {
        lock (_syncRoot)
        {
            _config[identifier] = config;
        }
    }

    public TConfig GetConfig(string identifier)
    {
        lock (_syncRoot)
        {
            return _config[identifier];
        }
    }

    public IReadOnlyDictionary<string, TConfig> Snapshot()
    {
        lock (_syncRoot)
        {
            return new Dictionary<string, TConfig>(_config);
        }
    }
}