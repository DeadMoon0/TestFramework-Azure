using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

internal class ConfigLoader(IConfigProvider configProvider)
{
    internal void LoadAllConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        LoadFunctionAppConfigs(configuration, serviceCollection);
        LoadCosmosDbConfigs(configuration, serviceCollection);
        LoadServiceBusConfigs(configuration, serviceCollection);
        LoadStorageAccountConfigs(configuration, serviceCollection);
        LoadSqlDatabaseConfigs(configuration, serviceCollection);
    }

    // Always register the store itself so environment-backed runs can hydrate
    // identifiers later even when the static config source did not define them.
    private static ConfigStore<TConfig> EnsureStoreRegistered<TConfig>(IServiceCollection serviceCollection)
    {
        ConfigStore<TConfig> store = new ConfigStore<TConfig>();
        serviceCollection.AddSingleton(store);
        return store;
    }

    internal void LoadFunctionAppConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<FunctionAppConfig> store = EnsureStoreRegistered<FunctionAppConfig>(serviceCollection);
        foreach (var key in configProvider.LoadAllFunctionAppIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadFunctionAppConfig(configuration, key));
        }
    }

    internal void LoadCosmosDbConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<CosmosContainerDbConfig> store = EnsureStoreRegistered<CosmosContainerDbConfig>(serviceCollection);
        foreach (var key in configProvider.LoadAllCosmosDbIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadCosmosDbConfig(configuration, key));
        }
    }

    internal void LoadServiceBusConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<ServiceBusConfig> store = EnsureStoreRegistered<ServiceBusConfig>(serviceCollection);
        foreach (var key in configProvider.LoadAllServiceBusIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadServiceBusConfig(configuration, key));
        }
    }

    internal void LoadStorageAccountConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<StorageAccountConfig> store = EnsureStoreRegistered<StorageAccountConfig>(serviceCollection);
        foreach (var key in configProvider.LoadAllStorageAccountIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadStorageAccountConfig(configuration, key));
        }
    }

    internal void LoadSqlDatabaseConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<SqlDatabaseConfig> store = EnsureStoreRegistered<SqlDatabaseConfig>(serviceCollection);
        foreach (var key in configProvider.LoadAllSqlDatabaseIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadSqlDatabaseConfig(configuration, key));
        }
    }
}