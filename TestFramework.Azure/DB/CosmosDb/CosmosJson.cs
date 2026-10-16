using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Azure.DB.CosmosDb;

/// <summary>
/// Reads Cosmos documents with the family's serializer - Newtonsoft - whichever way they were fetched.
/// </summary>
/// <remarks>
/// A point read and a query used to read the same document with two libraries: the read through Newtonsoft, the query
/// through the Cosmos SDK's own serializer. Once a caller configured that serializer, a finder and a reference could
/// see one record two different ways. Both now fetch the raw stream and read it here.
/// </remarks>
internal static class CosmosJson
{
    /// <summary>
    /// Reads one document.
    /// </summary>
    /// <typeparam name="T">The item type, or <see cref="Stream"/> for the raw body.</typeparam>
    /// <param name="stream">The response body; disposed here.</param>
    /// <returns>The item.</returns>
    internal static T ReadItem<T>(Stream stream)
    {
        if (typeof(Stream).IsAssignableFrom(typeof(T)))
            return (T)(object)stream;

        using (stream)
        using (StreamReader reader = new(stream))
        using (JsonTextReader json = new(reader))
        {
            return JsonSerializer.CreateDefault().Deserialize<T>(json)
                ?? throw new TestFramework.Core.Exceptions.FrameworkStateException("Could not read and parse JSON from CosmosDb.");
        }
    }

    /// <summary>
    /// Reads the documents of one query page.
    /// </summary>
    /// <remarks>
    /// A query page's body is <c>{ "Documents": [ ... ], "_count": n }</c>.
    /// </remarks>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="stream">The page body; disposed here.</param>
    /// <returns>The page's items, in order.</returns>
    internal static IReadOnlyList<T> ReadQueryPage<T>(Stream stream)
    {
        using (stream)
        using (StreamReader reader = new(stream))
        using (JsonTextReader json = new(reader))
        {
            JsonSerializer serializer = JsonSerializer.CreateDefault();
            JObject page = JObject.Load(json);

            List<T> items = [];
            if (page["Documents"] is JArray documents)
            {
                foreach (JToken document in documents)
                {
                    items.Add(document.ToObject<T>(serializer)
                        ?? throw new TestFramework.Core.Exceptions.FrameworkStateException("A Cosmos query returned a document that reads as null."));
                }
            }

            return items;
        }
    }
}
