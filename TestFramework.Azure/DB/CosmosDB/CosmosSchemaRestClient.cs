using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Minimal REST-based helper for creating Cosmos databases and containers when SDK metadata setup is insufficient.
/// </summary>
public static class CosmosSchemaRestClient
{
    private const string ApiVersion = "2018-12-31";

    /// <summary>
    /// Ensures that the target Cosmos database and container exist.
    /// </summary>
    /// <param name="connectionString">The Cosmos account connection string.</param>
    /// <param name="databaseName">The database name to create or validate.</param>
    /// <param name="containerName">The container name to create or validate.</param>
    /// <param name="partitionKeyPath">The container partition key path, including the leading slash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when both resources are available.</returns>
    public static async Task EnsureDatabaseAndContainerExistAsync(
        string connectionString,
        string databaseName,
        string containerName,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        (Uri endpoint, string key) = ParseConnectionString(connectionString);

        using HttpClient httpClient = CreateHttpClient();

        await CreateDatabaseIfNotExistsAsync(httpClient, endpoint, key, databaseName, cancellationToken).ConfigureAwait(false);
        await WaitForDatabaseAsync(httpClient, endpoint, key, databaseName, cancellationToken).ConfigureAwait(false);

        await CreateContainerIfNotExistsAsync(httpClient, endpoint, key, databaseName, containerName, partitionKeyPath, cancellationToken).ConfigureAwait(false);
        await WaitForContainerAsync(httpClient, endpoint, key, databaseName, containerName, cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private static async Task CreateDatabaseIfNotExistsAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, endpoint, key, "dbs", string.Empty, "/dbs");
        request.Content = CreateJsonContent(new { id = databaseName });

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict)
            return;

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateContainerIfNotExistsAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, string containerName, string partitionKeyPath, CancellationToken cancellationToken)
    {
        string resourceLink = $"dbs/{databaseName}";
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, endpoint, key, "colls", resourceLink, $"/dbs/{Uri.EscapeDataString(databaseName)}/colls");
        request.Headers.Add("x-ms-offer-throughput", "400");
        request.Content = CreateJsonContent(new
        {
            id = containerName,
            partitionKey = new
            {
                paths = new[] { partitionKeyPath },
                kind = "Hash",
                version = 2,
            }
        });

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict)
            return;

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForDatabaseAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, CancellationToken cancellationToken)
    {
        await WaitForResourceAsync(
            httpClient,
            endpoint,
            key,
            "dbs",
            $"dbs/{databaseName}",
            $"/dbs/{Uri.EscapeDataString(databaseName)}",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForContainerAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, string containerName, CancellationToken cancellationToken)
    {
        await WaitForResourceAsync(
            httpClient,
            endpoint,
            key,
            "colls",
            $"dbs/{databaseName}/colls/{containerName}",
            $"/dbs/{Uri.EscapeDataString(databaseName)}/colls/{Uri.EscapeDataString(containerName)}",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForResourceAsync(HttpClient httpClient, Uri endpoint, string key, string resourceType, string resourceLink, string relativeUri, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using HttpRequestMessage request = CreateRequest(HttpMethod.Get, endpoint, key, resourceType, resourceLink, relativeUri);
                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                    return;

                if (response.StatusCode == HttpStatusCode.NotFound && response.Headers.TryGetValues("x-ms-substatus", out IEnumerable<string>? substatusValues) && substatusValues.Contains("1013"))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"Timed out while waiting for Cosmos resource '{resourceLink}' to become available.", lastError);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri endpoint, string key, string resourceType, string resourceLink, string relativeUri)
    {
        string requestDate = DateTime.UtcNow.ToString("R");
        string authorization = GenerateMasterKeyAuthorizationSignature(method.Method, resourceType, resourceLink, requestDate, key);

        HttpRequestMessage request = new(method, new Uri(endpoint, relativeUri));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-ms-date", requestDate);
        request.Headers.Add("x-ms-version", ApiVersion);
        request.Headers.Add("authorization", authorization);
        return request;
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException($"Cosmos REST request failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    private static string GenerateMasterKeyAuthorizationSignature(string verb, string resourceType, string resourceLink, string date, string key)
    {
        string payload = $"{verb.ToLowerInvariant()}\n{resourceType.ToLowerInvariant()}\n{resourceLink}\n{date.ToLowerInvariant()}\n\n";
        using HMACSHA256 hmacSha256 = new HMACSHA256(Convert.FromBase64String(key));
        byte[] hashPayload = hmacSha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        string signature = Convert.ToBase64String(hashPayload);
        return WebUtility.UrlEncode($"type=master&ver=1.0&sig={signature}");
    }

    private static (Uri Endpoint, string Key) ParseConnectionString(string connectionString)
    {
        DbConnectionStringBuilder builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (!builder.TryGetValue("AccountEndpoint", out object? endpointValue) || endpointValue is not string endpointText)
            throw new InvalidOperationException("The Cosmos connection string is missing 'AccountEndpoint'.");

        if (!builder.TryGetValue("AccountKey", out object? keyValue) || keyValue is not string keyText)
            throw new InvalidOperationException("The Cosmos connection string is missing 'AccountKey'.");

        return (new Uri(endpointText, UriKind.Absolute), keyText);
    }
}