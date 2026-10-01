using System.Globalization;
using Avalonia;
using DevDeck.Core;

namespace DevDeck.App
{
    internal static class Program
    {
        /// <summary>
        ///  The entry point.
        /// </summary>
        /// <remarks>
        ///  Avalonia wants initialisation to happen here and nowhere else - before any control is
        ///  touched - so this stays free of app logic, with one exception.
        ///
        ///  That exception is the elevated helper. When the app hands its administrator-only
        ///  cleanup steps to a second copy of itself, that copy arrives here with
        ///  <see cref="Elevation.Switch"/> on its command line, does the work, and exits without
        ///  ever building a UI. It has to be caught before Avalonia starts: a helper that opened a
        ///  window would put a second DevDeck on the taskbar for the second it exists.
        ///
        ///  The other exception is the single-instance check, which has to come before the window
        ///  for the same reason: a second copy started by a link should hand its request over and
        ///  exit, not flash a window on its way to doing so.
        /// </remarks>
        [STAThread]
        public static int Main(string[] args)
        {
            if (Helper(args) is { } code)
            {
                return code;
            }

            // A copy started by a reset waits for the one it replaces, or the single-instance check
            // below would find that copy still listening and hand it a "show" instead of opening.
            args = Relaunch.AwaitPredecessor(args);

            // Taken off before the link parser sees it, which would otherwise read it as a request
            // for nothing in particular and be right only by accident.
            args = AutoStart.Strip(args, out bool atSignIn);
            App.StartMinimised = atSignIn;

            // Whatever this copy was asked to do, before deciding whether this copy is the one that
            // will do it.
            LinkRequest request = DeepLink.FromArguments(args);

            if (SingleInstance.Handoff(request.IsSomething ? Line(request) : Wake))
            {
                // Someone is already running: they have the request now, and a second window would
                // be two decks editing one settings file.
                return 0;
            }

            App.Arrived = request;

            // Listening now, not once the window exists. A first launch can take seconds to draw
            // anything, and a user who clicks again in that gap would otherwise find nobody
            // listening and start a second deck. What arrives early waits in App until the window
            // is there to act on it.
            SingleInstance.Listen(App.Receive);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            return 0;
        }

        /// <summary>What a copy with nothing to ask for sends: just bring the window up.</summary>
        public const string Wake = DeepLink.Prefix + "show";

        /// <summary>
        ///  Re-renders a request as a link, for handing to the copy that is already running.
        /// </summary>
        /// <remarks>
        ///  Through the link form rather than by passing the arguments along, so the receiving side
        ///  has one shape to parse however the request was originally phrased.
        /// </remarks>
        private static string Line(LinkRequest request)
        {
            string link = DeepLink.For(request.Intent, request.Target);

            if (request.Values.Count == 0)
            {
                return link;
            }

            string query = string.Join('&', request.Values.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

            return $"{link}?{query}";
        }

        /// <summary>
        ///  Runs the privileged steps and reports the mask, or null when this is a normal start.
        /// </summary>
        /// <remarks>
        ///  Returns zero rather than throwing on a malformed mask. This process was started by the
        ///  app itself, so a bad argument is a bug rather than user input - and an elevated process
        ///  that crashes with a dialog is a worse way to find out than an empty result.
        /// </remarks>
        private static int? Helper(string[] args)
        {
            if (args.Length < 2 || args[0] != Elevation.Switch)
            {
                return null;
            }

            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mask))
            {
                return 0;
            }

            string? resultPath = args.Length >= 3 ? args[2] : null;

            return Elevation.RunPrivilegedSteps(mask, resultPath);
        }

        /// <summary>Also called by the XAML previewer, which is why it is public and parameterless.</summary>
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
