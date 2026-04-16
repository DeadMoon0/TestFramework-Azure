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

    internal void LoadFunctionAppConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<FunctionAppConfig> store = new ConfigStore<FunctionAppConfig>();
        foreach (var key in configProvider.LoadAllFunctionAppIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadFunctionAppConfig(configuration, key));
        }
        serviceCollection.AddSingleton(store);
    }

    internal void LoadCosmosDbConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<CosmosContainerDbConfig> store = new ConfigStore<CosmosContainerDbConfig>();
        foreach (var key in configProvider.LoadAllCosmosDbIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadCosmosDbConfig(configuration, key));
        }
        serviceCollection.AddSingleton(store);
    }

    internal void LoadServiceBusConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<ServiceBusConfig> store = new ConfigStore<ServiceBusConfig>();
        foreach (var key in configProvider.LoadAllServiceBusIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadServiceBusConfig(configuration, key));
        }
        serviceCollection.AddSingleton(store);
    }

    internal void LoadStorageAccountConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<StorageAccountConfig> store = new ConfigStore<StorageAccountConfig>();
        foreach (var key in configProvider.LoadAllStorageAccountIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadStorageAccountConfig(configuration, key));
        }
        serviceCollection.AddSingleton(store);
    }

    internal void LoadSqlDatabaseConfigs(IConfiguration configuration, IServiceCollection serviceCollection)
    {
        ConfigStore<SqlDatabaseConfig> store = new ConfigStore<SqlDatabaseConfig>();
        foreach (var key in configProvider.LoadAllSqlDatabaseIdentifier(configuration))
        {
            store.AddConfig(key, configProvider.LoadSqlDatabaseConfig(configuration, key));
        }
        serviceCollection.AddSingleton(store);
    }
}