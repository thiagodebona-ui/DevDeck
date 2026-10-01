using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The icon beside the clock, and the menu behind it.
    /// </summary>
    /// <remarks>
    ///  A deck of commands is a thing you reach for twenty times an hour and look at for four
    ///  seconds each time, which makes the walk to it the cost that matters. From the tray the
    ///  whole round trip is two clicks and the window never has to be found in the taskbar at all.
    ///
    ///  It also settles the awkwardness the main window had already run into: with the floating
    ///  widget open the window hides rather than closes, and until now nothing could bring it back
    ///  except the widget itself. The comment in <see cref="MainWindow"/> said as much - "a tray
    ///  icon that does not exist yet". It does now, and a hidden window always has a way home.
    ///
    ///  Built in code rather than declared in App.axaml because the menu is not static: the
    ///  favourite commands on it come from the user's own deck and change while the app runs. A
    ///  XAML NativeMenu would have to be rebuilt from code anyway, and then it would live in two
    ///  places instead of one.
    ///
    ///  <list type="bullet">
    ///   <item>Windows - a real notification-area icon.</item>
    ///   <item>macOS - a status item in the menu bar.</item>
    ///   <item>Linux - needs a StatusNotifierItem host, which most desktops have and a bare
    ///    window manager does not. Where there is none the icon simply does not appear, which is
    ///    why nothing here is the only way to reach anything.</item>
    ///  </list>
    /// </remarks>
    internal sealed class Tray
    {
        /// <summary>
        ///  How many commands the menu offers.
        /// </summary>
        /// <remarks>
        ///  A menu is not a deck. Past about half a dozen entries it stops being faster than
        ///  opening the window, which is the only reason it exists.
        /// </remarks>
        private const int Shortcuts = 6;

        private readonly MainWindowViewModel model;
        private readonly TrayIcon icon;
        private readonly NativeMenu menu = new();

        private Tray(MainWindowViewModel model)
        {
            this.model = model;

            icon = new TrayIcon
            {
                ToolTipText = "DevDeck",
                Menu = menu,
            };

            if (AssetLoader.Exists(new Uri("avares://DevDeck/Assets/DevDeck.ico")))
            {
                icon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DevDeck/Assets/DevDeck.ico")));
            }

            // Left click on Windows, and the obvious thing everywhere: the icon itself brings the
            // deck up. Nobody should have to open a menu to find "open".
            icon.Clicked += (_, _) => model.Surface?.Invoke();

            Fill();

            // The deck is editable while the app runs, so the menu is rebuilt when it changes
            // rather than captured once at startup and left to go stale.
            model.Commands.Commands.CollectionChanged += (_, _) => Fill();
        }

        /// <summary>Puts an icon in the tray, if the platform has one.</summary>
        /// <remarks>
        ///  Failure here is not worth interrupting anyone over: every route into the app that
        ///  existed before this still exists, so a desktop with no tray loses a convenience rather
        ///  than a capability.
        /// </remarks>
        public static Tray? Attach(Application application, MainWindowViewModel model)
        {
            try
            {
                Tray tray = new(model);

                TrayIcon.SetIcons(application, [tray.icon]);

                return tray;
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("tray", "Could not put an icon in the tray", exception);

                return null;
            }
        }

        /// <summary>Takes the icon down, so it does not linger after the app has gone.</summary>
        /// <remarks>
        ///  Windows in particular keeps drawing a dead icon until something makes it repaint, and
        ///  a tray full of ghosts is the classic sign of an app that did not tidy up.
        /// </remarks>
        public void Remove()
        {
            try
            {
                icon.IsVisible = false;
                icon.Dispose();
            }
            catch (Exception)
            {
                // Shutting down. There is nobody left to tell.
            }
        }

        private void Fill()
        {
            menu.Items.Clear();

            menu.Items.Add(Entry("Open DevDeck", () => model.Surface?.Invoke()));

            IReadOnlyList<CommandItem> shortcuts = Pick();

            if (shortcuts.Count > 0)
            {
                menu.Items.Add(new NativeMenuItemSeparator());

                foreach (CommandItem command in shortcuts)
                {
                    // Captured by reference on purpose: renaming a command should not leave a menu
                    // entry pointing at the old name, and CommandItem.Run reads its own name.
                    CommandItem chosen = command;

                    menu.Items.Add(Entry($"Run {chosen.Name}", () =>
                    {
                        // Surfaced first, because a command that asks for a parameter needs a
                        // window to ask in, and one that fails needs somewhere to say so.
                        model.Surface?.Invoke();
                        chosen.RunCommand.Execute(null);
                    }));
                }
            }

            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(Entry("Quit", Quit));
        }

        /// <summary>
        ///  The commands worth a menu entry.
        /// </summary>
        /// <remarks>
        ///  The most-run ones, which is the only ordering that needs no maintenance: a deck used
        ///  for a week arranges its own shortcuts. Falls back to the first few in the deck when
        ///  there is no history yet, so a fresh install still has something on the menu.
        /// </remarks>
        private IReadOnlyList<CommandItem> Pick()
        {
            List<CommandItem> commands = [.. model.Commands.Commands];

            List<CommandItem> run = [.. commands
                .Where(command => RunHistory.Instance.For(command.Name).Count > 0)
                .OrderByDescending(command => RunHistory.Instance.For(command.Name).Count)
                .Take(Shortcuts)];

            return run.Count > 0 ? run : [.. commands.Take(Shortcuts)];
        }

        private static NativeMenuItem Entry(string header, Action clicked)
        {
            NativeMenuItem item = new(header);

            item.Click += (_, _) => clicked();

            return item;
        }

        /// <summary>
        ///  Ends the app from the menu.
        /// </summary>
        /// <remarks>
        ///  Posted, not done here. This runs inside the click handler of the icon's own menu, and
        ///  on Windows that menu is still in its native modal loop at this point: disposing the
        ///  icon underneath it and shutting the dispatcher down from inside it froze the app with
        ///  its window still on screen. Once the menu has closed, the lifetime's
        ///  ShutdownRequested takes the icon down along with everything else.
        /// </remarks>
        private static void Quit()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Dispatcher.UIThread.Post(() => desktop.Shutdown(), DispatcherPriority.Background);
            }
        }
    }
}
