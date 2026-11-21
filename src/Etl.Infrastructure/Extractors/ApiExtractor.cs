using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Etl.Application;
using Etl.Domain;
using Etl.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Etl.Infrastructure.Extractors;

/// <summary>
/// Consume comentarios desde una API REST usando un HttpClient tipado (IHttpClientFactory).
/// La respuesta JSON (array) se deserializa en streaming.
/// </summary>
public sealed class ApiExtractor : IExtractor
{
    private readonly HttpClient _http;
    private readonly ApiOptions _options;

    public ApiExtractor(HttpClient http, IOptions<ApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string Name => SourceNames.Api;

    public async IAsyncEnumerable<Opinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = _http.GetFromJsonAsAsyncEnumerable<ApiComment>(_options.Endpoint, cancellationToken);

        await foreach (var c in items.WithCancellation(cancellationToken))
        {
            if (c is null) continue;
            yield return new Opinion
            {
                Source = Name,
                ExternalId = c.Id.ToString(),
                Text = c.Body ?? string.Empty
            };
        }
    }

    private sealed class ApiComment
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
    }
}
