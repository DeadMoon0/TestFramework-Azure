using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Minimal REST-based helper for creating Cosmos databases and containers when SDK metadata setup is insufficient.
/// </summary>
internal static class CosmosSchemaRestClient
{
    private const string ApiVersion = "2018-12-31";

    /// <summary>
    /// The client used for every real Cosmos account. Certificate validation stays on: these requests
    /// carry a master-key HMAC signature, and a captured request is replayable full account control.
    /// </summary>
    private static readonly HttpClient ValidatingHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// The client used only for local emulators, which present a self-signed certificate.
    /// </summary>
    private static readonly HttpClient EmulatorHttpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// Ensures that the target Cosmos database and container exist.
    /// </summary>
    /// <param name="config">The resolved Cosmos container configuration.</param>
    /// <param name="partitionKeyPath">The container partition key path, including the leading slash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when both resources are available.</returns>
    /// <remarks>
    /// The common case - the container already exists - costs a single GET. Creation only happens
    /// after that probe comes back empty, and only when it is allowed for the target account.
    /// </remarks>
    public static async Task EnsureDatabaseAndContainerExistAsync(
        CosmosContainerDbConfig config,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        string databaseName = config.DatabaseName;
        string containerName = config.ContainerName;

        (Uri endpoint, string key) = ParseConnectionString(config.ConnectionString);

        HttpClient httpClient = SelectHttpClient(endpoint);

        if (await ContainerExistsAsync(httpClient, endpoint, key, databaseName, containerName, cancellationToken).ConfigureAwait(false))
            return;

        EnsureSchemaCreationAllowed(config, endpoint);

        await CreateDatabaseIfNotExistsAsync(httpClient, endpoint, key, databaseName, cancellationToken).ConfigureAwait(false);
        await WaitForDatabaseAsync(httpClient, endpoint, key, databaseName, cancellationToken).ConfigureAwait(false);

        await CreateContainerIfNotExistsAsync(httpClient, endpoint, key, config, partitionKeyPath, cancellationToken).ConfigureAwait(false);
        await WaitForContainerAsync(httpClient, endpoint, key, databaseName, containerName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Probes the container with a single GET so an existing container costs one round trip.
    /// </summary>
    private static async Task<bool> ContainerExistsAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, string containerName, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Get,
            endpoint,
            key,
            "colls",
            $"dbs/{databaseName}/colls/{containerName}",
            $"/dbs/{Uri.EscapeDataString(databaseName)}/colls/{Uri.EscapeDataString(containerName)}");

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
            return true;

        await EnsureNotAuthorizationFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Refuses to create schema on anything but a local emulator unless the caller opted in.
    /// </summary>
    private static void EnsureSchemaCreationAllowed(CosmosContainerDbConfig config, Uri endpoint)
    {
        if (config.AllowSchemaCreation || LocalEndpoints.IsLocalDevelopmentHost(endpoint))
            return;

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"Cosmos container '{config.DatabaseName}/{config.ContainerName}' does not exist on '{endpoint.Host}', " +
            $"which is not a local emulator. Create it out of band, or set '{nameof(CosmosContainerDbConfig.AllowSchemaCreation)}' " +
            $"to true on the Cosmos configuration to let the framework provision it.");
    }

    /// <summary>
    /// Turns a rejected master key into an actionable configuration error instead of a retry loop.
    /// </summary>
    private static async Task EnsureNotAuthorizationFailureAsync(HttpResponseMessage response, Uri endpoint, CancellationToken cancellationToken)
    {
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            return;

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"Cosmos account '{endpoint.Host}' rejected the configured credentials with status " +
            $"{(int)response.StatusCode} ({response.StatusCode}). Check 'AccountKey' in the Cosmos connection string " +
            $"and that the key is permitted to read and create metadata. Body: {body}");
    }

    /// <summary>
    /// Picks the emulator client only for local development endpoints; everything else - including
    /// every <c>*.documents.azure.com</c> account - gets full certificate validation.
    /// </summary>
    private static HttpClient SelectHttpClient(Uri endpoint)
        => LocalEndpoints.IsLocalDevelopmentHost(endpoint) ? EmulatorHttpClient : ValidatingHttpClient;

    private static async Task CreateDatabaseIfNotExistsAsync(HttpClient httpClient, Uri endpoint, string key, string databaseName, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, endpoint, key, "dbs", string.Empty, "/dbs");
        request.Content = CreateJsonContent(new { id = databaseName });

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict)
            return;

        await EnsureNotAuthorizationFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateContainerIfNotExistsAsync(HttpClient httpClient, Uri endpoint, string key, CosmosContainerDbConfig config, string partitionKeyPath, CancellationToken cancellationToken)
    {
        string databaseName = config.DatabaseName;
        string resourceLink = $"dbs/{databaseName}";
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, endpoint, key, "colls", resourceLink, $"/dbs/{Uri.EscapeDataString(databaseName)}/colls");

        // Only send throughput when it was asked for: a serverless account rejects the header outright,
        // and on a provisioned account an unrequested value is billed for the lifetime of the container.
        if (config.ProvisionedThroughput is int throughput)
            request.Headers.Add("x-ms-offer-throughput", throughput.ToString(System.Globalization.CultureInfo.InvariantCulture));

        request.Content = CreateJsonContent(new
        {
            id = config.ContainerName,
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

        await EnsureNotAuthorizationFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
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

                // A rejected key never becomes valid by waiting, so it must escape this loop.
                await EnsureNotAuthorizationFailureAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
                await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The HttpClient timeout elapsed rather than the caller cancelling - that is retryable.
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TestFramework.Core.Exceptions.FrameworkTimeoutException($"Timed out while waiting for Cosmos resource '{resourceLink}' to become available.", lastError);
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
        return new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
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
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("The Cosmos connection string is missing 'AccountEndpoint'.");

        if (!builder.TryGetValue("AccountKey", out object? keyValue) || keyValue is not string keyText)
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("The Cosmos connection string is missing 'AccountKey'.");

        return (new Uri(endpointText, UriKind.Absolute), keyText);
    }
}