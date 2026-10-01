using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DevDeck.App.ViewModels;
using Avalonia.Threading;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  The automation panel.
    /// </summary>
    /// <remarks>
    ///  No lifecycle here, unlike the Running and Clipboard panels. A watch that stopped when the
    ///  user looked at another page would be useless - the point of it is that it runs while they
    ///  are somewhere else - so the window starts and stops these, not the view.
    /// </remarks>
    public partial class AutomationView : UserControl
    {
        /// <summary>The chain whose output the view is following to the tail.</summary>
        private ChainItem? followed;

        private AutomationViewModel? model;

        private bool scrollQueued;

        public AutomationView()
        {
            InitializeComponent();

            TextMode.Wire(SelectChainOutput, ChainOutput, () => string.Join(
                Environment.NewLine, ChainOutput.Items.OfType<OutputLine>().Select(line => line.Text)));

            DataContextChanged += (_, _) =>
            {
                if (model is not null)
                {
                    model.PropertyChanged -= ModelChanged;
                }

                model = DataContext as AutomationViewModel;

                if (model is not null)
                {
                    model.PropertyChanged += ModelChanged;
                }

                Follow();
            };

            // The viewport rather than the scroll viewer's size: it is set later in the same layout
            // pass, so on SizeChanged it still holds the previous height.
            ChainEditor.PropertyChanged += (_, e) =>
            {
                if (e.Property == ScrollViewer.ViewportProperty)
                {
                    FitOutput();
                }
            };
            ChainForm.SizeChanged += (_, _) => FitOutput();
        }

        /// <summary>The shortest the chain output gets, however little room the section leaves it.</summary>
        private const double OutputMinHeight = 200;

        /// <summary>
        ///  Makes the chain output as tall as the editor has room for below the fields above it.
        /// </summary>
        /// <remarks>
        ///  Done here rather than with a star row: the editor is a scroll viewer, which measures
        ///  its content with unlimited height, so nothing inside it can fill the viewport by
        ///  layout alone. Below the floor the log stops shrinking and the editor scrolls instead.
        ///  The form is top-aligned so its bounds are its content: stretched, the scroll viewer
        ///  makes it at least as tall as the viewport, the empty space counts as "the fields above",
        ///  and the log never grows. Setting the height resizes the form and calls back in here, where the same sum comes
        ///  out and nothing changes, so it settles after one pass.
        /// </remarks>
        private void FitOutput()
        {
            double rest = ChainForm.Bounds.Height - ChainOutput.Bounds.Height;
            double height = Math.Max(OutputMinHeight, ChainEditor.Viewport.Height - rest);

            if (Math.Abs(ChainOutput.Height - height) > 0.5 || double.IsNaN(ChainOutput.Height))
            {
                ChainOutput.Height = height;
            }
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AutomationViewModel.SelectedChain))
            {
                Follow();
            }
        }

        /// <summary>
        ///  Keeps the chain output at its newest line, for whichever chain is selected.
        /// </summary>
        /// <remarks>
        ///  Re-hooked on every selection change, and the last one let go: another chain running
        ///  in the background goes on writing, and following it would scroll a list that is
        ///  showing something else.
        /// </remarks>
        private void Follow()
        {
            if (followed is not null)
            {
                followed.Output.CollectionChanged -= OutputChanged;
            }

            followed = model?.SelectedChain;

            if (followed is not null)
            {
                followed.Output.CollectionChanged += OutputChanged;
            }
        }

        private void OutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // The chain's log is cleared as a run starts. See CommandsView.Restart for why the list
            // starts over rather than being trusted to drop every old row.
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                CommandsView.Restart(ChainOutput);

                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Add || scrollQueued)
            {
                return;
            }

            scrollQueued = true;

            // After the new rows are measured, and once per burst rather than once per line.
            Dispatcher.UIThread.Post(
                () =>
                {
                    scrollQueued = false;

                    if (followed?.Output is { Count: > 0 } lines)
                    {
                        ChainOutput.ScrollIntoView(lines[^1]);
                    }
                },
                DispatcherPriority.Background);
        }

        /// <summary>Follows the grip under the chains section.</summary>
        /// <remarks>
        ///  Sets the current value rather than the property, so the binding to the saved height
        ///  stays in place underneath: the drop hands the result to the model, and from then on the
        ///  binding and the drag agree.
        /// </remarks>
        private void ChainsDragged(object? sender, VectorEventArgs e)
        {
            double height = Math.Clamp(
                ChainsBox.Bounds.Height + e.Vector.Y,
                AutomationViewModel.ChainsMinHeight,
                AutomationViewModel.ChainsMaxHeight);

            ChainsBox.SetCurrentValue(HeightProperty, height);
        }

        /// <summary>Remembers where the drag left the section - once, rather than per step.</summary>
        private void ChainsDropped(object? sender, VectorEventArgs e)
        {
            if (model is not null)
            {
                model.ChainsHeight = ChainsBox.Bounds.Height;
            }
        }

        /// <summary>Opens the file an output line names, as the deck's output does.</summary>
        private void LinkClick(object? sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is OutputLine { Link: { } link })
            {
                SourceLinks.Open(link);
            }
        }

        /// <summary>
        ///  Picks the folder the selected watch rule looks at.
        /// </summary>
        /// <remarks>
        ///  In the view rather than the view model for the usual reason: the picker is reached
        ///  through the top level that owns this control, which a view model has no way to.
        ///
        ///  Starts at the folder already set where there is one, so adjusting a path is a click
        ///  rather than a walk back down the tree to where it already was.
        /// </remarks>
        private async void BrowseWatchFolder(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not AutomationViewModel model
                || model.SelectedWatch is not { } watch
                || TopLevel.GetTopLevel(this) is not { } top)
            {
                return;
            }

            IStorageFolder? start = null;

            if (watch.Watching is { Length: > 0 } current && Directory.Exists(current))
            {
                start = await top.StorageProvider.TryGetFolderFromPathAsync(current);
            }

            IReadOnlyList<IStorageFolder> folders = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = Strings.Text("AutomationPickTheFolderToWatch"),
                    AllowMultiple = false,
                    SuggestedStartLocation = start,
                });

            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            {
                watch.Folder = path;
            }
        }
    }
}
