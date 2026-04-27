using System.Collections.Generic;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// Thread-safe runtime store for Azure configuration records keyed by logical identifier.
/// </summary>
/// <typeparam name="TConfig">The configuration record type stored in the collection.</typeparam>
public class ConfigStore<TConfig>
{
    private readonly Dictionary<string, TConfig> _config = [];
    private readonly object _syncRoot = new();

    /// <summary>
    /// Creates a store pre-populated with a single identifier/config pair.
    /// </summary>
    /// <param name="identifier">The logical identifier to register.</param>
    /// <param name="config">The configuration instance to store.</param>
    /// <returns>A new store containing the provided entry.</returns>
    public static ConfigStore<TConfig> Create(string identifier, TConfig config)
    {
        ConfigStore<TConfig> store = new();
        store.AddConfig(identifier, config);
        return store;
    }

    /// <summary>
    /// Adds or replaces the configuration for an identifier.
    /// </summary>
    /// <param name="identifier">The logical identifier used in the Azure DSL.</param>
    /// <param name="config">The configuration instance to store.</param>
    public void AddConfig(string identifier, TConfig config)
    {
        lock (_syncRoot)
        {
            _config[identifier] = config;
        }
    }

    /// <summary>
    /// Retrieves the configuration associated with an identifier.
    /// </summary>
    /// <param name="identifier">The logical identifier to resolve.</param>
    /// <returns>The stored configuration instance.</returns>
    public TConfig GetConfig(string identifier)
    {
        lock (_syncRoot)
        {
            return _config[identifier];
        }
    }

    /// <summary>
    /// Returns a copy of the current identifier/config map.
    /// </summary>
    /// <returns>A snapshot of the stored configuration entries.</returns>
    public IReadOnlyDictionary<string, TConfig> Snapshot()
    {
        lock (_syncRoot)
        {
            return new Dictionary<string, TConfig>(_config);
        }
    }
}