using System.Diagnostics;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  One URL the Running page asks on every refresh, and what it last said.
    /// </summary>
    /// <remarks>
    ///  Any answer counts as "up" for the dot, and the colour then comes from the status code: a
    ///  500 is a service that is running and broken, which is a different problem from one that is
    ///  not running at all, and the two should not look the same.
    /// </remarks>
    internal sealed partial class HealthCheck : ObservableObject
    {
        /// <summary>Shared, as HttpClient is meant to be. Short, because a health check that hangs is down.</summary>
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5) };

        public HealthCheck(string url) => Url = url;

        public string Url { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGood), nameof(IsWarn), nameof(IsBad), nameof(IsWaiting))]
        private int? code;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGood), nameof(IsWarn), nameof(IsBad), nameof(IsWaiting))]
        private bool failed;

        [ObservableProperty]
        private string note = Strings.Text("RunHealthWaiting");

        [ObservableProperty]
        private bool checking;

        public bool IsWaiting => Code is null && !Failed;

        public bool IsGood => Code is >= 200 and < 400;

        public bool IsWarn => Code is >= 400 and < 500;

        public bool IsBad => Failed || Code >= 500;

        /// <summary>Asks the URL once. Never throws: a failure is the answer, not an error.</summary>
        public async Task Check()
        {
            if (Checking)
            {
                return;
            }

            Checking = true;

            try
            {
                if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri))
                {
                    Code = null;
                    Failed = true;
                    Note = Strings.Format("RunHealthBadUrl", Url);

                    return;
                }

                Stopwatch clock = Stopwatch.StartNew();

                using HttpResponseMessage response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

                clock.Stop();

                Failed = false;
                Code = (int)response.StatusCode;
                Note = Strings.Format("RunHealthMs", $"{Code} {response.ReasonPhrase}".Trim(), clock.ElapsedMilliseconds);
            }
            catch (Exception exception)
            {
                Code = null;
                Failed = true;
                Note = Strings.Format("RunHealthNoAnswer", Reason(exception));
            }
            finally
            {
                Checking = false;
            }
        }

        private static string Reason(Exception exception) => exception switch
        {
            TaskCanceledException => "timed out",
            HttpRequestException { InnerException: { } inner } => inner.Message,
            _ => exception.Message,
        };
    }

    /// <summary>A deck command that is running, as the Running page lists it.</summary>
    /// <remarks>
    ///  The start time is taken when this page first sees it running, not from the command: the
    ///  command keeps a stopwatch for its own status line but does not expose when it started, and
    ///  a guess that is at most one tick late is fine for "how long has this been going".
    /// </remarks>
    internal sealed partial class DeckActivity : ObservableObject
    {
        public DeckActivity(CommandItem command)
        {
            Command = command;
            Since = DateTime.Now;
        }

        public CommandItem Command { get; }

        public DateTime Since { get; }

        public string Name => Command.Name;

        [ObservableProperty]
        private string elapsed = "0:00";

        public void Tick()
        {
            TimeSpan taken = DateTime.Now - Since;

            Elapsed = taken.TotalHours >= 1
                ? taken.ToString(@"h\:mm\:ss")
                : taken.ToString(@"m\:ss");
        }
    }
}
