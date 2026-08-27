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

            // The known identifiers go in the exception's own list rather than into the sentence. Concatenating
            // them made one long line that a reader has to parse; the structured form is what every other
            // failure in the family uses, and it is what a debugger renders as a list.
            List<string> registered = new(_config.Keys);
            registered.Sort(StringComparer.Ordinal);

            // "Nothing of this type at all" stays in the sentence, because an empty Available list renders as
            // nothing and a reader would not be able to tell it from a typo.
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                registered.Count == 0
                    ? $"No {typeof(TConfig).Name} is registered under the identifier '{identifier}', and no configuration of this type has been registered at all."
                    : $"No {typeof(TConfig).Name} is registered under the identifier '{identifier}'.",
                [
                    "Add the entry to your configuration, or declare a default for it on the component definition that provisions the resource.",
                ],
                registered);
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