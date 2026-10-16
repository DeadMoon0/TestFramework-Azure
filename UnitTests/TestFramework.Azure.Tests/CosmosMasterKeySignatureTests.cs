using System;
using System.Net;
using TestFramework.Azure.DB.CosmosDb;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// The master-key signature schema setup sends to Cosmos, checked against the example Microsoft publishes for it.
/// </summary>
/// <remarks>
/// A wrong signature is a 401 from every account, real or emulated, and reads as a bad key. The published vector
/// is the one independent answer to whether the payload is put together the way the service checks it.
/// </remarks>
public class CosmosMasterKeySignatureTests
{
    [Fact]
    public void TheSignatureMatchesMicrosoftsPublishedExample()
    {
        string token = CosmosSchemaRestClient.GenerateMasterKeyAuthorizationSignature(
            verb: "GET",
            resourceType: "dbs",
            resourceLink: "dbs/ToDoList",
            date: "Thu, 27 Apr 2017 00:51:12 GMT",
            key: "dsZQi3KtZmCv1ljt3VNWNm7sQUF1y5rJfC6kv5JiwvW0EndXdDku/dkKBp8/ufDToSxLzR4y+O/0H/t4bQtVNw==");

        Assert.Equal("type=master&ver=1.0&sig=c09PEVJrgp2uQRkr934kFbTqhByc7TVr3OHyqlu+c+c=", WebUtility.UrlDecode(token));
    }
}
