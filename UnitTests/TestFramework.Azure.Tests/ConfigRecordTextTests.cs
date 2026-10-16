using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestFramework.Azure.Configuration.SpecificConfigs;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Pins that a configuration record's text never carries its credentials.
/// </summary>
/// <remarks>
/// The damage is a key in clear wherever a record is turned into text: <c>context.Configured&lt;ServiceBusConfig&gt;("x")</c>
/// in a log line, an assertion message or a debugger's value view printed the connection string, because a record's
/// synthesized <c>ToString()</c> prints every member.
/// </remarks>
public sealed class ConfigRecordTextTests
{
    private static readonly Type[] Records = [.. typeof(ServiceBusConfig).Assembly.GetTypes()
        .Where(type => type.IsPublic && type.Namespace == typeof(ServiceBusConfig).Namespace && type.GetMethod("<Clone>$") is not null)
        .OrderBy(type => type.Name)];

    [Fact]
    public void TheCredentialsAreTheOnesThisTestNames()
    {
        // Pinned, so that dropping a marker is a visible decision rather than a quiet leak.
        string[] marked = [.. Records
            .SelectMany(type => type.GetProperties())
            .Where(property => property.GetCustomAttributes().Any(attribute => attribute.GetType().Name is "SecretAttribute" or "SignedUrlAttribute"))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order()];

        Assert.Equal(
            [
                "CosmosDbConfig.ConnectionString",
                "FunctionAppConfig.AdminCode",
                "FunctionAppConfig.Code",
                "LogicAppConsumptionConfig.InvokeUrl",
                "LogicAppStandardConfig.AdminCode",
                "LogicAppStandardConfig.Code",
                "ServiceBusConfig.ConnectionString",
                "SqlDatabaseConfig.ConnectionString",
                "StorageAccountConfig.ConnectionString",
            ],
            marked);
    }

    [Fact]
    public void ARecordsTextShowsItsSettingsAndHidesItsCredentials()
    {
        Assert.Equal(8, Records.Length);

        foreach (Type type in Records)
        {
            object record = Activator.CreateInstance(type)!;
            Dictionary<PropertyInfo, string> sentinels = [];

            foreach (PropertyInfo property in type.GetProperties().Where(property => property.PropertyType == typeof(string) && property.SetMethod is not null))
            {
                // A signed URL is shown without its signature, so its secret part is where the sentinel goes.
                string sentinel = $"sentinel-{type.Name}-{property.Name}";
                bool isSignedUrl = property.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "SignedUrlAttribute");
                property.SetValue(record, isSignedUrl ? $"https://host.test/invoke?api-version=1&sig={sentinel}" : sentinel);
                sentinels[property] = sentinel;
            }

            string text = record.ToString()!;

            foreach ((PropertyInfo property, string sentinel) in sentinels)
            {
                bool isCredential = property.GetCustomAttributes().Any(attribute => attribute.GetType().Name is "SecretAttribute" or "SignedUrlAttribute");
                if (isCredential)
                    Assert.DoesNotContain(sentinel, text, StringComparison.Ordinal);
                else
                    Assert.Contains(sentinel, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void AConnectionStringIsShownAsSetWithoutItsValue()
    {
        ServiceBusConfig config = new() { ConnectionString = "Endpoint=sb://bus/;SharedAccessKey=abc123", QueueName = "orders" };

        string text = config.ToString();

        Assert.DoesNotContain("abc123", text, StringComparison.Ordinal);
        Assert.Contains("ConnectionString = (secret)", text, StringComparison.Ordinal);
        Assert.Contains("QueueName = orders", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AConsumptionInvokeUrlKeepsItsAddressAndLosesItsSignature()
    {
        LogicAppConfig config = new()
        {
            HostingMode = Azure.LogicApp.LogicAppHostingMode.Consumption,
            Consumption = new LogicAppConsumptionConfig { InvokeUrl = "https://prod.logic.azure.com/workflows/abc/triggers/manual/paths/invoke?api-version=2016-10-01&sig=SIGNATURE" },
        };

        string text = config.ToString();

        Assert.DoesNotContain("SIGNATURE", text, StringComparison.Ordinal);
        Assert.Contains("prod.logic.azure.com", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordWithoutItsOptionalNames_CanStillBeWrittenAsText()
    {
        // The synthesized text read the computed BlobContainerNameRequired too, which throws when the name is absent -
        // so the record could not even be logged.
        StorageAccountConfig config = new() { ConnectionString = "UseDevelopmentStorage=true" };

        string text = config.ToString();

        Assert.Contains("BlobContainerName = null", text, StringComparison.Ordinal);
    }
}
