using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One header row, observable so the grid can edit it.</summary>
    /// <remarks>
    ///  A wrapper rather than making <see cref="HeaderLine"/> itself observable: the Core type is
    ///  what gets serialised and what the sender reads, and giving it change notification would mean
    ///  the MVVM toolkit reaching into a project that deliberately has no UI dependency.
    /// </remarks>
    internal sealed partial class HeaderRow : ObservableObject
    {
        public HeaderRow(HeaderLine line)
        {
            name = line.Name;
            value = line.Value;
            enabled = line.Enabled;
        }

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string value;

        [ObservableProperty]
        private bool enabled;

        public HeaderLine ToLine() => new() { Name = Name, Value = Value, Enabled = Enabled };
    }

    /// <summary>
    ///  One entry on the "move to an existing group" menu.
    /// </summary>
    /// <remarks>
    ///  A name would have been enough if the menu items could reach the view model, and they
    ///  cannot: an item generated from an ItemsSource is not in the visual tree of the menu that
    ///  owns it at the moment its style is applied, so a binding walking up to find an ancestor
    ///  resolves against nothing. Carrying the command alongside the name is what makes each
    ///  generated item self-contained.
    /// </remarks>
    internal sealed class GroupChoice
    {
        public GroupChoice(string name, System.Windows.Input.ICommand file)
        {
            Name = name;
            File = file;
        }

        public string Name { get; }

        /// <summary>Takes the group's name as its parameter.</summary>
        public System.Windows.Input.ICommand File { get; }
    }

    /// <summary>One group in the request list, with the requests filed under it.</summary>
    /// <remarks>
    ///  A view-model shape rather than a stored one: the grouping lives on each request as a name,
    ///  and this is that arrangement made drawable. Nothing here is serialised, which is what
    ///  keeps a request the single place its group is recorded - two places would eventually
    ///  disagree, and the one on screen would win.
    /// </remarks>
    internal sealed partial class RequestGroup : ObservableObject
    {
        public RequestGroup(
            string name, IReadOnlyList<HttpRequest> requests, bool collapsed, string icon)
        {
            Name = name;
            Requests = [.. requests];
            this.collapsed = collapsed;
            Icon = icon;
        }

        public string Name { get; }

        /// <summary>The icon's name, from settings. Empty for none.</summary>
        public string Icon { get; }

        /// <summary>The shape to draw, or null - which is what hides the Path.</summary>
        public Geometry? Glyph => Icons.Pick(Icon)?.Shape;

        /// <summary>The icon's colour and the wash inside it.</summary>
        public IBrush? Ink => Icons.Pick(Icon)?.Ink;

        public IBrush? Wash => Icons.Pick(Icon)?.Fill;

        public bool HasIcon => Glyph is not null;

        public ObservableCollection<HttpRequest> Requests { get; }

        /// <summary>Whether the group is shut, which the view persists through the model.</summary>
        [ObservableProperty]
        private bool collapsed;

        /// <summary>"Billing (4)" - the count is what makes a collapsed group still informative.</summary>
        /// <summary>
        ///  The name alone. The count used to be part of this and is now <see cref="Tally"/>,
        ///  because the header draws the two at different weights and a single string cannot be
        ///  half bold.
        /// </summary>
        public string Label => Name;

        /// <summary>How many requests are in the group.</summary>
        public string Tally => Requests.Count.ToString(CultureInfo.InvariantCulture);

        public bool Expanded => !Collapsed;

        partial void OnCollapsedChanged(bool value) => OnPropertyChanged(nameof(Expanded));
    }

    /// <summary>
    ///  Send a request, read the reply.
    /// </summary>
    /// <remarks>
    ///  The companion to the deck rather than a replacement for a real API client: the command that
    ///  just started a server is two panels away, and the question after starting one is always
    ///  whether it answers. Saved requests persist so that question survives a restart.
    ///
    ///  Sending is cancellable because the thing being called is usually something that has just
    ///  started and may not be listening yet, and a sixty-second wait with no way out is the wrong
    ///  behaviour for a panel you are using to debug exactly that.
    /// </remarks>
    internal sealed partial class HttpViewModel : ObservableObject
    {
        private readonly AppSettings settings;

        private CancellationTokenSource? inFlight;

        public HttpViewModel(AppSettings settings)
        {
            this.settings = settings;

            Saved = new ObservableCollection<HttpRequest>(settings.Requests);

            if (Saved.Count == 0)
            {
                Saved.Add(new HttpRequest
                {
                    Name = Strings.Text("HttpNewRequestName"),
                    Url = "http://localhost:5000/health",
                });
            }

            selected = Saved[0];
            Load(selected);

            Environments = new ObservableCollection<HttpEnvironment>(settings.Environments);

            // By name rather than by index, so adding or reordering environments cannot silently
            // change which one is active. A name that has gone selects none, which leaves every
            // variable visibly unresolved instead of sending somewhere nobody chose.
            activeEnvironment = Environments.FirstOrDefault(environment =>
                string.Equals(environment.Name, settings.ActiveEnvironment, StringComparison.Ordinal));

            RestoreLayout();
            Regroup();
        }

        public ObservableCollection<HttpEnvironment> Environments { get; }

        /// <summary>The saved requests arranged by group, which is what the tree draws.</summary>
        public ObservableCollection<RequestGroup> Groups { get; } = [];

        public ObservableCollection<HttpRequest> Saved { get; }

        public ObservableCollection<HeaderRow> Headers { get; } = [];

        public ObservableCollection<HeaderLine> ResponseHeaders { get; } = [];

        /// <summary>The rows actually drawn: <see cref="ResponseHeaders"/> after the filter.</summary>
        /// <remarks>
        ///  A second collection rather than a filtered view over the first, because the pane wants
        ///  the unfiltered count as well - "3 of 24" is only sayable if both numbers are to hand.
        /// </remarks>
        public ObservableCollection<HeaderLine> ShownHeaders { get; } = [];

        /// <summary>What the filter box holds.</summary>
        [ObservableProperty]
        private string headerFilter = string.Empty;

        partial void OnHeaderFilterChanged(string value) => FilterHeaders();

        /// <summary>How many headers came back, as the pane's own heading.</summary>
        public string HeaderCount =>
            ResponseHeaders.Count == 1
                ? Strings.Text("HttpHeaderCountOne")
                : Strings.Format("HttpHeaderCount", ResponseHeaders.Count);

        /// <summary>True when the reply had headers but none of them match the filter.</summary>
        public bool NothingMatches => ResponseHeaders.Count > 0 && ShownHeaders.Count == 0;

        /// <summary>True when the reply had no headers at all.</summary>
        public bool NoHeadersAtAll => ResponseHeaders.Count == 0;

        /// <summary>
        ///  Rebuilds <see cref="ShownHeaders"/> from the filter.
        /// </summary>
        /// <remarks>
        ///  Sorted by name rather than kept in arrival order. The order a server sends its headers
        ///  in carries no meaning worth preserving, and a reply with thirty of them is a list that
        ///  gets read by hunting for one name.
        ///
        ///  The filter matches the name or the value, because half the time what is being looked
        ///  for is a value - a cookie's domain, a cache directive - and only the person typing
        ///  knows which side of the colon they mean.
        /// </remarks>
        private void FilterHeaders()
        {
            string wanted = HeaderFilter.Trim();

            ShownHeaders.Clear();

            IEnumerable<HeaderLine> kept = wanted.Length == 0
                ? ResponseHeaders
                : ResponseHeaders.Where(header =>
                    header.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                    || header.Value.Contains(wanted, StringComparison.OrdinalIgnoreCase));

            foreach (HeaderLine header in kept.OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase))
            {
                ShownHeaders.Add(header);
            }

            OnPropertyChanged(nameof(HeaderCount));
            OnPropertyChanged(nameof(NothingMatches));
            OnPropertyChanged(nameof(NoHeadersAtAll));
        }

        /// <summary>Puts one header on the clipboard, in the form it arrived in.</summary>
        [RelayCommand]
        private void CopyHeader(HeaderLine? header)
        {
            if (header is not null)
            {
                Copy?.Invoke($"{header.Name}: {header.Value}");
            }
        }

        public IReadOnlyList<string> Methods => Http.Methods;

        [ObservableProperty]
        private HttpRequest selected;

        [ObservableProperty]
        private string method = "GET";

        [ObservableProperty]
        private string url = string.Empty;

        [ObservableProperty]
        private string body = string.Empty;

        [ObservableProperty]
        private string response = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasResult))]
        [NotifyPropertyChangedFor(nameof(ResponseIsStructured))]
        [NotifyPropertyChangedFor(nameof(ResponseKind))]
        [NotifyPropertyChangedFor(nameof(ResponseBodyKind))]
        [NotifyPropertyChangedFor(nameof(ResponseIsImage))]
        [NotifyPropertyChangedFor(nameof(ResponseIsMedia))]
        [NotifyPropertyChangedFor(nameof(ResponseIsText))]
        [NotifyPropertyChangedFor(nameof(ShowingBodyText))]
        [NotifyPropertyChangedFor(nameof(ShowingImage))]
        [NotifyPropertyChangedFor(nameof(ShowingMedia))]
        [NotifyPropertyChangedFor(nameof(ResponseImage))]
        [NotifyCanExecuteChangedFor(nameof(SaveResponseCommand))]
        private HttpResult? result;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        private bool sending;

        [ObservableProperty]
        private string status = string.Empty;

        /// <summary>Which half of the reply is on screen: the body or its headers.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowingBodyText))]
        [NotifyPropertyChangedFor(nameof(ShowingImage))]
        [NotifyPropertyChangedFor(nameof(ShowingMedia))]
        private bool showingHeaders;

        /// <summary>
        ///  The environment whose values <c>{{name}}</c> resolves from, or none.
        /// </summary>
        /// <remarks>
        ///  None is a first-class choice rather than a broken state: someone who has not set an
        ///  environment up gets their request sent exactly as typed, which is what the panel did
        ///  before environments existed.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EnvironmentNote))]
        [NotifyPropertyChangedFor(nameof(HasUnresolved))]
        [NotifyPropertyChangedFor(nameof(HasEnvironment))]
        private HttpEnvironment? activeEnvironment;

        partial void OnActiveEnvironmentChanged(HttpEnvironment? value)
        {
            if (loading)
            {
                return;
            }

            settings.ActiveEnvironment = value?.Name ?? string.Empty;
            settings.Save();
        }

        public bool HasEnvironment => ActiveEnvironment is not null;

        /// <summary>Goes back to sending requests exactly as they are written.</summary>
        /// <remarks>
        ///  A command rather than an entry in the list, because a ComboBox with a null item in it
        ///  draws a blank row that reads as a bug. The button is only there while there is
        ///  something to clear.
        /// </remarks>
        [RelayCommand]
        private void ClearEnvironment() => ActiveEnvironment = null;

        /// <summary>
        ///  Replaces the environments with an edited set, after the editor closes.
        /// </summary>
        /// <remarks>
        ///  The active one is found again by name rather than kept by reference, because the
        ///  editor may have renamed it - and a reference that survived the rename would leave the
        ///  picker showing a name that is no longer in the list.
        /// </remarks>
        public void TakeEnvironments(IReadOnlyList<HttpEnvironment> edited)
        {
            string wanted = ActiveEnvironment?.Name ?? settings.ActiveEnvironment;

            Environments.Clear();

            foreach (HttpEnvironment environment in edited)
            {
                Environments.Add(environment);
            }

            settings.Environments = [.. edited];

            ActiveEnvironment = Environments.FirstOrDefault(environment =>
                string.Equals(environment.Name, wanted, StringComparison.Ordinal));

            settings.ActiveEnvironment = ActiveEnvironment?.Name ?? string.Empty;
            settings.Save();

            // The note is about this request against the new values, and neither changed in a way
            // the property system would have noticed on its own.
            OnPropertyChanged(nameof(EnvironmentNote));
            OnPropertyChanged(nameof(HasUnresolved));
        }

        /// <summary>
        ///  Writes the environments back after something outside the editor changed one.
        /// </summary>
        /// <remarks>
        ///  Only the import path needs this, and only because it writes into an environment rather
        ///  than through the editor dialog. The list held in settings is the same set of objects
        ///  this view model shows, so there is nothing to copy across - the value has already
        ///  changed and what is missing is the save.
        /// </remarks>
        private void SaveEnvironments()
        {
            settings.Environments = [.. Environments];
            settings.Save();

            OnPropertyChanged(nameof(EnvironmentNote));
            OnPropertyChanged(nameof(HasUnresolved));
        }

        /// <summary>
        ///  The variables this request uses that the chosen environment cannot supply.
        /// </summary>
        /// <remarks>
        ///  Read live rather than cached, because it has to answer for the request as it is being
        ///  typed. Cheap: a handful of small regex passes over strings already in memory.
        /// </remarks>
        public IReadOnlyList<string> Unresolved =>
            HttpVariables.Missing(Current(), ActiveEnvironment);

        public bool HasUnresolved => Unresolved.Count > 0;

        /// <summary>What the panel says under the address bar about variables.</summary>
        public string EnvironmentNote
        {
            get
            {
                IReadOnlyList<string> missing = Unresolved;

                if (missing.Count == 0)
                {
                    return string.Empty;
                }

                // Named rather than counted. The usual cause is a misspelling or a variable defined
                // in the other environment, and both are obvious the moment the name is on screen.
                string names = string.Join(", ", missing.Select(name => $"{{{{{name}}}}}"));

                return ActiveEnvironment is null
                    ? Strings.Format("HttpNoEnvironmentFor", names)
                    : Strings.Format("HttpEnvironmentMissing", ActiveEnvironment.Name, names);
            }
        }

        public bool HasResult => Result is not null;

        /// <summary>
        ///  The smallest a pane may be dragged to.
        /// </summary>
        /// <remarks>
        ///  Enough to see that something is there and to get hold of the splitter again. A pane
        ///  that can be dragged to nothing is one the user has to guess the location of to get
        ///  back, and a resize that cannot be undone is worse than no resize.
        /// </remarks>
        public double PaneFloor => 44;

        /// <summary>
        ///  How tall the header list is.
        /// </summary>
        /// <remarks>
        ///  Ten rows to begin with, which is where a hand-written request stops and a captured one
        ///  begins: two or three headers typed by hand, twenty or thirty imported from a browser's
        ///  devtools. Left unbounded the imported case pushes the body and the whole response pane
        ///  off the bottom, so the reply - the thing the request was sent to see - is what goes
        ///  missing.
        ///
        ///  A height the user owns rather than a ceiling they do not, because the right answer is
        ///  the one they are working on today: editing thirty headers wants this pane tall, and
        ///  reading a long document back wants it short.
        /// </remarks>
        [ObservableProperty]
        private double headerPaneHeight;

        /// <summary>
        ///  How tall the request body box is. Saved, for the reason above.
        /// </summary>
        /// <remarks>
        ///  The reply below it has no height of its own: it takes what these two leave, so that a
        ///  taller window gives its extra room to the reply rather than to a box the user has
        ///  already sized. Both splitters still resize it, from either side.
        /// </remarks>
        [ObservableProperty]
        private double bodyPaneHeight;

        /// <summary>Header rows shown before the list starts scrolling, on a fresh install.</summary>
        private const int Rows = 10;

        /// <summary>One row: the text box plus the 4px margin under it in the template.</summary>
        private const double RowHeight = 36;

        /// <summary>The request body's height on a fresh install.</summary>
        private const double BodyDefault = 120;

        partial void OnHeaderPaneHeightChanged(double value) => KeepLayout();

        partial void OnBodyPaneHeightChanged(double value) => KeepLayout();

        /// <summary>
        ///  Writes the three heights back to settings.
        /// </summary>
        /// <remarks>
        ///  Set rather than saved: a drag raises this on every frame of the pointer's travel, and
        ///  writing the settings file each time would be a few hundred writes for one gesture. The
        ///  file is written when the window closes, along with everything else.
        ///
        ///  Floored on the way out as well as in the view, because a pane collapsed by a window
        ///  that was made very short should not be what is restored next time.
        /// </remarks>
        private void KeepLayout()
        {
            if (loading)
            {
                return;
            }

            settings.HttpHeaderPaneHeight = Math.Max(PaneFloor, HeaderPaneHeight);
            settings.HttpBodyPaneHeight = Math.Max(PaneFloor, BodyPaneHeight);
        }

        /// <summary>
        ///  Takes the saved layout, or the defaults on a fresh install.
        /// </summary>
        /// <remarks>
        ///  A zero means the setting was never written, which is a different thing from a pane the
        ///  user dragged shut - and the floor is applied to a saved value so that a file carrying a
        ///  collapsed pane from an older build still opens onto something usable.
        /// </remarks>
        private void RestoreLayout()
        {
            HeaderPaneHeight = Pick(settings.HttpHeaderPaneHeight, Rows * RowHeight);
            BodyPaneHeight = Pick(settings.HttpBodyPaneHeight, BodyDefault);
            TreeWidth = settings.HttpTreeWidth > 0
                ? Math.Max(TreeFloor, settings.HttpTreeWidth)
                : TreeDefault;
        }

        /// <summary>The narrowest the request list may be dragged.</summary>
        /// <remarks>
        ///  Enough for a colour flag and a few characters of name. Narrower than this and the list
        ///  is a column of swatches nobody can read, with no way back but guessing where the
        ///  splitter went.
        /// </remarks>
        public double TreeFloor => 120;

        private const double TreeDefault = 190;

        /// <summary>
        ///  How wide the saved-request list is.
        /// </summary>
        /// <remarks>
        ///  Grouping made this worth owning: a flat list of six fits in any width, where groups,
        ///  indents and longer names do not, and the right width is a property of how the list has
        ///  been organised rather than something the app can pick once.
        /// </remarks>
        [ObservableProperty]
        private double treeWidth;

        partial void OnTreeWidthChanged(double value)
        {
            if (loading)
            {
                return;
            }

            settings.HttpTreeWidth = Math.Max(TreeFloor, value);
        }

        private double Pick(double saved, double fallback) =>
            saved > 0 ? Math.Max(PaneFloor, saved) : fallback;

        /// <summary>Whether the reply is drawn with colour rather than as flat text.</summary>
        public bool ResponseIsStructured => Result?.IsStructured == true;

        /// <summary>"JSON", "HTML", "image/png" - what the reply turned out to be.</summary>
        public string ResponseKind => Result?.BodyLabel ?? string.Empty;

        /// <summary>Which highlighter the response pane uses, if any.</summary>
        public BodyKind ResponseBodyKind => Result?.Kind ?? BodyKind.Plain;

        /// <summary>Whether the reply is an image, and so is drawn rather than printed.</summary>
        public bool ResponseIsImage => Result?.Kind == BodyKind.Image;

        /// <summary>Whether the reply is audio or video.</summary>
        public bool ResponseIsMedia => Result?.Kind == BodyKind.Media;

        /// <summary>
        ///  Whether the reply goes in the text pane, which is everything that is not drawn.
        /// </summary>
        /// <remarks>
        ///  A binary that is neither an image nor playable still goes here: what the text pane
        ///  shows for one is the description the sender wrote, which is the useful thing to say.
        /// </remarks>
        public bool ResponseIsText => !ResponseIsImage && !ResponseIsMedia;

        /// <summary>Which of the four response panes is the one on screen.</summary>
        /// <remarks>
        ///  Worked out here rather than as a stack of conditions in the XAML. Four panes share one
        ///  grid cell and exactly one of them may be visible; expressed as bindings in the markup
        ///  that is four expressions that have to stay mutually exclusive by inspection, and the
        ///  first one that does not is two panes drawn on top of each other.
        /// </remarks>
        public bool ShowingBodyText => !ShowingHeaders && ResponseIsText;

        public bool ShowingImage => !ShowingHeaders && ResponseIsImage;

        public bool ShowingMedia => !ShowingHeaders && ResponseIsMedia;

        /// <summary>
        ///  The reply as a bitmap, or null when it is not one or will not decode.
        /// </summary>
        /// <remarks>
        ///  Decoded here rather than in the view so that a corrupt or unsupported image is a blank
        ///  pane and a description rather than an exception on the UI thread - Avalonia's decoder
        ///  throws on a truncated PNG, and a truncated PNG is exactly what a cancelled download
        ///  leaves behind. A format Avalonia has no decoder for - AVIF, most TIFFs - lands in the
        ///  same place, and the pane falls back to saying what it was and how big.
        ///
        ///  Rebuilt on each access rather than cached because it is read once per response, by one
        ///  binding, and a cache would need invalidating on every send.
        /// </remarks>
        public Bitmap? ResponseImage
        {
            get
            {
                if (Result is not { Kind: BodyKind.Image, Raw: { Length: > 0 } bytes })
                {
                    return null;
                }

                try
                {
                    using MemoryStream stream = new(bytes);

                    return new Bitmap(stream);
                }
                catch (Exception)
                {
                    // Every decoder failure, on purpose. The list of what a platform image decoder
                    // can throw is not documented and differs by platform, and there is nothing to
                    // do about any of them except show the description instead.
                    return null;
                }
            }
        }

        /// <summary>Set by the view, to ask where a response should be written.</summary>
        /// <remarks>
        ///  A callback for the same reason the name and group prompts are: a save dialog needs a
        ///  window to be modal to, and a view model does not have one. It is handed the name to
        ///  suggest and answers with the full path chosen, or null if the user thought better of
        ///  it.
        /// </remarks>
        public Func<string, Task<string?>>? AskWhereToSave { get; set; }

        /// <summary>Set by the view, for the two clipboard buttons.</summary>
        public Func<string, Task>? Copy { get; set; }

        public Func<Task<string?>>? Paste { get; set; }

        partial void OnSelectedChanged(HttpRequest value) => Load(value);

        partial void OnMethodChanged(string value) => Keep(request => request.Method = value);

        /// <summary>
        ///  Takes a URL, or notices that what arrived was a whole curl command.
        /// </summary>
        /// <remarks>
        ///  Pasting a curl into the address box is what people try first, because it is where the
        ///  address is and the clipboard holds one thing. Postman reads it there and so does this:
        ///  the alternative is a box that accepts twenty lines of shell as a URL and then reports a
        ///  connection error for it, which teaches nothing about where the import button was.
        ///
        ///  What arrives becomes a request of its own - see <see cref="Take"/>.
        /// </remarks>
        partial void OnUrlChanged(string value)
        {
            if (!loading && Looks(value) && Http.FromCurl(value) is { } parsed)
            {
                Take(parsed);

                return;
            }

            Keep(request => request.Url = value);

            // The unresolved-variable note is about the text in the box, so it is re-read here
            // rather than only when the environment changes.
            OnPropertyChanged(nameof(EnvironmentNote));
            OnPropertyChanged(nameof(HasUnresolved));
        }

        /// <summary>
        ///  Takes what was pasted into the address box, and reports whether it was a curl.
        /// </summary>
        /// <remarks>
        ///  The view calls this before the TextBox sees the paste, which is the only place it can
        ///  be caught intact. A curl command copied out of devtools is several lines joined by
        ///  backslashes, and the address box is single-line: by the time the text has been through
        ///  it, the newlines are gone and with them the boundary between the last header and the
        ///  next flag, so the parser gets a mangled command and answers null. Reading the clipboard
        ///  text directly avoids the whole problem.
        ///
        ///  <see cref="OnUrlChanged"/> still does the same job for text that arrives any other way
        ///  - typed, dropped, set by a test - and the two agree because both end in
        ///  <see cref="Take"/>. This one exists because it is the only one that sees a multi-line
        ///  paste before it has been flattened.
        ///
        ///  False means "not a curl, carry on and paste it as a URL", which is the ordinary case.
        /// </remarks>
        public bool TakeCurl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Looks(text) || Http.FromCurl(text) is not { } parsed)
            {
                return false;
            }

            Take(parsed);

            return true;
        }

        /// <summary>Whether a string is worth handing to the curl parser at all.</summary>
        /// <remarks>
        ///  Cheap enough to run on every keystroke, and narrow enough that a URL with the word
        ///  "curl" in its path does not get taken for a command: it has to start with the word and
        ///  be followed by something.
        /// </remarks>
        private static bool Looks(string text)
        {
            string trimmed = text.TrimStart();

            return (trimmed.StartsWith("curl ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("curl\t", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("curl\n", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("curl\r", StringComparison.OrdinalIgnoreCase))
                && trimmed.Length > 5;
        }

        /// <summary>
        ///  Files an imported request and selects it.
        /// </summary>
        /// <remarks>
        ///  A new row, not an overwrite. This used to copy the fields onto whatever was selected,
        ///  on the reasoning that someone pasting into the address box was editing that request -
        ///  which was wrong twice over. A curl is a whole request, method, headers and body
        ///  included, and pasting one over a request that took work to set up destroys it with no
        ///  way back; and the tree was left showing the old name, because the imported fields went
        ///  onto an object the tree was already holding and nothing told it to look again. The next
        ///  rebuild - pressing "new request" was the quickest way to cause one - then corrected the
        ///  label and added the new row at the same moment, which is what produced the two entries
        ///  that were reported.
        ///
        ///  The one request that is written over is an untouched blank one, and only because the
        ///  alternative is worse: a panel opened fresh starts with an empty request selected, and
        ///  importing into it otherwise leaves a permanent "New request" stub above every import
        ///  anybody does.
        /// </remarks>
        private void Take(HttpRequest parsed)
        {
            // Before anything else touches it, and before it is saved: the credentials in a curl
            // out of devtools go to the environment rather than to settings.json. See Adoption.
            Adoption.Result adopted = Adoption.Take(parsed, ActiveEnvironment);

            HttpRequest into;

            if (IsBlank(Selected))
            {
                into = Selected;

                into.Method = parsed.Method;
                into.Url = parsed.Url;
                into.Body = parsed.Body;
                into.Headers = [.. parsed.Headers];
                into.Name = parsed.Name;
            }
            else
            {
                into = parsed;

                // Filed beside the request that was open, so an import made while working inside a
                // group lands in that group rather than at the top of the tree.
                into.Group = Selected.Group;

                Saved.Add(into);
            }

            Selected = into;

            Load(into);
            Save();

            // Both paths change what the tree should say: one added a row, the other renamed one.
            Regroup();

            if (adopted.Any && ActiveEnvironment is { } environment)
            {
                // Said out loud, because a header the user can see in the curl they just pasted is
                // now somewhere else in the app, and silently is the wrong way to learn that.
                SaveEnvironments();

                Status = Imported(parsed) + Strings.Format(
                    "HttpMovedToEnvironment",
                    string.Join(", ", adopted.Moved),
                    environment.Name);

                return;
            }

            Status = Imported(parsed);
        }

        /// <summary>
        ///  Whether a request is an empty one nobody has typed into yet.
        /// </summary>
        /// <remarks>
        ///  The URL is the test, with the name only ruling out a request that was deliberately
        ///  named and not yet filled in. A blank body and no headers are not asked about: a GET
        ///  with neither is a perfectly ordinary request, and the one thing every request that has
        ///  had any work done to it has is an address.
        /// </remarks>
        private static bool IsBlank(HttpRequest request) =>
            request.Url.Trim().Length == 0 && IsUnnamed(request);

        /// <summary>Says what came across, since the headers are below the fold.</summary>
        private static string Imported(HttpRequest request)
        {
            int headers = request.Headers.Count;

            string note = Strings.Format("HttpImportedNote", request.Method)
                + (headers == 1
                    ? Strings.Text("HttpOneHeader")
                    : Strings.Format("HttpManyHeaders", headers));

            if (request.Headers.Any(header =>
                    header.Name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)))
            {
                note += Strings.Text("HttpNoteCookies");
            }

            if (request.Body.Length > 0)
            {
                note += Strings.Text("HttpNoteBody");
            }

            // Worth saying out loud rather than leaving to be discovered: the request is about to
            // be written to settings.json exactly as it reads, credentials included.
            if (request.Headers.Any(header =>
                    header.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)))
            {
                note += Strings.Text("HttpNoteCredentials");
            }

            return note;
        }

        partial void OnBodyChanged(string value)
        {
            Keep(request => request.Body = value);

            // A {{name}} is as likely to be in the body as in the URL, and the note has to answer
            // for the whole request rather than for the address bar alone.
            OnPropertyChanged(nameof(EnvironmentNote));
            OnPropertyChanged(nameof(HasUnresolved));
        }

        /// <summary>
        ///  Fills the editor from a saved request.
        /// </summary>
        /// <remarks>
        ///  The flag stops the setters above writing the newly-loaded values back into the request
        ///  that is being switched away from - which, without it, copies one request over another
        ///  the moment the selection changes.
        /// </remarks>
        private bool loading;

        private void Load(HttpRequest request)
        {
            loading = true;

            Method = request.Method;
            Url = request.Url;
            Body = request.Body;

            Headers.Clear();

            foreach (HeaderLine header in request.Headers)
            {
                Add(new HeaderRow(header));
            }

            loading = false;

            Result = null;
            Response = string.Empty;
            ResponseHeaders.Clear();
            ShownHeaders.Clear();
            Status = string.Empty;

            // The note belongs to whichever request is now in the editor.
            OnPropertyChanged(nameof(EnvironmentNote));
            OnPropertyChanged(nameof(HasUnresolved));
        }

        /// <summary>Applies an edit to the selected request and saves.</summary>
        private void Keep(Action<HttpRequest> edit)
        {
            if (loading)
            {
                return;
            }

            edit(Selected);
            Save();
        }

        /// <summary>Wires a header row so that editing it saves, then adds it.</summary>
        private void Add(HeaderRow row)
        {
            row.PropertyChanged += (_, _) => Keep(request =>
                request.Headers = [.. Headers.Select(header => header.ToLine())]);

            Headers.Add(row);
        }

        private void Save()
        {
            settings.Requests = [.. Saved];
            settings.Save();
        }

        /// <summary>
        ///  The selected request as it stands in the editor, rather than as last saved.
        /// </summary>
        /// <remarks>
        ///  The "what is missing" note has to answer for what is on screen while it is being
        ///  typed, and the editor's values reach the saved request only on the next keystroke's
        ///  Keep. Built fresh rather than mutating Selected, for the reason the resolver is a copy:
        ///  nothing that is only being inspected should be able to write itself back.
        /// </remarks>
        private HttpRequest Current() => new()
        {
            Name = Selected.Name,
            Method = Method,
            Url = Url,
            Body = Body,
            Colour = Selected.Colour,
            Group = Selected.Group,
            Headers = [.. Headers.Select(header => header.ToLine())],
        };

        /// <summary>
        ///  Rebuilds the grouped view of the saved list.
        /// </summary>
        /// <remarks>
        ///  Rebuilt wholesale rather than patched, because every edit that could change it -
        ///  renaming, filing, deleting, importing - would otherwise need its own correct patch,
        ///  and a tree that is subtly out of step with the list is worse than one rebuilt in a
        ///  microsecond. The lists involved are a few dozen entries.
        ///
        ///  Ungrouped requests come first and keep their order. Groups follow, sorted by name, so
        ///  the tree does not reshuffle itself as requests are filed.
        /// </remarks>
        private void Regroup()
        {
            Groups.Clear();
            Ungrouped.Clear();

            foreach (HttpRequest loose in Saved.Where(request => request.Group.Trim().Length == 0))
            {
                Ungrouped.Add(loose);
            }

            foreach (IGrouping<string, HttpRequest> group in Saved
                .Where(request => request.Group.Trim().Length > 0)
                .GroupBy(request => request.Group.Trim(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                Groups.Add(new RequestGroup(
                    group.Key,
                    [.. group],
                    settings.HttpCollapsedGroups.Contains(group.Key, StringComparer.Ordinal),
                    GroupIcon(group.Key)));
            }

            OnPropertyChanged(nameof(GroupNames));
            OnPropertyChanged(nameof(GroupChoices));
            OnPropertyChanged(nameof(HasGroups));
        }

        /// <summary>The requests filed under no group, which sit above the groups.</summary>
        public ObservableCollection<HttpRequest> Ungrouped { get; } = [];

        /// <summary>The group names in use, for the "move to" menu.</summary>
        public IReadOnlyList<string> GroupNames =>
            [.. Saved.Select(request => request.Group.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)];

        public bool HasGroups => Groups.Count > 0;

        /// <summary>The existing groups as menu entries, each carrying the command that files into it.</summary>
        public IReadOnlyList<GroupChoice> GroupChoices =>
            [.. GroupNames.Select(name => new GroupChoice(name, MoveToGroupCommand))];

        /// <summary>The colours offered on the flag menu.</summary>
        public IReadOnlyList<string> Colours => RequestColour.All;

        /// <summary>Set by the view, to ask for a group name.</summary>
        public Func<string, Task<string?>>? AskForGroup { get; set; }

        /// <summary>
        ///  Flags a request with a colour, or clears it.
        /// </summary>
        /// <remarks>
        ///  The parameter is the colour; the request is whichever the menu was opened on, which
        ///  the view supplies by selecting it first. An unknown colour clears the flag rather than
        ///  storing something nothing can draw.
        /// </remarks>
        [RelayCommand]
        private void Flag(string? colour)
        {
            string wanted = colour is not null && RequestColour.Known(colour) ? colour : RequestColour.None;

            Selected.Colour = wanted;
            Save();
            Regroup();
        }

        /// <summary>
        ///  Set by the view, to put the icon grid in front of a window. Takes the heading and the
        ///  icon already chosen; returns a name, "" to clear it, or null if the user backed out.
        /// </summary>
        public Func<string, string?, Task<string?>>? AskForIcon { get; set; }

        /// <summary>Puts an icon on the selected request, or takes it off.</summary>
        [RelayCommand]
        private async Task ChooseIcon()
        {
            if (AskForIcon is not { } ask)
            {
                return;
            }

            string? picked = await ask(Strings.Text("IconPickerTitle"), Selected.Icon);

            // Null is "leave it alone" and "" is "clear it". They have to stay distinguishable,
            // or backing out of the dialog would silently remove the icon that was there.
            if (picked is null)
            {
                return;
            }

            Selected.Icon = picked;
            Save();
            Regroup();
        }

        /// <summary>The same for a group, whose icon lives in settings rather than on an object.</summary>
        public async Task ChooseGroupIcon(RequestGroup group)
        {
            if (AskForIcon is not { } ask)
            {
                return;
            }

            string? picked = await ask(Strings.Text("IconPickerTitle"), GroupIcon(group.Name));

            if (picked is null)
            {
                return;
            }

            Dictionary<string, string> icons = new(settings.HttpGroupIcons, StringComparer.Ordinal);

            if (picked.Length == 0)
            {
                icons.Remove(group.Name);
            }
            else
            {
                icons[group.Name] = picked;
            }

            settings.HttpGroupIcons = icons;
            settings.Save();
            Regroup();
        }

        /// <summary>The icon chosen for a group, or empty.</summary>
        public string GroupIcon(string name) =>
            settings.HttpGroupIcons.TryGetValue(name, out string? icon) ? icon : string.Empty;

        /// <summary>Files the selected request under a group, asking for the name.</summary>
        [RelayCommand]
        private async Task MoveToNewGroup()
        {
            if (AskForGroup is null)
            {
                return;
            }

            string? name = await AskForGroup(Selected.Group);

            if (name is null)
            {
                return;
            }

            Selected.Group = name.Trim();
            Save();
            Regroup();

            Status = Selected.Group.Length > 0
                ? Strings.Format("HttpFiledUnder", Selected.Group)
                : Strings.Text("HttpUngrouped");
        }

        /// <summary>
        ///  Whether this request still carries the name it was given automatically.
        /// </summary>
        /// <remarks>
        ///  Checked against every language's default rather than the current one. A request made
        ///  in English and then pasted over while the app is in Portuguese is still a request
        ///  nobody has named, and keeping "New request" over the URL from the curl would be the
        ///  wrong answer purely because a setting changed in between.
        /// </remarks>
        private static bool IsUnnamed(HttpRequest request) =>
            request.Name.Length == 0
            || request.Name == "New request"
            || request.Name == Strings.Text("HttpNewRequestName");

        /// <summary>Files the selected request under a group that already exists.</summary>
        [RelayCommand]
        private void MoveToGroup(string? name)
        {
            Selected.Group = (name ?? string.Empty).Trim();
            Save();
            Regroup();

            Status = Selected.Group.Length > 0
                ? Strings.Format("HttpFiledUnder", Selected.Group)
                : Strings.Text("HttpUngrouped");
        }

        /// <summary>
        ///  Remembers which groups are collapsed.
        /// </summary>
        /// <remarks>
        ///  Called by the view when a header is clicked. Collapsed is what is stored rather than
        ///  expanded, so a group created today arrives open - the user has just put something in
        ///  it and expects to see it.
        /// </remarks>
        public void SetCollapsed(string group, bool collapsed)
        {
            List<string> shut = [.. settings.HttpCollapsedGroups];

            if (collapsed && !shut.Contains(group, StringComparer.Ordinal))
            {
                shut.Add(group);
            }
            else if (!collapsed)
            {
                shut.RemoveAll(name => string.Equals(name, group, StringComparison.Ordinal));
            }

            settings.HttpCollapsedGroups = shut;
            settings.Save();
        }

        /// <summary>Duplicates the selected request, which is how a variant of one is started.</summary>
        [RelayCommand]
        private void Duplicate()
        {
            HttpRequest copy = new()
            {
                Name = Strings.Format("HttpCopySuffix", Selected.Name),
                Method = Selected.Method,
                Url = Selected.Url,
                Body = Selected.Body,
                Colour = Selected.Colour,
                Icon = Selected.Icon,
                Group = Selected.Group,
                Headers = [.. Selected.Headers.Select(header => new HeaderLine
                {
                    Name = header.Name,
                    Value = header.Value,
                    Enabled = header.Enabled,
                })],
            };

            Saved.Add(copy);
            Selected = copy;
            Save();
            Regroup();
        }

        [RelayCommand]
        private void AddHeader() => Add(new HeaderRow(new HeaderLine()));

        [RelayCommand]
        private void RemoveHeader(HeaderRow? row)
        {
            if (row is null)
            {
                return;
            }

            Headers.Remove(row);
            Keep(request => request.Headers = [.. Headers.Select(header => header.ToLine())]);
        }

        [RelayCommand]
        private void NewRequest()
        {
            HttpRequest request = new() { Name = Strings.Text("HttpNewRequestName") };

            Saved.Add(request);
            Selected = request;
            Save();
            Regroup();
        }

        [RelayCommand]
        private void DeleteRequest()
        {
            if (Saved.Count <= 1)
            {
                // The panel always has one request in it, because a panel with none has nothing to
                // show and no obvious way back to having one.
                Status = Strings.Text("HttpLastOne");

                return;
            }

            HttpRequest doomed = Selected;
            int at = Saved.IndexOf(doomed);

            Saved.Remove(doomed);
            Selected = Saved[Math.Min(at, Saved.Count - 1)];
            Save();
            Regroup();
        }

        /// <summary>Renames the selected request after its URL, which is what it is really called.</summary>
        [RelayCommand]
        private void Rename() => Retitle(Http.Name(Url));

        /// <summary>Set by the view, to ask for a name.</summary>
        /// <remarks>
        ///  A callback rather than a dialog built here, for the usual reason: asking needs a window
        ///  to be modal to, and a view model does not have one.
        /// </remarks>
        public Func<string, Task<string?>>? AskForName { get; set; }

        /// <summary>
        ///  Renames the request to whatever the user types.
        /// </summary>
        /// <remarks>
        ///  Reached by right-click or by double-click on the row, which is where anyone who has
        ///  used a file manager will try first. "Name it after the URL" stays on the ⋯ menu beside
        ///  it: the two do different jobs, and the automatic one is right often enough to keep.
        /// </remarks>
        [RelayCommand]
        private async Task RenameAs(HttpRequest? request)
        {
            HttpRequest target = request ?? Selected;

            if (AskForName is null)
            {
                return;
            }

            // Selected first, so the dialog is about the row that was clicked rather than about
            // whichever one happened to be selected before.
            Selected = target;

            string? chosen = await AskForName(target.Name);

            if (string.IsNullOrWhiteSpace(chosen) || chosen.Trim() == target.Name)
            {
                return;
            }

            Retitle(chosen.Trim());
        }

        private void Retitle(string name)
        {
            Selected.Name = name;
            Save();

            // The list binds to the object, which has no change notification of its own, so the
            // row is replaced in place to make the new name appear.
            int at = Saved.IndexOf(Selected);

            if (at >= 0)
            {
                HttpRequest same = Selected;

                Saved.RemoveAt(at);
                Saved.Insert(at, same);
                Selected = same;
            }
        }

        private bool CanSend => !Sending;

        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task Send()
        {
            Sending = true;
            Status = "Sending…";
            Result = null;

            // The row that was sent, held here: the user may click another request while this
            // one is on its way, and the answer belongs to the one that went out.
            HttpRequest target = Selected;
            target.InFlight = true;

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource();

            // Resolved into a copy, never in place: the saved request keeps its braces, so the
            // panel never becomes a place a token is displayed and settings.json never acquires
            // one. An unresolved name is sent as written and the note above says which.
            HttpResult sent = await Http.Send(
                HttpVariables.Resolved(Current(), ActiveEnvironment),
                inFlight.Token);

            Result = sent;
            Response = sent.Failed ? string.Empty : sent.Body;
            Status = sent.Summary;

            target.LastStatus = sent.Failed || sent.Status == 0 ? -1 : sent.Status;
            target.LastMilliseconds = sent.Milliseconds;
            target.InFlight = false;

            // Saved so the list still shows what each request last answered after a restart.
            Save();

            ResponseHeaders.Clear();

            foreach (HeaderLine header in sent.Headers)
            {
                ResponseHeaders.Add(header);
            }

            FilterHeaders();

            Sending = false;
        }

        private bool CanCancel => Sending;

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel() => inFlight?.Cancel();

        [RelayCommand]
        private async Task CopyCurl()
        {
            if (Copy is { } copy)
            {
                await copy(Http.AsCurl(Selected));
                Status = Strings.Text("HttpCurlCopied");
            }
        }

        /// <summary>
        ///  Reads a curl command off the clipboard into the editor.
        /// </summary>
        /// <remarks>
        ///  The single highest-value button on the panel. Every API's documentation and every
        ///  browser's devtools hand out curl, and this is the difference between using the request
        ///  and retyping it.
        /// </remarks>
        [RelayCommand]
        private async Task PasteCurl()
        {
            if (Paste is not { } paste)
            {
                return;
            }

            if (await paste() is not { Length: > 0 } text)
            {
                return;
            }

            if (Http.FromCurl(text) is not { } parsed)
            {
                Status = Strings.Text("HttpNotCurl");

                return;
            }

            Take(parsed);
        }

        /// <summary>
        ///  Writes the reply to a file, named and extended by what it turned out to be.
        /// </summary>
        /// <remarks>
        ///  The bytes as they arrived where there are any, and the text otherwise. That
        ///  distinction is the whole point: an image saved from its displayed text would be the
        ///  sentence describing it, and a JSON body saved from bytes that were never kept would be
        ///  nothing at all. The sender keeps the raw bytes for exactly the kinds whose text is a
        ///  description, so the two cases cover every response between them.
        ///
        ///  The text written is what is on screen, which for JSON, XML and JavaScript means the
        ///  formatted version rather than the wire bytes. That is deliberate - somebody saving a
        ///  response is saving what they were looking at - and it is the reason a JSON file written
        ///  here will not be byte-identical to one curl wrote.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(HasResult))]
        private async Task SaveResponse()
        {
            if (Result is not { } reply || AskWhereToSave is not { } ask)
            {
                return;
            }

            string? path = await ask(reply.SuggestedName);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (reply.Raw is { Length: > 0 } bytes)
                {
                    await File.WriteAllBytesAsync(path, bytes);
                }
                else
                {
                    await File.WriteAllTextAsync(path, Response);
                }

                Status = Strings.Format("HttpResponseSaved", System.IO.Path.GetFileName(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A folder that went away, a file open in something else, a share that needs
                // credentials. All of them are the user's to fix and none of them are a crash.
                Status = Strings.Format("HttpResponseNotSaved", exception.Message);
            }
        }

        [RelayCommand]
        private async Task CopyResponse()
        {
            if (Response.Length > 0 && Copy is { } copy)
            {
                await copy(Response);
                Status = Strings.Text("HttpResponseCopied");
            }
        }
    }
}
