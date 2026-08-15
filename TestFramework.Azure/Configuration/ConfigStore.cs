using System;
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
    /// <exception cref="TestFramework.Core.Exceptions.FrameworkConfigurationException">
    /// Thrown when no configuration is registered under <paramref name="identifier"/>. The message
    /// lists the identifiers that are registered, because a typo here is the most common first-run
    /// mistake.
    /// </exception>
    public TConfig GetConfig(string identifier)
    {
        lock (_syncRoot)
        {
            if (_config.TryGetValue(identifier, out TConfig? config))
            {
                return config;
            }

            List<string> registered = new(_config.Keys);
            registered.Sort(StringComparer.Ordinal);

            string known = registered.Count == 0
                ? "none - no configuration of this type has been registered"
                : string.Join(", ", registered.ConvertAll(entry => $"'{entry}'"));

            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"No {typeof(TConfig).Name} is registered under the identifier '{identifier}'. Registered identifiers: {known}.");
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