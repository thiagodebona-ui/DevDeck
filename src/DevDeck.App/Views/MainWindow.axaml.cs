using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using DevDeck.App.ViewModels;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // The rail is the user's own list of places: the ones they use most belong at the top.
            ListReorder.Attach(Rail, (item, to) => (DataContext as MainWindowViewModel)?.MoveSection((Section)item, to));

            // The watches have to start whether or not their panel is ever looked at, and the
            // notification needs a window to fall back to - both of which are the window's job
            // rather than the automation view's.
            DataContextChanged += (_, _) =>
            {
                if (DataContext is MainWindowViewModel model)
                {
                    model.Announce = Announce;
                    model.Surface = Surface;

                    // A watch firing reaches the user the same way a finished run does. It is the
                    // same situation from the other end: something happened in this app while the
                    // user was looking at another one.
                    model.Automation.Changed = Announce;

                    model.Automation.Start();
                }
            };

            // Ctrl+K and Ctrl+P, because muscle memory for this is split between editors and
            // launchers and neither key is doing anything else here.
            KeyDown += (_, key) =>
            {
                if (key.KeyModifiers != KeyModifiers.Control || key.Key is not (Key.K or Key.P))
                {
                    return;
                }

                if (DataContext is MainWindowViewModel model)
                {
                    Palette.Show(this, new PaletteViewModel(model.Actions()));
                    key.Handled = true;
                }
            };
        }

        /// <summary>
        ///  Says that a long command has finished, wherever this platform can say it.
        /// </summary>
        /// <remarks>
        ///  The system's own notification first, because the user is by definition looking at
        ///  another window. Where there is no channel for that - Windows, without the application
        ///  identity a real toast needs - the fallback is to make this window ask for attention:
        ///  a taskbar button that flashes is at least visible from wherever they are.
        /// </remarks>
        /// <summary>
        ///  Brings this window up, for a link that arrived while something else had the screen.
        /// </summary>
        /// <remarks>
        ///  Show() first because the window may be hidden rather than merely behind - it hides
        ///  itself while the floating widget is out - and a window that is not shown cannot be
        ///  activated.
        ///
        ///  Topmost is toggled rather than set: several window managers only let the foreground
        ///  application raise a window, and this one is not it. Leaving Topmost on afterwards would
        ///  pin the deck over everything else for the rest of the session, which is not what asking
        ///  for a window is asking for.
        /// </remarks>
        /// <summary>
        ///  Set once this window has really closed, which during a sign-out or shutdown it does
        ///  while hidden. A closed window throws if shown, so anything still asking for it - the
        ///  tray, the hotkey, a link arriving late - has to be told no rather than crash the app
        ///  on the way out.
        /// </summary>
        private bool closed;

        protected override void OnClosed(EventArgs e)
        {
            closed = true;
            base.OnClosed(e);
        }

        private void Surface()
        {
            if (closed)
            {
                return;
            }

            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            bool pinned = Topmost;

            Topmost = true;
            Activate();
            Topmost = pinned;
        }

        private void Announce(string title, string body)
        {
            if (Notifier.Send(title, body))
            {
                return;
            }

            if (TaskbarFlash.Ask(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero))
            {
                return;
            }

            // Everything else has been tried, so leave it where it will be seen the moment the
            // window is looked at again rather than doing nothing at all.
            if (DataContext is MainWindowViewModel model)
            {
                model.Announcement = $"{title} - {body}";
            }
        }

        /// <summary>
        ///  Hides rather than closes while the widget is floating.
        /// </summary>
        /// <remarks>
        ///  The widget is not in the taskbar, so closing this window with one open used to leave
        ///  the app running with nothing on screen that could bring it back - no window, no taskbar
        ///  button, and a tray icon that does not exist yet.
        ///
        ///  Hiding keeps the window alive so it can be shown again from the widget's menu; a closed
        ///  Avalonia window cannot be reopened, which is why this is not simply a re-Show. With no
        ///  widget open there is nothing to come back to, so the close is a real one and the app
        ///  shuts down.
        /// </remarks>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (e.Cancel || e.CloseReason != WindowCloseReason.WindowClosing)
            {
                return;
            }

            if (DataContext is MainWindowViewModel { Memory.IsWidgetOpen: true })
            {
                e.Cancel = true;
                Hide();

                return;
            }

            // Nothing left to keep the app alive, and the lifetime is set to explicit shutdown, so
            // say so outright rather than relying on the last window going.
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}
