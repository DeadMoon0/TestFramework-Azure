using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using TestFramework.Azure;
using TestFramework.Azure.LogicApp;
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
        object builder = AzureExt.Trigger.FunctionApp.Http("func");
        object functionBuilder = AzureExt.Trigger.FunctionApp.Http("func").SelectFunction("HttpEchoTest", HttpMethod.Post);

        Assert.NotNull(builder);
        Assert.NotNull(functionBuilder);
    }

    [Fact]
    public void FunctionApp_ManagedBuilder_ReturnsStep()
    {
        Step<ManagedResult> step = AzureExt.Trigger.FunctionApp.Managed<DummyFunction>("func", "Run");

        Assert.NotNull(step);
    }

    [Fact]
    public void FunctionApp_InProcessHttpOverloads_ReturnBuilderInstances()
    {
        object actionBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => { });
        object taskBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.CompletedTask);
        object resultBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => new OkResult());
        object taskResultBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.FromResult<IActionResult>(new OkResult()));

        Assert.NotNull(actionBuilder);
        Assert.NotNull(taskBuilder);
        Assert.NotNull(resultBuilder);
        Assert.NotNull(taskResultBuilder);
    }

    [Fact]
    public void LogicApp_HttpBuilder_AndEvents_AreCreated()
    {
        object triggerBuilder = AzureExt.Trigger.LogicApp.Http("logic");
        object runCompleted = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const("run-1"));
        object runSucceeded = AzureExt.Event.LogicApp.RunReachedStatus("logic", Var.Const("run-1"), LogicAppRunStatus.Succeeded);
        object runCompletedWithContext = AzureExt.Event.LogicApp.RunCompleted("logic", Var.Const(new LogicAppRunContext("OrderProcessor", "run-1")));
        object capture = AzureExt.Trigger.LogicApp.Http("logic").Workflow("OrderProcessor").Manual().CallAndCapture();
        object timer = AzureExt.Trigger.LogicApp.Http("logic").Workflow("NightlyJob").Timer().Call();
        object timerContext = AzureExt.Trigger.LogicApp.Http("logic").Workflow("NightlyJob").Timer().CallForRunContext();

        Assert.NotNull(triggerBuilder);
        Assert.NotNull(runCompleted);
        Assert.NotNull(runSucceeded);
        Assert.NotNull(runCompletedWithContext);
        Assert.NotNull(capture);
        Assert.NotNull(timer);
        Assert.NotNull(timerContext);
    }

    [Fact]
    public void ArtifactProxies_CreateExpectedReferenceTypes()
    {
        object cosmosRef = AzureExt.Artifact.DB.CosmosRef<object>("cosmos", "id-1", new PartitionKey("tenant"));
        object sqlRef = AzureExt.Artifact.DB.SqlRef<DummyRow>("sql", Var.Const("pk"));
        object tableRef = AzureExt.Artifact.StorageAccount.TableRef<DummyTableEntity>("storage", Var.Const("orders"), Var.Const("tenant"), Var.Const("row"));
        object blobRef = AzureExt.Artifact.StorageAccount.BlobRef("storage", Var.Const("samples/a.txt"));

        Assert.NotNull(cosmosRef);
        Assert.NotNull(sqlRef);
        Assert.NotNull(tableRef);
        Assert.NotNull(blobRef);
    }

    [Fact]
    public void ArtifactFinderProxies_CreateExpectedFinderTypes()
    {
        object cosmosQuery = AzureExt.ArtifactFinder.DB.CosmosQuery<object>("cosmos", Var.Const(new QueryDefinition("SELECT * FROM c")));
        object sqlQuery = AzureExt.ArtifactFinder.DB.SqlQuery<DummyRow>("sql", query => query);
        TableStorageEntityArtifactQueryFinder<DummyTableEntity> tableQuery = AzureExt.ArtifactFinder.StorageAccount.TableQuery<DummyTableEntity>("storage", Var.Const("orders"), "PartitionKey ne ''");

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