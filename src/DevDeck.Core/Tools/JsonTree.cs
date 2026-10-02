using System.Globalization;
using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>What a node in a JSON document is, which decides its colour.</summary>
    internal enum JsonNodeKind
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null,

        /// <summary>The marker left where the tree stopped growing. See <see cref="JsonTree"/>.</summary>
        More,
    }

    /// <summary>One key or element, with what is under it.</summary>
    internal sealed class JsonNode
    {
        public JsonNode(string key, JsonNodeKind kind, string value, IReadOnlyList<JsonNode> children)
        {
            Key = key;
            Kind = kind;
            Value = value;
            Children = children;
        }

        /// <summary>The property name, or "[3]" for an array element. Empty for the root.</summary>
        public string Key { get; }

        public JsonNodeKind Kind { get; }

        /// <summary>The value as written for a leaf; "{ 4 }" or "[ 12 ]" for a container.</summary>
        public string Value { get; }

        public IReadOnlyList<JsonNode> Children { get; }

        public bool HasKey => Key.Length > 0;

        public bool IsString => Kind == JsonNodeKind.String;

        public bool IsNumber => Kind == JsonNodeKind.Number;

        public bool IsLiteral => Kind is JsonNodeKind.Boolean or JsonNodeKind.Null;

        public bool IsContainer => Kind is JsonNodeKind.Object or JsonNodeKind.Array or JsonNodeKind.More;
    }

    /// <summary>
    ///  A JSON document as a tree to expand and collapse.
    /// </summary>
    /// <remarks>
    ///  The formatted text answers "is this valid" and "copy me the result"; the tree answers
    ///  "where in this is the thing I want", which in a payload four levels deep is the question
    ///  people actually came with. Collapsing everything but one branch is how a thousand-line
    ///  response becomes readable.
    ///
    ///  Capped, but generously: the tree is drawn as a virtualised list, so only the rows in view
    ///  cost anything on screen, and the caps bound the memory and the parse instead. Past one, a
    ///  container ends with a marker saying how much was left out, and the text view still has
    ///  all of it.
    /// </remarks>
    internal static class JsonTree
    {
        public const int MostNodes = 200_000;

        /// <summary>Most children kept under one container, so one huge array does not spend the whole budget.</summary>
        public const int MostChildren = 20_000;

        /// <summary>The parsed document, or null when the text is not JSON.</summary>
        public static JsonNode? Build(string json)
        {
            if (json.Trim().Length == 0)
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                int budget = MostNodes;

                return Node(string.Empty, document.RootElement, ref budget);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static JsonNode Node(string key, JsonElement element, ref int budget)
        {
            budget--;

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    List<JsonNode> children = [];
                    int count = 0;

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        count++;

                        if (budget <= 0 || children.Count >= MostChildren)
                        {
                            continue;
                        }

                        children.Add(Node(property.Name, property.Value, ref budget));
                    }

                    Cut(children, count);

                    return new JsonNode(key, JsonNodeKind.Object, Count("{", count, "}"), children);
                }

                case JsonValueKind.Array:
                {
                    List<JsonNode> children = [];
                    int count = 0;

                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        if (budget > 0 && children.Count < MostChildren)
                        {
                            children.Add(Node($"[{count}]", item, ref budget));
                        }

                        count++;
                    }

                    Cut(children, count);

                    return new JsonNode(key, JsonNodeKind.Array, Count("[", count, "]"), children);
                }

                case JsonValueKind.String:
                    return new JsonNode(key, JsonNodeKind.String, JsonSerializer.Serialize(element.GetString()), []);

                case JsonValueKind.Number:
                    return new JsonNode(key, JsonNodeKind.Number, element.GetRawText(), []);

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return new JsonNode(key, JsonNodeKind.Boolean, element.GetRawText(), []);

                default:
                    return new JsonNode(key, JsonNodeKind.Null, "null", []);
            }
        }

        /// <summary>Marks a container whose children ran past the budget.</summary>
        private static void Cut(List<JsonNode> children, int count)
        {
            if (children.Count < count)
            {
                children.Add(new JsonNode(
                    "…",
                    JsonNodeKind.More,
                    Strings.Format("ToolJsonMore", (count - children.Count).ToString("N0", CultureInfo.CurrentCulture)),
                    []));
            }
        }

        private static string Count(string open, int count, string close) =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1:N0} {2}", open, count, close);
    }
}
