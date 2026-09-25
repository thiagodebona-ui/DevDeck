using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>One turn of a conversation.</summary>
    internal sealed record ChatMessage(string Role, string Content);

    /// <summary>
    ///  Talks to any endpoint that speaks the OpenAI chat-completions dialect.
    /// </summary>
    /// <remarks>
    ///  Deliberately not tied to one provider. That one wire format is spoken by Ollama and LM
    ///  Studio running locally, and by Groq, Google Gemini and OpenRouter over the network, so the
    ///  choice between "free and local" and "free tier in the cloud" is a base URL in Settings
    ///  rather than a different client.
    /// </remarks>
    internal sealed class AiClient
    {
        /// <summary>
        ///  Shared, because a new HttpClient per request exhausts sockets. No timeout of its own:
        ///  a long answer is not a failure, and every call is cancellable.
        /// </summary>
        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

        public string BaseUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Where this client is pointed, for the log and for error messages.</summary>
        public string Endpoint => $"{BaseUrl.TrimEnd('/')}/chat/completions";

        /// <summary>
        ///  What the endpoint said the last exchange actually cost, when it said anything.
        /// </summary>
        /// <remarks>
        ///  Null after a call to an endpoint that reported nothing, which is most local ones. The
        ///  caller falls back to an estimate and labels it as one, rather than showing a zero that
        ///  reads as "this was free".
        /// </remarks>
        public (int Sent, int Received)? LastUsage { get; private set; }

        /// <summary>True when the endpoint is on this machine, so nothing leaves it.</summary>
        public bool IsLocal =>
            Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? uri) && uri.IsLoopback;

        /// <summary>
        ///  Streams a reply, handing each fragment to <paramref name="onDelta"/> as it arrives.
        /// </summary>
        /// <remarks>
        ///  Streaming rather than waiting for the whole answer: a 7B model on a laptop CPU can take
        ///  the better part of a minute, and watching it write is the difference between "thinking"
        ///  and "hung".
        /// </remarks>
        public async Task StreamAsync(
            IEnumerable<ChatMessage> messages,
            Action<string> onDelta,
            CancellationToken token)
        {
            LastUsage = null;

            object payload = new
            {
                model = Model,
                stream = true,

                // Asks for a final chunk carrying the real token counts. Endpoints that do not know
                // this option ignore it, which is why it can be sent unconditionally.
                stream_options = new { include_usage = true },
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
            };

            using HttpRequestMessage request = new(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (ApiKey.Length > 0)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            }

            using HttpResponseMessage response =
                await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await DescribeFailureAsync(response, token));
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(token);
            using StreamReader reader = new(stream, Encoding.UTF8);

            while (await reader.ReadLineAsync(token) is string line)
            {
                // Server-sent events: blank separators, and comment lines some proxies use as a
                // keep-alive. Neither carries content.
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                string data = line[5..].Trim();

                if (data == "[DONE]")
                {
                    break;
                }

                if (ReadUsage(data) is { } usage)
                {
                    LastUsage = usage;
                }

                string? delta = ReadDelta(data);

                if (!string.IsNullOrEmpty(delta))
                {
                    onDelta(delta);
                }
            }
        }

        /// <summary>Model names the endpoint advertises, so the picker is not free text.</summary>
        public async Task<List<string>> ListModelsAsync(CancellationToken token)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl.TrimEnd('/')}/models");

            if (ApiKey.Length > 0)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            }

            using HttpResponseMessage response = await Http.SendAsync(request, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await DescribeFailureAsync(response, token));
            }

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));

            JsonElement root = document.RootElement;

            // OpenAI and Ollama answer {"data":[...]}, Gemini's native list uses {"models":[...]},
            // and a couple of local servers just return the bare array.
            JsonElement items =
                root.ValueKind == JsonValueKind.Array ? root
                : TryProperty(root, "data", out JsonElement data) ? data
                : TryProperty(root, "models", out JsonElement models) ? models
                : default;

            if (items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return items.EnumerateArray()
                .Select(ModelName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>A model's identifier, whichever field this provider puts it in.</summary>
        private static string? ModelName(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }

            if (TryProperty(item, "id", out JsonElement id) && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }

            return TryProperty(item, "name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }

        /// <summary>
        ///  Reads a property only when the element really is an object.
        /// </summary>
        /// <remarks>
        ///  JsonElement.TryGetProperty throws InvalidOperationException - not JsonException - when
        ///  the element is an array or a scalar. Providers disagree about the shape of an error
        ///  body, so calling it unguarded put ".NET requires an element of type 'Object'" on screen
        ///  in place of whatever the API had actually complained about.
        /// </remarks>
        private static bool TryProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                return element.TryGetProperty(name, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        ///  Pulls the token counts out of a chunk, when one carries them.
        /// </summary>
        /// <remarks>
        ///  The usage chunk arrives last and has an empty choices array, so it is read separately
        ///  from the text rather than alongside it. Absent on most endpoints, which is not a
        ///  failure - it is the ordinary case for anything running locally.
        /// </remarks>
        private static (int Sent, int Received)? ReadUsage(string data)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(data);

                if (!TryProperty(document.RootElement, "usage", out JsonElement usage)
                    || usage.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                int sent = TryProperty(usage, "prompt_tokens", out JsonElement asked)
                    && asked.TryGetInt32(out int askedValue) ? askedValue : 0;

                int received = TryProperty(usage, "completion_tokens", out JsonElement answered)
                    && answered.TryGetInt32(out int answeredValue) ? answeredValue : 0;

                // Both zero is a field that was present and empty, which is no more informative
                // than its being absent - and would otherwise read as a free exchange.
                return sent == 0 && received == 0 ? null : (sent, received);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Pulls the text out of one streamed chunk, ignoring anything malformed.</summary>
        private static string? ReadDelta(string data)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(data);

                if (!TryProperty(document.RootElement, "choices", out JsonElement choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement choice = choices[0];

                // "delta" while streaming; "message" is what a provider that quietly ignored
                // stream:true sends back instead.
                JsonElement holder = TryProperty(choice, "delta", out JsonElement delta)
                    ? delta
                    : TryProperty(choice, "message", out JsonElement message) ? message : default;

                return TryProperty(holder, "content", out JsonElement content)
                    && content.ValueKind == JsonValueKind.String
                        ? content.GetString()
                        : null;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        ///  Turns a failed response into something worth showing: providers put the useful part in
        ///  the body, and "404 Not Found" on its own sends people hunting in the wrong place.
        /// </summary>
        private static async Task<string> DescribeFailureAsync(HttpResponseMessage response, CancellationToken token)
        {
            string body;

            try
            {
                body = (await response.Content.ReadAsStringAsync(token)).Trim();
            }
            catch (Exception)
            {
                body = string.Empty;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(body);

                JsonElement root = document.RootElement;

                // Google's APIs wrap an error in an array: [{"error": {...}}].
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    root = root[0];
                }

                if (TryProperty(root, "error", out JsonElement error))
                {
                    body = error.ValueKind == JsonValueKind.String
                        ? error.GetString() ?? body
                        : TryProperty(error, "message", out JsonElement message)
                            && message.ValueKind == JsonValueKind.String
                                ? message.GetString() ?? body
                                : body;
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                // Not JSON, or not a shape we know - a proxy error page, most likely. Show it raw.
            }

            if (body.Length > 400)
            {
                body = body[..400] + "…";
            }

            return body.Length > 0
                ? $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}"
                : $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }
}
