using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TestFramework.Azure.DB.CosmosDB;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// A Cosmos document reads the same way whether a reference reads it by id or a finder finds it by query.
/// </summary>
/// <remarks>
/// The query used the Cosmos SDK's own serializer and the point read used Newtonsoft, so after a caller configured
/// the SDK's serializer a found item and the same item read back could disagree - a Newtonsoft name mapping honoured
/// by one road and ignored by the other.
/// </remarks>
public sealed class CosmosJsonTests
{
    public sealed class Order
    {
        [JsonProperty("id")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("customer_name")]
        public string CustomerName { get; set; } = string.Empty;
    }

    private static Stream Text(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void AQueryPageReadsItsDocumentsWithTheFamilysSerializer()
    {
        IReadOnlyList<Order> items = CosmosJson.ReadQueryPage<Order>(Text("""
            { "_rid": "x", "Documents": [ { "id": "1", "customer_name": "Ada" }, { "id": "2", "customer_name": "Grace" } ], "_count": 2 }
            """));

        Assert.Equal(["1", "2"], [items[0].Key, items[1].Key]);
        Assert.Equal("Grace", items[1].CustomerName);
    }

    [Fact]
    public void AQueriedDocumentAndTheSameDocumentReadById_AreOneRecord()
    {
        const string document = """{ "id": "7", "customer_name": "Hedy" }""";

        Order found = CosmosJson.ReadQueryPage<Order>(Text($$"""{ "Documents": [ {{document}} ] }"""))[0];
        Order read = CosmosJson.ReadItem<Order>(Text(document));

        Assert.Equal((read.Key, read.CustomerName), (found.Key, found.CustomerName));
    }

    [Fact]
    public void AnEmptyPage_HasNoItems()
        => Assert.Empty(CosmosJson.ReadQueryPage<Order>(Text("""{ "Documents": [], "_count": 0 }""")));
}
