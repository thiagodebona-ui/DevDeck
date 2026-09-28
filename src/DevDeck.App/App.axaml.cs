using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DevDeck.App.ViewModels;
using DevDeck.App.Views;
using DevDeck.Core;

namespace DevDeck.App
{
    public partial class App : Application
    {
        /// <summary>
        ///  What this copy was started to do, set by <see cref="Program"/> before Avalonia runs.
        /// </summary>
        /// <remarks>
        ///  A static because the lifetime here is genuinely static: the request is known before any
        ///  object exists that could be handed it, and it is read exactly once, by the window that
        ///  is about to be built.
        /// </remarks>
        internal static LinkRequest Arrived { get; set; } = LinkRequest.Nothing;

        private static readonly object handoffLock = new();

        private static readonly Queue<string> early = new();

        private static Action<string>? deliver;

        /// <summary>
        ///  Takes a request from a later copy, holding it until the window exists.
        /// </summary>
        /// <remarks>
        ///  <see cref="Program"/> starts listening before Avalonia does, so a request can arrive
        ///  while there is no deck yet to hand it to.
        /// </remarks>
        internal static void Receive(string payload)
        {
            lock (handoffLock)
            {
                if (deliver is null)
                {
                    early.Enqueue(payload);

                    return;
                }
            }

            deliver(payload);
        }

        private static void Deliver(Action<string> to)
        {
            string[] waiting;

            lock (handoffLock)
            {
                deliver = to;
                waiting = early.ToArray();
                early.Clear();
            }

            foreach (string payload in waiting)
            {
                to(payload);
            }
        }

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Settings are read before the first window exists, because the theme has to be in
                // place before anything is drawn or the window flashes the wrong palette.
                AppSettings settings = AppSettings.Load();

                ThemeManager.Apply(this, AppTheme.Parse(settings.Theme));

                // For the same reason, and before the same first frame: a window built in English
                // and translated a moment later is a visible flicker on every start.
                Strings.Use(AppLanguage.Parse(settings.Language));

                // Explicit, because the main window hides itself rather than closing while the
                // floating widget is open. Under the default OnLastWindowClose that hide would be
                // read as the last window going and take the app down with it.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                MainWindowViewModel model = new(settings);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = model,
                };

                // After the window is assigned, so anything the request does - switching section,
                // starting a run - happens on a deck that is actually on screen.
                Dispatcher.UIThread.Post(() => model.Handle(Arrived));

                // Later copies hand their request here rather than opening a second window. Posted
                // onto the UI thread because it arrives on the pipe's own.
                void Arrive(string payload) =>
                    Dispatcher.UIThread.Post(() => model.Handle(DeepLink.Parse(payload)));

                // Program is already listening; from here on what arrives goes straight to the deck.
                Deliver(Arrive);

                // After Surface is wired by the window, since the menu's first entry uses it.
                Tray? tray = Tray.Attach(this, model);

                // Claiming the key is the app's job rather than the settings page's: what the key
                // does is raise a window, and a view model has none. Posted onto the UI thread
                // because the press arrives on the hotkey thread's own message loop.
                model.Preferences.ClaimHotkey = combination => GlobalHotkey.Register(
                    combination,
                    () => Dispatcher.UIThread.Post(() => model.Surface?.Invoke()));

                // Applied at startup rather than only when the tick box is touched. The setting
                // defaults to on and persists, so an app that only honoured it on a change would
                // do nothing at all for the user who set it once and never opened Settings again -
                // which is how the ported-but-inert version of this looked from outside.
                KeepAwake.Set(settings.KeepAwake, settings.StayAvailable);

                // The listener goes first so the pipe is free by the time the new copy looks for
                // it; the new copy also waits for this process to exit, which covers the rest.
                // Posted, so the status line and the dialog closing get to finish before the
                // window goes.
                model.Preferences.Restart = () =>
                {
                    SingleInstance.Stop();

                    if (!Relaunch.Start())
                    {
                        // Staying, so this copy is still the one that answers links.
                        SingleInstance.Listen(Arrive);

                        return false;
                    }

                    Dispatcher.UIThread.Post(() => desktop.Shutdown());

                    return true;
                };

                if (settings.Hotkey.Length > 0)
                {
                    // Best effort. A key that another application has taken since last time is a
                    // line in the log, not a dialog in front of a window the user has not seen yet.
                    model.Preferences.ClaimHotkey(settings.Hotkey);
                }

                desktop.ShutdownRequested += (_, _) =>
                {
                    tray?.Remove();
                    GlobalHotkey.Unregister();
                    SingleInstance.Stop();

                    // Before the process goes, because the macOS and Linux inhibitors are separate
                    // processes: left behind they would hold the machine awake with nothing on
                    // screen to explain why.
                    KeepAwake.Release();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
