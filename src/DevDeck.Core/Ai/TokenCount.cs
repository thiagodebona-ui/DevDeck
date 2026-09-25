namespace DevDeck.Core
{
    /// <summary>What one exchange is reckoned to have cost.</summary>
    internal sealed record Spend(int Sent, int Received, decimal Money, bool Measured)
    {
        public int Total => Sent + Received;

        /// <summary>
        ///  What the readout says.
        /// </summary>
        /// <remarks>
        ///  The tilde is load-bearing: when the endpoint did not report usage these are estimates,
        ///  and a number presented as exact when it is not is worse than no number.
        /// </remarks>
        public string Describe()
        {
            string prefix = Measured ? string.Empty : "~";
            string tokens = $"{prefix}{Total:N0} tokens ({prefix}{Sent:N0} in, {prefix}{Received:N0} out)";

            return Money <= 0 ? tokens : $"{tokens} · {prefix}{Money:0.####} USD";
        }
    }

    /// <summary>
    ///  Reckons how much of a context window a conversation is using, and what it costs.
    /// </summary>
    /// <remarks>
    ///  Two questions this answers that nothing else in the app can. The first is why a long
    ///  conversation starts forgetting: every endpoint silently drops the oldest messages once the
    ///  window is full, and the only visible symptom is the model losing the thread. The second is
    ///  the bill - free locally, and not free on a hosted endpoint where a pasted file can cost
    ///  more than a day of ordinary questions.
    ///
    ///  Counting is approximate and says so. A real tokeniser is per-model, is a large dependency,
    ///  and would still be wrong for any endpoint the app has never heard of. Four characters per
    ///  token is the well-known rule of thumb for English prose and code, and is close enough for
    ///  the decision it informs - whether to start a new conversation - which is not a decision
    ///  that turns on five per cent.
    ///
    ///  Where the endpoint reports real usage, that is used instead, and the readout drops the
    ///  tilde to say so.
    /// </remarks>
    internal static class TokenCount
    {
        /// <summary>Characters per token, for the estimate.</summary>
        private const double PerToken = 4.0;

        /// <summary>What a message costs before any of its content, in tokens.</summary>
        /// <remarks>
        ///  Every chat format wraps each message in role markers and separators. Four is the figure
        ///  OpenAI published for theirs, and the others are close enough that the difference is
        ///  lost in the estimate anyway.
        /// </remarks>
        private const int PerMessage = 4;

        /// <summary>Roughly how many tokens this text is.</summary>
        public static int Of(string? text) => string.IsNullOrEmpty(text)
            ? 0
            : (int)Math.Ceiling(text.Length / PerToken);

        /// <summary>Roughly how many tokens a set of messages is, wrappers included.</summary>
        public static int Of(IEnumerable<ChatMessage> messages) =>
            messages.Sum(message => Of(message.Content) + PerMessage);

        /// <summary>
        ///  What a model's context window is, as far as the name gives it away.
        /// </summary>
        /// <remarks>
        ///  From the name rather than from the endpoint, because almost none of them report it. The
        ///  names are the convention the whole ecosystem uses - a number followed by "k" means the
        ///  window - so this reads what is there and returns nothing when it is not, rather than
        ///  guessing a default that would be wrong by a factor of ten either way.
        /// </remarks>
        public static int? WindowFor(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return null;
            }

            string name = model.ToLowerInvariant();

            // Longest first: "128k" must not be read as the "8k" inside it.
            foreach (int size in (int[])[1000, 256, 200, 128, 64, 32, 16, 8, 4])
            {
                if (name.Contains($"{size}k", StringComparison.Ordinal))
                {
                    return size * 1024;
                }
            }

            return name switch
            {
                var n when n.Contains("gpt-4o") || n.Contains("gpt-4.1") => 128 * 1024,
                var n when n.Contains("claude") => 200 * 1024,
                var n when n.Contains("gemini") => 1000 * 1024,
                var n when n.Contains("llama3") || n.Contains("llama-3") => 8 * 1024,
                _ => null,
            };
        }

        /// <summary>
        ///  Dollars per million tokens, in and out, for endpoints whose price is published.
        /// </summary>
        /// <remarks>
        ///  Prices change, and a stale number shown confidently is worse than none - so anything
        ///  not matched here reports no money at all rather than a figure from last year. The ones
        ///  listed are the widely-used defaults, and the readout is a scale check ("this
        ///  conversation is cents, not dollars") rather than an invoice.
        /// </remarks>
        public static (decimal In, decimal Out)? PriceFor(string baseUrl, string model)
        {
            // Local endpoints are free, and saying "0.0001 USD" about a model running on the user's
            // own processor would be nonsense rather than a rounding error.
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) && uri.IsLoopback)
            {
                return (0m, 0m);
            }

            string name = model.ToLowerInvariant();

            return name switch
            {
                var n when n.Contains("gpt-4o-mini") => (0.15m, 0.60m),
                var n when n.Contains("gpt-4o") => (2.50m, 10.00m),
                var n when n.Contains("haiku") => (0.80m, 4.00m),
                var n when n.Contains("sonnet") => (3.00m, 15.00m),
                var n when n.Contains("opus") => (15.00m, 75.00m),
                var n when n.Contains("gemini") && n.Contains("flash") => (0.075m, 0.30m),
                var n when n.Contains("gemini") => (1.25m, 5.00m),
                _ => null,
            };
        }

        /// <summary>Works out what an exchange cost, measured where possible and estimated where not.</summary>
        public static Spend Reckon(
            string baseUrl,
            string model,
            int sent,
            int received,
            bool measured)
        {
            decimal money = 0m;

            if (PriceFor(baseUrl, model) is { } price)
            {
                money = ((sent * price.In) + (received * price.Out)) / 1_000_000m;
            }

            return new Spend(sent, received, money, measured);
        }
    }
}
