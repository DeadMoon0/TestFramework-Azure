using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;

namespace TestFramework.Azure.Configuration;

public interface IConfigProvider
{
    public string[] LoadAllFunctionAppIdentifier(IConfiguration configuration);
    public FunctionAppConfig LoadFunctionAppConfig(IConfiguration configuration, string identifier);
    public string[] LoadAllStorageAccountIdentifier(IConfiguration configuration);
    public StorageAccountConfig LoadStorageAccountConfig(IConfiguration configuration, string identifier);
    public string[] LoadAllCosmosDbIdentifier(IConfiguration configuration);
    public CosmosContainerDbConfig LoadCosmosDbConfig(IConfiguration configuration, string identifier);
    public string[] LoadAllServiceBusIdentifier(IConfiguration configuration);
    public ServiceBusConfig LoadServiceBusConfig(IConfiguration configuration, string identifier);
    public string[] LoadAllSqlDatabaseIdentifier(IConfiguration configuration);
    public SqlDatabaseConfig LoadSqlDatabaseConfig(IConfiguration configuration, string identifier);
}