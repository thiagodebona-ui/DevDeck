using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One conversion the toolbox offers.</summary>
    /// <remarks>
    ///  One class rather than a subclass per tool: every one of them is a string in and a
    ///  <see cref="ToolResult"/> out, and sixteen near-identical classes would add ceremony without
    ///  adding a single decision.
    ///
    ///  It holds the keys of its three pieces of prose rather than the prose itself, and looks each
    ///  one up on demand, so that switching language redraws the list in place. The alternative -
    ///  building the list again - would throw away the selection, the input and the output every
    ///  time, which is a visible loss for a panel people leave open with something half typed in it.
    /// </remarks>
    internal sealed partial class Tool : ObservableObject
    {
        private readonly string nameKey;

        private readonly string groupKey;

        private readonly string hintKey;

        public Tool(string nameKey, string groupKey, Func<string, ToolResult> run, string hintKey = "")
        {
            this.nameKey = nameKey;
            this.groupKey = groupKey;
            this.hintKey = hintKey;
            Run = run;

            Strings.Changed += () =>
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Group));
                OnPropertyChanged(nameof(Hint));
            };
        }

        /// <summary>What the tool does, as drawn in the list.</summary>
        public string Name => Strings.Text(nameKey);

        /// <summary>The heading it sits under.</summary>
        public string Group => Strings.Text(groupKey);

        /// <summary>The conversion itself.</summary>
        public Func<string, ToolResult> Run { get; }

        /// <summary>Shown under the input, to say what this expects.</summary>
        public string Hint => hintKey.Length > 0 ? Strings.Text(hintKey) : string.Empty;

        /// <summary>Whether the algorithm picker applies to this one.</summary>
        public bool NeedsAlgorithm { get; init; }

        /// <summary>Whether the case picker applies.</summary>
        public bool NeedsCase { get; init; }

        /// <summary>Whether this one takes a pattern as well as an input.</summary>
        public bool NeedsPattern { get; init; }

        /// <summary>Whether it produces its own output without being given anything.</summary>
        public bool Generates { get; init; }

        /// <summary>The glyph beside the name in the list, from <see cref="Icons"/>.</summary>
        public Avalonia.Media.Geometry? Icon { get; init; }

        /// <summary>
        ///  What the output is, which decides how it is drawn: JSON and XML are coloured, the rest
        ///  is plain text.
        /// </summary>
        public BodyKind OutputKind { get; init; } = BodyKind.Plain;

        /// <summary>
        ///  The JSON to build a tree from, given the input and the output - or null for a tool
        ///  whose output is not a document.
        /// </summary>
        /// <remarks>
        ///  Usually the output itself. The JWT decoder is the exception: its output reads well and
        ///  does not parse, so the tree is built from the token's parts instead.
        /// </remarks>
        public Func<string, string, string>? TreeFrom { get; init; }

        public bool IsStructured => OutputKind != BodyKind.Plain;

        /// <summary>What was in the input box when this tool was last left, put back when it is chosen again.</summary>
        public string KeptInput { get; set; } = string.Empty;

        /// <summary>The same for the regex tool's pattern.</summary>
        public string KeptPattern { get; set; } = string.Empty;

        public bool HasTree => TreeFrom is not null;
    }

    /// <summary>
    ///  The conversions a developer would otherwise leave the machine for.
    /// </summary>
    /// <remarks>
    ///  Modelled on DevToys, and built for the same reason it exists: decoding a JWT or formatting
    ///  a blob of JSON is a thirty-second job that people do on a website, pasting production
    ///  tokens and customer data into someone else's server to do it. Everything here runs in this
    ///  process and nothing leaves the machine - which is the whole argument for the panel, and is
    ///  said plainly on it rather than only here.
    ///
    ///  It converts as you type rather than on a button, because half of these are used to answer a
    ///  question rather than to produce a value - what is in this token, is this JSON valid - and
    ///  an answer that needs a button press is an answer you go elsewhere for. That is also why
    ///  every tool returns a result rather than throwing: a half-typed input is the normal state of
    ///  this panel, not an error in it.
    /// </remarks>
    internal sealed partial class ToolboxViewModel : ObservableObject
    {
        public ToolboxViewModel()
        {
            Tools = new ObservableCollection<Tool>(Build());
            selected = Tools[0];
            Convert();
        }

        public ObservableCollection<Tool> Tools { get; }

        [ObservableProperty]
        private Tool selected;

        [ObservableProperty]
        private string input = string.Empty;

        [ObservableProperty]
        private string output = string.Empty;

        [ObservableProperty]
        private string pattern = string.Empty;

        [ObservableProperty]
        private string? error;

        [ObservableProperty]
        private string note = string.Empty;

        [ObservableProperty]
        private string statistics = string.Empty;

        /// <summary>
        ///  Whether a JSON result is shown as a tree to expand rather than as text.
        /// </summary>
        /// <remarks>
        ///  Kept across tools and keystrokes: someone who chose the tree is reading structure, and
        ///  falling back to text on every change would make them choose it again each time.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowingTree))]
        [NotifyPropertyChangedFor(nameof(ShowingStructuredText))]
        private bool treeChosen;

        /// <summary>The parsed result, or null when there is none to show.</summary>
        [ObservableProperty]
        private JsonNode? tree;

        /// <summary>Said in place of a tree when the output does not parse.</summary>
        [ObservableProperty]
        private string treeNote = string.Empty;

        public bool ShowingTree => TreeChosen && Selected.HasTree;

        /// <summary>Coloured text: a JSON or XML tool, not showing its tree.</summary>
        public bool ShowingStructuredText => Selected.IsStructured && !ShowingTree && !Selected.NeedsPattern;

        /// <summary>Plain text, for every other tool.</summary>
        public bool ShowingPlainText => !Selected.IsStructured && !Selected.NeedsPattern;

        partial void OnTreeChosenChanged(bool value) => Grow();

        [RelayCommand]
        private void ShowText() => TreeChosen = false;

        [RelayCommand]
        private void ShowTree() => TreeChosen = true;

        /// <summary>Counts tree builds, so a slow parse of older text is dropped when a newer one finishes.</summary>
        private int growing;

        /// <summary>
        ///  Builds the tree, only while it is the thing on screen, and off the UI thread: this runs
        ///  on every keystroke, and parsing a pasted export of several megabytes takes long enough
        ///  to be felt as lag in the box being typed into.
        /// </summary>
        private async void Grow()
        {
            int mine = ++growing;

            if (!ShowingTree || Selected.TreeFrom is not { } from)
            {
                Tree = null;
                TreeNote = string.Empty;
                return;
            }

            string source = from(Input, Output);
            bool typed = Input.Length > 0;

            JsonNode? root = await Task.Run(() => JsonTree.Build(source));

            if (mine != growing)
            {
                return;
            }

            Tree = root;
            TreeNote = root is null && typed ? Strings.Text("ToolboxTreeNotJson") : string.Empty;
        }

        /// <summary>Set by the view after a copy, and cleared on the next keystroke.</summary>
        [ObservableProperty]
        private string status = string.Empty;

        public IReadOnlyList<string> Algorithms => TextTools.HashAlgorithms;

        [ObservableProperty]
        private string algorithm = "SHA256";

        public IReadOnlyList<string> Cases { get; } =
            ["lower", "UPPER", "Title", "camelCase", "PascalCase", "snake_case", "kebab-case", "CONSTANT_CASE"];

        [ObservableProperty]
        private string casing = "camelCase";

        /// <summary>Set by the view: copying needs the clipboard, which lives up there.</summary>
        public Func<string, Task>? Copy { get; set; }

        /// <summary>Also set by the view, for the tools that want what is on the clipboard.</summary>
        public Func<Task<string?>>? Paste { get; set; }

        public bool HasError => Error is not null;

        public bool HasNote => Note.Length > 0;

        partial void OnSelectedChanged(Tool? oldValue, Tool newValue)
        {
            Status = string.Empty;

            // Each tool keeps its own input. The box used to be shared, so JSON formatted in one
            // tool was still sitting there when the UUID generator or the hash was opened - text
            // that belonged to another job. Going back to a tool finds what was left in it, which
            // is the other half of the same idea. Chaining two tools on purpose is what "Use as
            // input" is for.
            if (oldValue is not null)
            {
                oldValue.KeptInput = Input;
                oldValue.KeptPattern = Pattern;
            }

            Error = null;

            // Pattern first and quietly: setting the input converts, and it should convert with
            // this tool's pattern rather than the last one's.
            pattern = newValue.KeptPattern;
            OnPropertyChanged(nameof(Pattern));

            if (Input == newValue.KeptInput)
            {
                Convert();
            }
            else
            {
                Input = newValue.KeptInput;
            }

            OnPropertyChanged(nameof(ShowingTree));
            OnPropertyChanged(nameof(ShowingStructuredText));
            OnPropertyChanged(nameof(ShowingPlainText));
        }

        partial void OnInputChanged(string value)
        {
            Status = string.Empty;
            Statistics = value.Length > 0 ? TextTools.Statistics(value) : string.Empty;
            Convert();
        }

        partial void OnPatternChanged(string value) => Convert();

        partial void OnAlgorithmChanged(string value) => Convert();

        partial void OnCasingChanged(string value) => Convert();

        /// <summary>
        ///  Runs the selected tool over the current input.
        /// </summary>
        /// <remarks>
        ///  Called on every keystroke, so it has to be cheap and it has to be quiet: an input that
        ///  is not yet valid shows nothing rather than a red error, because text on its way to
        ///  being correct is not a mistake. The error only appears once there is enough input for
        ///  "this is wrong" to be a useful thing to say.
        /// </remarks>
        private void Convert()
        {
            Note = string.Empty;

            if (Selected.Generates)
            {
                Error = null;
                Output = Selected.Run(Input).Text;
                Raise();

                return;
            }

            if (Input.Length == 0)
            {
                Output = string.Empty;
                Error = null;
                Raise();

                return;
            }

            ToolResult result = Selected.Run(Input);

            Output = result.Text;
            Error = result.Error;
            Raise();
        }

        private void Raise()
        {
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasNote));
            Grow();
        }

        [RelayCommand]
        private async Task CopyOut()
        {
            if (Output.Length == 0 || Copy is not { } copy)
            {
                return;
            }

            await copy(Output);
            Status = Strings.Text("ToolCopied");
        }

        [RelayCommand]
        private async Task PasteIn()
        {
            if (Paste is not { } paste)
            {
                return;
            }

            if (await paste() is { Length: > 0 } text)
            {
                Input = text;
            }
        }

        /// <summary>Feeds the result back in, for the times two tools are wanted in sequence.</summary>
        [RelayCommand]
        private void Chain()
        {
            if (Output.Length > 0)
            {
                Input = Output;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            Input = string.Empty;
            Pattern = string.Empty;
            Status = string.Empty;
        }

        /// <summary>Re-rolls a generator without needing the input to change.</summary>
        [RelayCommand]
        private void Again() => Convert();

        /// <summary>The matches from the regex tool, shown as a list rather than as text.</summary>
        public ObservableCollection<string> Matches { get; } = [];

        private IReadOnlyList<Tool> Build() =>
        [
            new("ToolJsonFormat", "ToolGroupData", text => TextTools.FormatJson(text), "ToolJsonFormatHint")
            {
                Icon = Icons.Braces,
                OutputKind = BodyKind.Json,
                TreeFrom = (_, output) => output,
            },
            new("ToolJsonMinify", "ToolGroupData", TextTools.MinifyJson)
            {
                Icon = Icons.Compress,
                OutputKind = BodyKind.Json,
                TreeFrom = (_, output) => output,
            },
            new("ToolXmlFormat", "ToolGroupData", TextTools.FormatXml)
            {
                Icon = Icons.Code,
                OutputKind = BodyKind.Xml,
            },
            new("ToolBase64Encode", "ToolGroupEncoding", TextTools.ToBase64) { Icon = Icons.Lock },
            new("ToolBase64Decode", "ToolGroupEncoding", TextTools.FromBase64, "ToolBase64DecodeHint") { Icon = Icons.Unlock },
            new("ToolUrlEncode", "ToolGroupEncoding", TextTools.UrlEncode) { Icon = Icons.Link },
            new("ToolUrlDecode", "ToolGroupEncoding", TextTools.UrlDecode) { Icon = Icons.Globe },
            new("ToolHtmlEscape", "ToolGroupEncoding", TextTools.HtmlEncode) { Icon = Icons.Shield },
            new("ToolHtmlUnescape", "ToolGroupEncoding", TextTools.HtmlDecode) { Icon = Icons.Eye },
            new("ToolJwtDecode", "ToolGroupInspect", TextTools.DecodeJwt, "ToolJwtDecodeHint")
            {
                Icon = Icons.Key,
                OutputKind = BodyKind.Json,
                TreeFrom = (input, _) => TextTools.JwtAsJson(input),
            },
            new("ToolHash", "ToolGroupInspect", text => TextTools.Hash(text, Algorithm))
            {
                Icon = Icons.Hash,
                NeedsAlgorithm = true,
            },
            new("ToolTimestamp", "ToolGroupInspect", text => TextTools.Timestamp(text), "ToolTimestampHint") { Icon = Icons.Clock },
            new("ToolUuid", "ToolGroupGenerate", _ => ToolResult.Ok(TextTools.NewGuid()), "ToolUuidHint")
            {
                Icon = Icons.Sparkle,
                Generates = true,
            },
            new("ToolCase", "ToolGroupText", text => TextTools.ChangeCase(text, Style()))
            {
                Icon = Icons.Case,
                NeedsCase = true,
            },
            new("ToolSortLines", "ToolGroupText", text => TextTools.SortLines(text, unique: true), "ToolSortLinesHint") { Icon = Icons.Sort },
            new("ToolRegex", "ToolGroupText", RunRegex, "ToolRegexHint")
            {
                Icon = Icons.Regex,
                NeedsPattern = true,
            },
        ];

        /// <summary>
        ///  The regex tool, which is the one that does not fit the shape.
        /// </summary>
        /// <remarks>
        ///  It has a second input and a list of results rather than one string, so it fills the
        ///  match collection as a side effect and returns a summary. Bending the other fifteen tools
        ///  into a shape that accommodated this one would have cost more than this single exception.
        /// </remarks>
        private ToolResult RunRegex(string text)
        {
            Matches.Clear();

            if (Pattern.Length == 0)
            {
                return ToolResult.Ok(string.Empty);
            }

            ToolResult result = TextTools.TestRegex(
                Pattern, text, ignoreCase: false, multiline: true, out IReadOnlyList<TextTools.RegexMatch> found);

            foreach (TextTools.RegexMatch match in found)
            {
                string groups = match.Groups.Count > 0
                    ? "   [" + string.Join("] [", match.Groups) + "]"
                    : string.Empty;

                Matches.Add($"{match.Index}: {match.Value}{groups}");
            }

            return result;
        }

        private TextTools.CaseStyle Style() => Casing switch
        {
            "lower" => TextTools.CaseStyle.Lower,
            "UPPER" => TextTools.CaseStyle.Upper,
            "Title" => TextTools.CaseStyle.Title,
            "camelCase" => TextTools.CaseStyle.Camel,
            "PascalCase" => TextTools.CaseStyle.Pascal,
            "snake_case" => TextTools.CaseStyle.Snake,
            "kebab-case" => TextTools.CaseStyle.Kebab,
            _ => TextTools.CaseStyle.Constant,
        };
    }
}
