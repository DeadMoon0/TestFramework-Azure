using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using TestFramework.Azure;
using TestFramework.Azure.FunctionApp.InProcessProxies;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.StorageAccount.Table;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

public class AzureDslCoverageTests
{
    [Fact]
    public void FunctionApp_HttpBuilder_IsCreated()
    {
        object builder = AzureTF.Trigger.FunctionApp.Http("func");

        Assert.NotNull(builder);
    }

    [Fact]
    public void FunctionApp_ManagedBuilder_ReturnsStep()
    {
        Step<ManagedResult> step = AzureTF.Trigger.FunctionApp.Managed<DummyFunction>("func", "Run");

        Assert.NotNull(step);
    }

    [Fact]
    public void FunctionApp_InProcessHttpOverloads_ReturnBuilderInstances()
    {
        object actionBuilder = AzureTF.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => { });
        object taskBuilder = AzureTF.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.CompletedTask);
        object resultBuilder = AzureTF.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => new OkResult());
        object taskResultBuilder = AzureTF.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.FromResult<IActionResult>(new OkResult()));

        Assert.NotNull(actionBuilder);
        Assert.NotNull(taskBuilder);
        Assert.NotNull(resultBuilder);
        Assert.NotNull(taskResultBuilder);
    }

    [Fact]
    public void ArtifactProxies_CreateExpectedReferenceTypes()
    {
        object cosmosRef = AzureTF.Artifact.DB.CosmosRef<object>("cosmos", "id-1", new PartitionKey("tenant"));
        object sqlRef = AzureTF.Artifact.DB.SqlRef<DummyRow>("sql", Var.Const("pk"));
        object tableRef = AzureTF.Artifact.StorageAccount.TableRef<DummyTableEntity>("storage", Var.Const("orders"), Var.Const("tenant"), Var.Const("row"));
        object blobRef = AzureTF.Artifact.StorageAccount.BlobRef("storage", Var.Const("samples/a.txt"));

        Assert.NotNull(cosmosRef);
        Assert.NotNull(sqlRef);
        Assert.NotNull(tableRef);
        Assert.NotNull(blobRef);
    }

    [Fact]
    public void ArtifactFinderProxies_CreateExpectedFinderTypes()
    {
        object cosmosQuery = AzureTF.ArtifactFinder.DB.CosmosQuery<object>("cosmos", Var.Const(new QueryDefinition("SELECT * FROM c")));
        object sqlQuery = AzureTF.ArtifactFinder.DB.SqlQuery<DummyRow>("sql", query => query);
        TableStorageEntityArtifactQueryFinder<DummyTableEntity> tableQuery = AzureTF.ArtifactFinder.StorageAccount.TableQuery<DummyTableEntity>("storage", Var.Const("orders"), "PartitionKey ne ''");

        Assert.NotNull(cosmosQuery);
        Assert.NotNull(sqlQuery);
        Assert.NotNull(tableQuery);
    }

    private sealed class DummyFunction
    {
        [Function("Run")]
        public void Run()
        {
        }
    }

    private sealed class DummyRow;

    private sealed class DummyTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public global::Azure.ETag ETag { get; set; }
    }
}