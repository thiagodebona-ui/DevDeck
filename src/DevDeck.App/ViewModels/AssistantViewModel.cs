using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>Who said it, which is all the transcript needs to lay a turn out.</summary>
    /// <remarks>
    ///  The last three are V2's, and they are not decoration: an error from the app, a note from
    ///  the app, and the output of something the app ran are three different things, and a
    ///  transcript that paints them all as "assistant" is lying about where the text came from.
    /// </remarks>
    internal enum ChatRole
    {
        User,
        Assistant,
        Notice,
        Problem,
        Output,
    }

    /// <summary>
    ///  A run of text inside one turn: either prose or a fenced code block.
    /// </summary>
    /// <remarks>
    ///  V2 drew these by hand in GDI and hit-tested the Run and Add buttons itself. Here a segment
    ///  is just data and the template draws it, which is most of why the transcript is no longer
    ///  the largest file in the project.
    /// </remarks>
    internal sealed partial class Segment : ObservableObject
    {
        public Segment(string text, bool isCode, string info = "", bool showsActions = false)
        {
            this.text = text;
            IsCode = isCode;
            Info = info;
            Language = ChainPlan.LanguageOf(info);
            Name = ChainPlan.NameOf(info);
            IsChain = isCode && ChainPlan.IsChainFence(info);
            ShowsActions = showsActions;
        }

        /// <summary>The whole fence line after the backticks: powershell name="Count files".</summary>
        public string Info { get; }

        /// <summary>The name the model gave this block, for a chain's commands. Empty otherwise.</summary>
        public string Name { get; }

        /// <summary>
        ///  Whether this is the block that defines a chain.
        /// </summary>
        /// <remarks>
        ///  It gets Create chain instead of Run and Add: it is a list of names, not a script, and
        ///  running it as one would hand "steps:" to cmd.exe.
        /// </remarks>
        public bool IsChain { get; }

        public bool IsScript => !IsChain;

        /// <summary>The chain made from this block, once Create chain or Run chain has made it.</summary>
        public ChainItem? Chain { get; set; }

        /// <summary>
        ///  Whether this block carries Run and Add.
        /// </summary>
        /// <remarks>
        ///  V2's rule, which V3 had lost: buttons belong on a model's code, not on the user's words
        ///  and not on the output of something already run. Without it, pasting a fenced block into
        ///  the prompt gave your own message a Run button.
        /// </remarks>
        public bool ShowsActions { get; }

        [ObservableProperty]
        private string text;

        public bool IsCode { get; }

        public string Language { get; }

        /// <summary>The label under a code block, e.g. "powershell" or "powershell · Count files".</summary>
        public string Caption => (Language.Length > 0 ? Language : "code")
            + (Name.Length > 0 ? $" · {Name}" : string.Empty);
    }

    /// <summary>
    ///  One thing the assistant can do, as the help panel shows it: what, how, and a prompt to try.
    /// </summary>
    /// <remarks>
    ///  Keys rather than text, so a topic is the same thing in every language. The panel follows
    ///  a change of language by being handed fresh topics, which a binding reads again.
    /// </remarks>
    internal sealed record HelpTopic(string TitleKey, string TextKey, string ExampleKey)
    {
        public string Title => Strings.Text(TitleKey);

        public string Text => Strings.Text(TextKey);

        public string Example => Strings.Text(ExampleKey);
    }

    /// <summary>One turn in the conversation.</summary>
    internal sealed partial class Turn : ObservableObject
    {
        public Turn(ChatRole role, string text)
        {
            Role = role;
            At = DateTime.Now;
            Raw = new StringBuilder(text);
            Segments = new ObservableCollection<Segment>();
            Resegment();
        }

        public ChatRole Role { get; }

        /// <summary>
        ///  When the turn started.
        /// </summary>
        /// <remarks>
        ///  Taken when the turn opens, not when it finishes: for a streamed reply those can be a
        ///  minute apart, and the useful question is when you asked, not when the model stopped.
        /// </remarks>
        public DateTime At { get; }

        /// <summary>The same "HH:mm:ss" V2's log used, beside the speaker's name.</summary>
        public string Stamp => At.ToString("HH:mm:ss");

        public StringBuilder Raw { get; }

        public ObservableCollection<Segment> Segments { get; }

        public bool IsUser => Role == ChatRole.User;

        public bool IsAssistant => Role == ChatRole.Assistant;

        public bool IsNotice => Role is ChatRole.Notice or ChatRole.Problem;

        public bool IsProblem => Role == ChatRole.Problem;

        public bool IsOutput => Role == ChatRole.Output;

        /// <summary>Output is machine text and is set solid, so it never gets the prose treatment.</summary>
        public bool IsMono => Role == ChatRole.Output;

        /// <summary>Who said it, as drawn above the turn.</summary>
        /// <remarks>
        ///  Looked up rather than stored, so a transcript already on screen is relabelled when the
        ///  language changes. The app's own name is not translated: it is a name, not a word.
        /// </remarks>
        public string Who => Role switch
        {
            ChatRole.User => Strings.Text("AiWhoYou"),
            ChatRole.Assistant => Strings.Text("AiWhoAssistant"),
            ChatRole.Output => Strings.Text("AiWhoOutput"),
            ChatRole.Problem => Strings.Text("AiWhoProblem"),
            _ => "DevDeck",
        };

        public void Append(string delta)
        {
            Raw.Append(delta);
            Resegment();
        }

        /// <summary>
        ///  Splits the raw markdown into prose and fenced code.
        /// </summary>
        /// <remarks>
        ///  Re-split on every flush rather than parsed incrementally: a fence only becomes a fence
        ///  once its closing line arrives, so an incremental parser would have to guess and then
        ///  undo.
        ///
        ///  What it no longer does is clear the collection and refill it. That rebuilt every code
        ///  block's visual tree - buttons, borders, the lot - for each token that arrived, which is
        ///  half of why a streaming reply used to lock the window up. Now a segment whose shape has
        ///  not changed keeps its object and only its text is set, so the controls stay put and
        ///  only the characters change.
        /// </remarks>
        public void Resegment()
        {
            // Only the model's turns are split on fences. V2 drew the other roles whole, and it is
            // right to: output is machine text that may well contain backticks, and a user's own
            // message is not a thing to offer actions on.
            if (Role != ChatRole.Assistant)
            {
                Merge([new Segment(Raw.ToString().TrimEnd('\r', '\n'), Role == ChatRole.Output)]);
                return;
            }

            List<Segment> built = [];
            StringBuilder prose = new();
            StringBuilder code = new();

            bool inCode = false;
            string language = string.Empty;

            foreach (string line in Raw.ToString().Split('\n'))
            {
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCode)
                    {
                        built.Add(Code(code.ToString(), language));
                        code.Clear();
                        inCode = false;
                        language = string.Empty;
                    }
                    else
                    {
                        Flush(built, prose);
                        inCode = true;
                        language = line.Trim().TrimStart('`').Trim();
                    }

                    continue;
                }

                (inCode ? code : prose).Append(line).Append('\n');
            }

            // An unterminated fence is a block still being streamed, so it shows as code already.
            if (inCode)
            {
                built.Add(Code(code.ToString(), language));
            }

            Flush(built, prose);

            Merge(built);
        }

        /// <summary>A code block, with actions only when there is something to act on.</summary>
        private static Segment Code(string body, string language)
        {
            string text = body.TrimEnd('\r', '\n');

            return new Segment(text, true, language, showsActions: text.Trim().Length > 0);
        }

        /// <summary>Reconciles the freshly split list into the bound one, in place where it can.</summary>
        private void Merge(List<Segment> built)
        {
            for (int index = 0; index < built.Count; index++)
            {
                Segment fresh = built[index];

                if (index < Segments.Count)
                {
                    Segment existing = Segments[index];

                    // Same kind and same language means the same block, one token longer. Setting
                    // the text keeps the control and its buttons exactly where they were.
                    if (existing.IsCode == fresh.IsCode
                        && existing.Info == fresh.Info
                        && existing.ShowsActions == fresh.ShowsActions)
                    {
                        if (existing.Text != fresh.Text)
                        {
                            existing.Text = fresh.Text;
                        }

                        continue;
                    }

                    Segments[index] = fresh;
                    continue;
                }

                Segments.Add(fresh);
            }

            // A fence that closed can leave fewer segments than last time.
            while (Segments.Count > built.Count)
            {
                Segments.RemoveAt(Segments.Count - 1);
            }
        }

        private static void Flush(List<Segment> into, StringBuilder prose)
        {
            string text = prose.ToString().Trim('\r', '\n');

            if (text.Length > 0)
            {
                into.Add(new Segment(text, false));
            }

            prose.Clear();
        }
    }

    /// <summary>
    ///  The assistant: pick a provider and model, ask, watch the answer arrive, and take any code
    ///  it gives you straight into the command deck - or run it here and read the output in place.
    /// </summary>
    /// <remarks>
    ///  Feature for feature with V2's ChatPanel: the endpoint is probed before the box will accept
    ///  anything, a local runtime can be installed from the failure message, the model can be
    ///  switched mid-conversation, Enter sends, the last answer can be saved as a command, and code
    ///  runs into the transcript rather than somewhere else.
    /// </remarks>
    internal sealed partial class AssistantViewModel : ObservableObject
    {
        /// <summary>
        ///  How much of the conversation is sent back as context.
        /// </summary>
        /// <remarks>
        ///  V2's cap, and V2's reason: it keeps follow-ups like "make it recursive" working without
        ///  growing the prompt without limit on a local model. V3 was sending the whole history,
        ///  which on a long conversation is what makes a local model slow to first token.
        /// </remarks>
        private const int MaxHistory = 12;

        /// <summary>
        ///  How often streamed text is handed to the UI.
        /// </summary>
        /// <remarks>
        ///  A fast local model emits tokens far quicker than a screen refreshes, and the old code
        ///  posted a dispatcher job per token, each one re-splitting the whole reply and rebuilding
        ///  its controls. Sixteen milliseconds is one frame at 60Hz: anything finer is work nobody
        ///  can see.
        /// </remarks>
        private static readonly TimeSpan StreamFlush = TimeSpan.FromMilliseconds(16);

        private readonly AppSettings settings;
        private readonly Func<CustomCommand, CommandItem> addCommand;
        private readonly Action showCommands;
        private readonly Func<string> workingDirectory;
        private readonly List<ChatMessage> history = [];

        /// <summary>Text that has arrived from the network but not yet been shown.</summary>
        private readonly StringBuilder incoming = new();

        private readonly object gate = new();

        private readonly DispatcherTimer streamTimer;

        private Turn? streaming;

        private CancellationTokenSource? cancellation;

        private CancellationTokenSource? running;

        /// <summary>
        ///  The conversation on screen, as it will be written to disk.
        /// </summary>
        /// <remarks>
        ///  Kept alongside <see cref="Turns"/> rather than derived from it at save time, because the
        ///  two are not the same thing: the transcript carries the app's own notices and the output
        ///  of scripts it ran, and a saved conversation wants what was said to the model and what
        ///  came back. Reopening one should not replay "Setting up Ollama".
        /// </remarks>
        private SavedChat current = new();

        public AssistantViewModel(
            AppSettings settings,
            Func<CustomCommand, CommandItem> addCommand,
            Action showCommands,
            Func<string> workingDirectory)
        {
            this.settings = settings;
            this.addCommand = addCommand;
            this.showCommands = showCommands;
            this.workingDirectory = workingDirectory;

            Providers = AiPresets.All.ToList();

            AiProfile current = settings.CurrentAiProfile();

            provider = Providers.FirstOrDefault(p =>
                string.Equals(p.Name, current.Provider, StringComparison.OrdinalIgnoreCase))
                ?? AiPresets.Match(current.BaseUrl);

            baseUrl = current.BaseUrl.Length > 0 ? current.BaseUrl : provider.BaseUrl;
            model = current.Model.Length > 0 ? current.Model : provider.Model;
            apiKey = current.ApiKey;

            Models = new ObservableCollection<string>(current.Models);

            if (Models.Count == 0 && model.Length > 0)
            {
                Models.Add(model);
            }

            streamTimer = new DispatcherTimer { Interval = StreamFlush };
            streamTimer.Tick += (_, _) => Drain();

            if (PromptLibrary.Seed(settings.Prompts) > 0)
            {
                settings.Save();
            }

            foreach (SavedPrompt saved in settings.Prompts)
            {
                Prompts.Add(saved);
            }

            Reload();

            Strings.Changed += () =>
            {
                OnPropertyChanged(nameof(Using));
                OnPropertyChanged(nameof(HelpTopics));
                OnPropertyChanged(nameof(HelpTips));
            };

            // Probed rather than assumed: V2 would not let you type until something had answered,
            // because sending into a dead endpoint only ever produced an error bubble.
            _ = CheckAsync();
        }

        public ObservableCollection<Turn> Turns { get; } = new();

        /// <summary>Everything saved so far, newest first.</summary>
        public ObservableCollection<SavedChat> History { get; } = new();

        /// <summary>The prompts worth keeping, shared with settings.</summary>
        public ObservableCollection<SavedPrompt> Prompts { get; } = new();

        /// <summary>Files put in front of the model with the next question.</summary>
        public ObservableCollection<Attachment> Attachments { get; } = new();

        public ObservableCollection<string> Models { get; }

        public IReadOnlyList<AiPreset> Providers { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NeedsKey))]
        [NotifyPropertyChangedFor(nameof(Using))]
        private AiPreset provider;

        [ObservableProperty]
        private string baseUrl;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Using))]
        private string model;

        /// <summary>Set while a provider switch is rewriting the model, so it is not announced.</summary>
        private bool switchingProvider;

        /// <summary>
        ///  Set while the model list is being replaced, so the half-built state is not saved.
        /// </summary>
        /// <remarks>
        ///  Clearing the list clears the selection with it (see <see cref="TakeModels"/>), and a
        ///  save at that instant would write an empty model and an empty list into the profile -
        ///  which a provider switch then reads straight back.
        /// </remarks>
        private bool holding;

        [ObservableProperty]
        private string apiKey;

        /// <summary>
        ///  Opens the settings page, where the endpoint is configured. Set by the window.
        /// </summary>
        public Action? ShowSettings { get; set; }

        [RelayCommand]
        private void OpenAiSettings() => ShowSettings?.Invoke();

        /// <summary>What this panel will ask, in one line: the model, and whose it is.</summary>
        public string Using => Strings.Format("AiUsing", Model, Provider.Name);

        partial void OnBaseUrlChanged(string value) => SaveQuietly();

        partial void OnApiKeyChanged(string value) => SaveQuietly();

        /// <summary>
        ///  Saves the endpoint as it is edited.
        /// </summary>
        /// <remarks>
        ///  The endpoint is configured on the settings page now, and nothing there sends a question
        ///  - so waiting for the next answer to save it, as this panel used to, would lose a key
        ///  typed in and never used before a restart.
        /// </remarks>
        private void SaveQuietly()
        {
            if (!switchingProvider && !holding)
            {
                Save();
            }
        }

        [ObservableProperty]
        private string prompt = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
        [NotifyPropertyChangedFor(nameof(CanType))]
        private bool isBusy;

        [ObservableProperty]
        private string status = Strings.Text("AiReady");

        #region Readiness
        /// <summary>False until a working endpoint has answered, which is what gates the tab.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyPropertyChangedFor(nameof(CanType))]
        [NotifyPropertyChangedFor(nameof(IsUnavailable))]
        private bool isReady;

        [ObservableProperty]
        private string hint = Strings.Text("AiLookingForModel");

        /// <summary>Shown only when the endpoint is one the app knows how to install.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSetUp))]
        private string setupLabel = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
        [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
        private bool isSettingUp;

        public bool CanSetUp => SetupLabel.Length > 0;

        public bool IsUnavailable => !IsReady;

        /// <summary>The box is open only when something answered and nothing is in flight.</summary>
        public bool CanType => IsReady && !IsBusy;

        /// <summary>
        ///  Asks the endpoint whether it is there, and opens or closes the tab accordingly.
        /// </summary>
        /// <remarks>
        ///  Listing models doubles as the reachability test: it is the cheapest call every one of
        ///  these endpoints implements, and its answer is also something the model picker wants.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanCheck))]
        private async Task Check()
        {
            await CheckAsync();
        }

        private bool CanCheck() => !IsSettingUp;

        private async Task CheckAsync()
        {
            Hint = Strings.Format("AiChecking", BaseUrl);

            try
            {
                List<string> names = await Client().ListModelsAsync(CancellationToken.None);

                if (names.Count > 0)
                {
                    TakeModels(names);
                }

                IsReady = true;
                SetupLabel = string.Empty;
                Hint = Strings.Format("AiModelOn", Model, BaseUrl);
                Status = Strings.Text("AiReady");
            }
            catch (Exception exception)
            {
                IsReady = false;

                // Only a local runtime can be offered for install; a hosted provider needs an
                // account and a key, which is not something to automate on someone's behalf.
                LocalAiProvider? local = LocalAiProvider.For(BaseUrl);

                SetupLabel = local is null ? string.Empty : Strings.Format("AiSetUpLabel", local.Name);
                Hint = Strings.Format("AiNothingAnswered", BaseUrl, exception.Message);
                Status = Strings.Text("AiNoEndpoint");
            }
        }

        /// <summary>Installs and starts the local runtime behind this endpoint.</summary>
        [RelayCommand(CanExecute = nameof(CanCheck))]
        private async Task SetUp()
        {
            if (LocalAiProvider.For(BaseUrl) is not { } local)
            {
                return;
            }

            IsSettingUp = true;
            Say(ChatRole.Notice, Strings.Format("AiSettingUp", local.Name));

            try
            {
                await local.PrepareAsync(
                    Client(),
                    Model,
                    milestone => Dispatcher.UIThread.Post(() => Say(ChatRole.Notice, milestone)),
                    line => Dispatcher.UIThread.Post(() => Hint = line),
                    CancellationToken.None);

                Say(ChatRole.Notice, Strings.Format("AiLocalReady", local.Name));
            }
            catch (Exception exception)
            {
                Say(ChatRole.Problem, exception.Message);
            }
            finally
            {
                IsSettingUp = false;
                await CheckAsync();
            }
        }
        #endregion

        partial void OnModelChanged(string value)
        {
            // V2 said so in the transcript, and it matters: the answers before and after this line
            // came from different models, and nothing else in the conversation records that.
            if (!switchingProvider && value.Length > 0 && Turns.Count > 0)
            {
                Say(ChatRole.Notice, Strings.Format("AiNowAsking", value));
            }

            if (IsReady)
            {
                Hint = Strings.Format("AiModelOn", value, BaseUrl);
            }

            SaveQuietly();
        }

        public bool NeedsKey => Provider.NeedsKey;

        public bool IsEmpty => Turns.Count == 0;

        /// <summary>True when the newest answer had a command in it worth keeping.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLastCommand))]
        private bool hasSavableReply;

        partial void OnProviderChanged(AiPreset value)
        {
            switchingProvider = true;


            // Switching provider carries its own endpoint and default model with it, otherwise the
            // previous provider's URL is silently used against the new one.
            BaseUrl = value.BaseUrl;
            Model = value.Model;

            AiProfile profile = settings.AiProfileFor(value);
            ApiKey = profile.ApiKey;

            Models.Clear();

            foreach (string name in profile.Models.Count > 0 ? profile.Models : [value.Model])
            {
                Models.Add(name);
            }

            Model = profile.Model.Length > 0 ? profile.Model : value.Model;

            switchingProvider = false;

            Save();

            // A different endpoint is a different question about whether anything is there.
            _ = CheckAsync();
        }

        /// <summary>
        ///  Replaces the model list, keeping the model that is already chosen.
        /// </summary>
        /// <remarks>
        ///  The reason this is a method rather than three lines at each call site: clearing the
        ///  collection changes the selection as a side effect, and that side effect used to lose
        ///  the user's model on every refresh.
        ///
        ///  The picker binds SelectedItem two-way. Emptying the list leaves it with a selection
        ///  that is no longer in the list, so it selects nothing and writes that back - Model is
        ///  empty before the new names are even added. The test for "is the chosen model still
        ///  offered" then compared an empty string against the list, found it missing, and fell to
        ///  the first name. So refreshing a list that still contained the user's model would
        ///  silently move them to whatever happened to sort first.
        ///
        ///  Reading it into a local before the clear is the whole fix: the answer is taken while
        ///  it is still true, and put back afterwards.
        /// </remarks>
        private void TakeModels(List<string> names)
        {
            holding = true;

            try
            {
                Replace(names);
            }
            finally
            {
                holding = false;
            }

            SaveQuietly();
        }

        private void Replace(List<string> names)
        {
            string chosen = Model;

            Models.Clear();

            foreach (string name in names)
            {
                Models.Add(name);
            }

            // Still offered, so keep it - even though the binding has cleared it by now.
            if (names.Contains(chosen))
            {
                Model = chosen;

                return;
            }

            // Genuinely gone: the endpoint no longer serves it, or the user pointed at a different
            // one. Falling to the first is better than leaving a selection nothing can answer.
            if (names.Count > 0)
            {
                Model = names[0];
            }
            else if (chosen.Length > 0)
            {
                // Nothing came back at all, which is a broken endpoint rather than a model that
                // was withdrawn. Keep what they had; the status line says the list is empty.
                Models.Add(chosen);
                Model = chosen;
            }
        }

        [RelayCommand]
        private async Task RefreshModels()
        {
            Status = Strings.Text("AiAskingEndpoint");

            try
            {
                List<string> names = await Client().ListModelsAsync(CancellationToken.None);

                TakeModels(names);

                Status = names.Count > 0
                    ? Strings.Format(
                        names.Count == 1 ? "AiModelsAvailableOne" : "AiModelsAvailable", names.Count)
                    : Strings.Text("AiNoModels");
            }
            catch (Exception exception)
            {
                Status = Strings.Format("AiCouldNotReach", BaseUrl, exception.Message);
            }
        }

        #region Asking
        /// <summary>
        ///  Hands a failed run to the model with the output already attached.
        /// </summary>
        /// <remarks>
        ///  The reason this is worth a button is the copying. Explaining a failure by hand means
        ///  selecting the right part of a log, pasting it somewhere, and writing the sentence about
        ///  what was being run - and the part people get wrong is the selection, because the useful
        ///  lines are rarely the last ones and almost never the first.
        ///
        ///  The output goes in as a fenced block so a model reading it can tell the log from the
        ///  question, and the command itself goes in above it, because "why did this fail" is not
        ///  answerable without knowing what was run.
        /// </remarks>
        public void Explain(string command, string body, string output, int exitCode)
        {
            if (IsBusy)
            {
                return;
            }

            Prompt = $"""
                This command failed with exit code {exitCode}. What went wrong, and what should I change?

                It is called "{command}" and this is what it runs:

                ```
                {body.Trim()}
                ```

                This is the output:

                ```
                {output.Trim()}
                ```
                """;

            if (SendCommand.CanExecute(null))
            {
                SendCommand.Execute(null);
            }
            else
            {
                // The endpoint is not up. The question is left in the box rather than thrown away,
                // so pressing Send after fixing that sends the same thing.
                Status = Strings.Text("AiQuestionReady");
            }
        }

        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task Send()
        {
            string asked = Prompt.Trim();

            if (asked.Length == 0)
            {
                return;
            }

            Prompt = string.Empty;
            IsBusy = true;
            Status = Strings.Format("AiAsking", Model);
            HasSavableReply = false;

            // What is shown is what was typed; what is sent has the files in front of it. Pasting
            // a whole file into the transcript would bury the question under it.
            string carried = WithAttachments(asked);

            AddTurn(new Turn(ChatRole.User, asked));
            history.Add(new ChatMessage("user", carried));
            Remember("user", asked);

            Turn reply = new(ChatRole.Assistant, string.Empty);
            AddTurn(reply);

            streaming = reply;
            streamTimer.Start();

            cancellation = new CancellationTokenSource();

            try
            {
                // The briefing goes in front of the history every time, not once at the start: it
                // carries the workspace, which the user can change mid-conversation, and a system
                // message buried twenty turns back is the first thing a short context window drops.
                List<ChatMessage> sent =
                [
                    new ChatMessage("system", AiBriefing.For(workingDirectory())),
                    .. Recent(),
                ];

                AiClient client = Client();

                await client.StreamAsync(sent, Buffer, cancellation.Token);

                Drain();

                string answer = reply.Raw.ToString();

                history.Add(new ChatMessage("assistant", answer));
                Remember("assistant", answer);

                HasSavableReply = CodeBlock.LastCommand(answer).Length > 0;
                Status = "Ready.";

                Reckon(client, sent, answer);

                Save();
                Keep();
            }
            catch (OperationCanceledException)
            {
                Drain();
                reply.Append("\n\n_Stopped._");
                Status = "Stopped.";
            }
            catch (Exception exception)
            {
                Drain();

                // Named endpoint and the command that fixes it, rather than "connection refused".
                string explained = AiBriefing.Describe(exception, Client());

                reply.Append("\n\n" + Strings.Format("AiNoAnswer", explained));
                Status = Strings.Text("AiFailed");
            }
            finally
            {
                streamTimer.Stop();
                streaming = null;

                IsBusy = false;
                cancellation?.Dispose();
                cancellation = null;

                ScrollToEnd?.Invoke();
            }
        }

        /// <summary>
        ///  Takes one streamed fragment off the network thread.
        /// </summary>
        /// <remarks>
        ///  Nothing here touches the UI: the text joins a buffer, and the timer picks it up on the
        ///  UI thread a frame later. The reader thread never blocks on the UI thread, and the UI
        ///  thread never sees more than sixty updates a second however fast the model is.
        /// </remarks>
        private void Buffer(string delta)
        {
            lock (gate)
            {
                incoming.Append(delta);
            }
        }

        /// <summary>Moves whatever has arrived into the open turn. UI thread only.</summary>
        private void Drain()
        {
            string text;

            lock (gate)
            {
                if (incoming.Length == 0)
                {
                    return;
                }

                text = incoming.ToString();
                incoming.Clear();
            }

            streaming?.Append(text);
            ScrollToEnd?.Invoke();
        }

        /// <summary>The tail of the conversation, oldest first, for sending back as context.</summary>
        private List<ChatMessage> Recent() =>
            history.Count <= MaxHistory ? history : history[^MaxHistory..];

        [RelayCommand(CanExecute = nameof(IsBusy))]
        private void Stop() => cancellation?.Cancel();

        /// <summary>
        ///  Puts the conversation away and starts an empty one.
        /// </summary>
        /// <remarks>
        ///  Saves first. "Clear chat" used to mean "lose this", which made it a button people
        ///  hesitated over - and hesitating over the button that tidies up is how a conversation
        ///  ends up twenty turns long and out of context window.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanClear))]
        private void Clear()
        {
            bool kept = Keep();

            Turns.Clear();
            history.Clear();
            Attachments.Clear();
            current = new SavedChat();

            HasSavableReply = false;
            Spent = string.Empty;
            Context = string.Empty;

            OnPropertyChanged(nameof(IsEmpty));

            Status = Strings.Text(kept ? "AiSavedNewOne" : "AiCleared");
        }

        // Clearing mid-answer would drop the history the reply is still being appended to.
        private bool CanClear() => !IsBusy;

        private bool CanSend() => IsReady && !IsBusy;
        #endregion

        #region Attachments
        /// <summary>Opens a file chooser. Set by the view, which owns the top level.</summary>
        public Func<Task<IReadOnlyList<string>>>? PickFiles { get; set; }

        [ObservableProperty]
        private string attachmentNote = string.Empty;

        public bool HasAttachments => Attachments.Count > 0;

        [RelayCommand]
        private async Task Attach()
        {
            if (PickFiles is not { } pick)
            {
                return;
            }

            foreach (string path in await pick())
            {
                Add(path);
            }
        }

        /// <summary>Takes a file that was chosen or dropped on the window.</summary>
        public void Add(string path)
        {
            if (Attachments.Any(held => string.Equals(held.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                AttachmentNote = Strings.Format("AiAlreadyAttached", System.IO.Path.GetFileName(path));
                return;
            }

            if (Attachment.Read(path, out string problem) is not { } attachment)
            {
                AttachmentNote = problem;
                return;
            }

            Attachments.Add(attachment);
            OnPropertyChanged(nameof(HasAttachments));

            AttachmentNote = Strings.Format("AiAttachedNote", attachment.Summary);
        }

        [RelayCommand]
        private void Detach(Attachment? attachment)
        {
            if (attachment is null)
            {
                return;
            }

            Attachments.Remove(attachment);
            OnPropertyChanged(nameof(HasAttachments));

            AttachmentNote = Attachments.Count == 0
                ? string.Empty
                : Strings.Format("AiCountAttached", Attachments.Count);
        }

        /// <summary>
        ///  Puts the attached files in front of the question, and lets go of them.
        /// </summary>
        /// <remarks>
        ///  One-shot, like a command's supplied parameters and for the same reason: the text is now
        ///  in the history and goes back with every following turn anyway, so keeping the
        ///  attachment would send the file again per question and quietly double the bill. The
        ///  transcript records that it went, so nothing is lost by the chip disappearing.
        /// </remarks>
        private string WithAttachments(string asked)
        {
            if (Attachments.Count == 0)
            {
                return asked;
            }

            StringBuilder built = new();

            foreach (Attachment attachment in Attachments)
            {
                built.Append(attachment.AsContext()).Append("\n\n");
            }

            built.Append(asked);

            Say(
                ChatRole.Notice,
                Attachments.Count == 1
                    ? Strings.Format("AiSentWithOne", Attachments[0].Summary)
                    : Strings.Format(
                        "AiSentWithMany",
                        Attachments.Count,
                        string.Join(", ", Attachments.Select(file => file.Name))));

            Attachments.Clear();
            OnPropertyChanged(nameof(HasAttachments));
            AttachmentNote = string.Empty;

            return built.ToString();
        }
        #endregion

        #region Saved conversations
        /// <summary>Rereads the archive, so the list matches the folder after a save or a delete.</summary>
        private void Reload()
        {
            History.Clear();

            foreach (SavedChat chat in ChatArchive.All())
            {
                History.Add(chat);
            }

            OnPropertyChanged(nameof(HasHistory));
        }

        public bool HasHistory => History.Count > 0;

        /// <summary>Adds one turn to what will be written.</summary>
        private void Remember(string role, string text)
        {
            if (current.Turns.Count == 0)
            {
                current.Title = ChatArchive.TitleFor(text);
                current.At = DateTime.Now;
            }

            current.Turns.Add(new SavedTurn(role, text, DateTime.Now));
            current.Model = Model;
        }

        /// <summary>Writes the conversation out, and says whether there was one to write.</summary>
        private bool Keep()
        {
            if (current.Turns.Count == 0 || ChatArchive.Save(current) is null)
            {
                return false;
            }

            Reload();

            return true;
        }

        /// <summary>
        ///  Puts a saved conversation back on screen.
        /// </summary>
        /// <remarks>
        ///  Restores the history as well as the transcript, so a reopened conversation can be
        ///  continued rather than only read - which is the difference between an archive and a
        ///  scrollback.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanClear))]
        private void OpenChat(SavedChat? chat)
        {
            if (chat is null)
            {
                return;
            }

            Keep();

            Turns.Clear();
            history.Clear();
            Attachments.Clear();
            OnPropertyChanged(nameof(HasAttachments));

            // Reread rather than trusting the listing: the list was built once, and the file may
            // have grown since if the same conversation is open in another copy of the app.
            SavedChat opened =
                ChatArchive.Read(System.IO.Path.Combine(ChatArchive.Folder, chat.Id + ".json")) ?? chat;

            foreach (SavedTurn turn in opened.Turns)
            {
                AddTurn(new Turn(turn.Role == "user" ? ChatRole.User : ChatRole.Assistant, turn.Text));
                history.Add(new ChatMessage(turn.Role, turn.Text));
            }

            current = opened;

            HasSavableReply = Turns.LastOrDefault(turn => turn.IsAssistant) is { } last
                && CodeBlock.LastCommand(last.Raw.ToString()).Length > 0;

            Spent = string.Empty;
            Context = string.Empty;

            OnPropertyChanged(nameof(IsEmpty));
            ScrollToEnd?.Invoke();

            Status = Strings.Format("AiOpened", opened.Title);
        }

        [RelayCommand]
        private void Forget(SavedChat? chat)
        {
            if (chat is null)
            {
                return;
            }

            ChatArchive.Delete(chat.Id);

            // If it was the one on screen, the screen keeps it - it just has no file any more, and
            // the next exchange writes it out afresh under a new id.
            if (current.Id == chat.Id)
            {
                current.Id = string.Empty;
            }

            Reload();

            Status = Strings.Format("AiDeleted", chat.Title);
        }
        #endregion

        #region Prompts
        /// <summary>
        ///  Drops a saved prompt into the box rather than sending it.
        /// </summary>
        /// <remarks>
        ///  Because most of them have a <c>{{placeholder}}</c> in them the user has to fill in, and
        ///  because a library that fires on selection is one misclick away from asking a question
        ///  nobody meant to pay for.
        /// </remarks>
        [RelayCommand]
        private void UsePrompt(SavedPrompt? saved)
        {
            if (saved is null)
            {
                return;
            }

            Prompt = Prompt.Trim().Length == 0 ? saved.Body : Prompt.TrimEnd() + "\n\n" + saved.Body;

            Status = Strings.Format("AiLoadedPrompt", saved.Name);
            FocusComposer?.Invoke();
        }

        /// <summary>Saves whatever is in the box as a prompt.</summary>
        [RelayCommand]
        private void SavePrompt()
        {
            string body = Prompt.Trim();

            if (body.Length == 0)
            {
                Status = Strings.Text("AiNothingToSave");
                return;
            }

            // Named after its own first line, the same way a conversation is. A dialog asking for a
            // name is one more step between having a good prompt and keeping it.
            string name = ChatArchive.TitleFor(body);

            SavedPrompt saved = new() { Name = name, Body = body };

            settings.Prompts.RemoveAll(existing =>
                string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));

            settings.Prompts.Add(saved);
            settings.Save();

            if (Prompts.FirstOrDefault(existing =>
                string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)) is { } shown)
            {
                Prompts.Remove(shown);
            }

            Prompts.Add(saved);

            Status = Strings.Format("AiSavedPrompt", name);
        }

        [RelayCommand]
        private void DeletePrompt(SavedPrompt? saved)
        {
            if (saved is null)
            {
                return;
            }

            settings.Prompts.RemoveAll(existing =>
                string.Equals(existing.Name, saved.Name, StringComparison.OrdinalIgnoreCase));

            settings.Save();
            Prompts.Remove(saved);

            Status = Strings.Format("AiDeleted", saved.Name);
        }

        /// <summary>Focuses the box after a prompt is loaded. Set by the view.</summary>
        public Action? FocusComposer { get; set; }
        #endregion

        #region What it cost
        /// <summary>What the last exchange came to, and the session with it.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowsSpend))]
        private string spent = string.Empty;

        /// <summary>"31% of a 128k window" - the sentence that explains a model losing the thread.</summary>
        [ObservableProperty]
        private string context = string.Empty;

        /// <summary>True once the window is filling up, which is when the line changes colour.</summary>
        [ObservableProperty]
        private bool contextIsTight;

        public bool ShowsSpend => Spent.Length > 0;

        private decimal session;

        /// <summary>
        ///  Works out what the exchange cost and how full the window is.
        /// </summary>
        /// <remarks>
        ///  Measured counts where the endpoint reports them, estimates where it does not, and the
        ///  readout says which by keeping or dropping the tilde. The running total is per session
        ///  rather than per conversation, because the question it answers - "is this costing me
        ///  anything" - is about the afternoon, not about one thread.
        /// </remarks>
        private void Reckon(AiClient client, List<ChatMessage> sent, string answer)
        {
            bool measured = client.LastUsage is not null;

            (int input, int output) = client.LastUsage
                ?? (TokenCount.Of(sent), TokenCount.Of(answer));

            Spend spend = TokenCount.Reckon(BaseUrl, Model, input, output, measured);

            session += spend.Money;

            Spent = session > spend.Money
                ? Strings.Format(
                    "AiSpendSession",
                    spend.Describe(),
                    session.ToString("0.####", CultureInfo.InvariantCulture))
                : spend.Describe();

            if (TokenCount.WindowFor(Model) is not { } window)
            {
                // Nothing in the name said how big the window is, and inventing one would be worse
                // than saying nothing: the whole value of this line is that it can be trusted.
                Context = string.Empty;
                ContextIsTight = false;

                return;
            }

            int held = TokenCount.Of(Recent()) + TokenCount.Of(AiBriefing.For(workingDirectory()));
            int percent = (int)Math.Round(held * 100.0 / window);

            Context = Strings.Format("AiContextWindow", percent, window / 1024);
            ContextIsTight = percent >= 70;

            if (ContextIsTight)
            {
                Context += Strings.Text("AiContextTight");
            }
        }
        #endregion

        #region Code
        /// <summary>Saves a code block as a command and shows it in the deck.</summary>
        [RelayCommand]
        private void AddAsCommand(Segment? segment)
        {
            if (segment is null || !segment.IsCode)
            {
                return;
            }

            addCommand(Build(segment));
            showCommands();
        }

        /// <summary>Saves the command out of the newest answer, as V2's Save as command did.</summary>
        [RelayCommand(CanExecute = nameof(HasSavableReply))]
        private void SaveLast()
        {
            Turn? last = Turns.LastOrDefault(turn => turn.IsAssistant);

            if (last is null)
            {
                return;
            }

            (string body, CommandKind kind) = CodeBlock.Last(last.Raw.ToString());

            if (body.Length == 0)
            {
                Say(ChatRole.Notice, Strings.Text("AiNoCommandInAnswer"));
                return;
            }

            addCommand(new CustomCommand { Name = Name(body), Command = body, Kind = kind });
            showCommands();
        }

        /// <summary>
        ///  Runs a code block and streams its output into the transcript.
        /// </summary>
        /// <remarks>
        ///  V2 ran code from the transcript and echoed the output back into it, which is the whole
        ///  point of asking for a script and then pressing Run: the answer and what it did are read
        ///  in one place. V3 instead saved it to the deck and jumped to another page, which lost
        ///  the thread of the conversation and left a one-off snippet saved for ever.
        ///
        ///  Output is batched exactly as <see cref="CommandItem"/> batches it, and for the same
        ///  reason - a chatty script otherwise posts a dispatcher job per line and the window stops
        ///  answering.
        /// </remarks>
        /// <summary>
        ///  Asked once per session before any model-written code is run.
        /// </summary>
        /// <remarks>
        ///  V2 put this in a message box. Here it is a bar across the bottom of the transcript
        ///  instead, because a modal over a chat window hides the very code it is asking about.
        /// </remarks>
        private bool acceptedRunningGeneratedCode;

        [ObservableProperty]
        private bool isConfirmingRun;

        private Segment? pending;

        /// <summary>What the confirmation bar says it is about to do.</summary>
        public string ConfirmBlurb => Strings.Format("AiConfirmBlurb", workingDirectory());

        [RelayCommand]
        private void ConfirmRun()
        {
            acceptedRunningGeneratedCode = true;
            IsConfirmingRun = false;

            Segment? wanted = pending;
            pending = null;

            if (wanted is { IsChain: true })
            {
                RunChainCommand.Execute(wanted);
            }
            else if (wanted is not null)
            {
                RunCodeCommand.Execute(wanted);
            }
        }

        [RelayCommand]
        private void CancelRun()
        {
            pending = null;
            IsConfirmingRun = false;
            Status = Strings.Text("AiNotRun");
        }

        [RelayCommand(CanExecute = nameof(CanRunCode))]
        private async Task RunCode(Segment? segment)
        {
            if (segment is null || !segment.IsCode || IsRunning)
            {
                return;
            }

            // Running somewhere the user did not choose is worse than not running at all: the
            // command was written for their project, not for whatever folder the app started in.
            if (workingDirectory().Length == 0)
            {
                Say(ChatRole.Problem, Strings.Text("AiNeedWorkspace"));
                return;
            }

            if (!acceptedRunningGeneratedCode)
            {
                pending = segment;
                IsConfirmingRun = true;
                OnPropertyChanged(nameof(ConfirmBlurb));

                return;
            }

            CustomCommand created = Build(segment);

            IsRunning = true;

            Turn output = new(ChatRole.Output, $"$ {created.Name}\n");
            AddTurn(output);

            running = new CancellationTokenSource();

            StringBuilder batch = new();
            object batchGate = new();
            bool queued = false;

            void Emit(string line, LogLevel level)
            {
                lock (batchGate)
                {
                    batch.Append(line).Append('\n');

                    if (queued)
                    {
                        return;
                    }

                    queued = true;
                }

                Dispatcher.UIThread.Post(
                    () =>
                    {
                        string text;

                        lock (batchGate)
                        {
                            text = batch.ToString();
                            batch.Clear();
                            queued = false;
                        }

                        output.Append(text);
                        ScrollToEnd?.Invoke();
                    },
                    DispatcherPriority.Background);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            ScriptFile? script = null;

            try
            {
                script = ScriptFile.Create(created);

                int exit = await ProcessRunner.RunAsync(
                    script.FileName,
                    script.Arguments,
                    workingDirectory(),
                    Emit,
                    cancellationToken: running.Token);

                output.Append("\n" + Strings.Format("AiExitCode", exit, Describe(stopwatch.Elapsed)));

                Status = exit == 0
                    ? Strings.Text("AiRanCleanly")
                    : Strings.Format("AiExited", exit);
            }
            catch (OperationCanceledException)
            {
                output.Append("\n" + Strings.Format("AiStoppedAfter", Describe(stopwatch.Elapsed)));

                Status = Strings.Text("AiStopped");
            }
            catch (Exception exception)
            {
                output.Append("\n" + Strings.Format("AiCouldNotRunLine", exception.Message));

                Status = Strings.Format("AiCouldNotRun", exception.Message);
            }
            finally
            {
                script?.Dispose();

                IsRunning = false;
                running?.Dispose();
                running = null;

                ScrollToEnd?.Invoke();
            }
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCodeCommand))]
        [NotifyCanExecuteChangedFor(nameof(RunChainCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCodeCommand))]
        [NotifyPropertyChangedFor(nameof(RunLabel))]
        private bool isRunning;

        private bool CanRunCode() => !IsRunning;

        public string RunLabel => IsRunning ? "Running" : "Run";

        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void StopCode()
        {
            // A chain is stopped through its page, which also stops the step it is on.
            if (runningChain is { } chain)
            {
                Automation?.StopChainCommand.Execute(chain);

                return;
            }

            running?.Cancel();
        }

        private static CustomCommand Build(Segment segment) => new()
        {
            Name = segment.Name.Length > 0 ? segment.Name : Name(segment.Text),
            Command = segment.Text,
            Kind = CodeBlock.KindFor(segment.Language, segment.Text),
        };
        #endregion

        #region Help
        /// <summary>
        ///  What the help panel lists, each with a prompt that shows it off.
        /// </summary>
        /// <remarks>
        ///  A prompt to press rather than a paragraph to read. The commonest reason a feature here
        ///  goes unused is not knowing it can be asked for - nobody guesses that a chat box will
        ///  build an automation chain - and one example that works teaches the phrasing better
        ///  than a description of it.
        /// </remarks>
        public IReadOnlyList<HelpTopic> HelpTopics =>
        [
            new("AiHelpCommandTitle", "AiHelpCommandText", "AiHelpCommandExample"),
            new("AiHelpChainTitle", "AiHelpChainText", "AiHelpChainExample"),
            new("AiHelpFailureTitle", "AiHelpFailureText", "AiHelpFailureExample"),
            new("AiHelpFilesTitle", "AiHelpFilesText", "AiHelpFilesExample"),
            new("AiHelpRefineTitle", "AiHelpRefineText", "AiHelpRefineExample"),
        ];

        /// <summary>How to ask well, read in the current language.</summary>
        public IReadOnlyList<string> HelpTips =>
        [
            .. new[] { "AiHelpTip1", "AiHelpTip2", "AiHelpTip3", "AiHelpTip4", "AiHelpTip5", "AiHelpTip6" }
                .Select(Strings.Text),
        ];

        /// <summary>Puts an example in the box, for the user to send as it is or change first.</summary>
        /// <remarks>
        ///  Not sent. An example is a starting point, and sending it unasked would spend a round
        ///  trip - and, on a hosted model, money - on a question nobody asked yet.
        /// </remarks>
        [RelayCommand]
        private void UseExample(HelpTopic? topic)
        {
            if (topic is null)
            {
                return;
            }

            Prompt = topic.Example;
            FocusComposer?.Invoke();
        }
        #endregion

        #region Chains
        /// <summary>
        ///  The automation page, where a chain from an answer is added and run. Set by the window.
        /// </summary>
        /// <remarks>
        ///  A reference one way only: the automation page knows nothing of the assistant, so the
        ///  two can still be built in either order.
        /// </remarks>
        public AutomationViewModel? Automation { get; set; }

        /// <summary>Opens the automation page on a chain. Set by the window.</summary>
        public Action<ChainItem>? ShowChain { get; set; }

        /// <summary>The deck's command of this name, if there is one. Set by the window.</summary>
        public Func<string, CustomCommand?>? FindCommand { get; set; }

        /// <summary>A command name the deck does not have yet. Set by the window.</summary>
        public Func<string, string>? UniqueCommandName { get; set; }

        /// <summary>The chain running from the transcript, so Stop knows what to stop.</summary>
        private ChainItem? runningChain;

        /// <summary>Creates the chain an answer describes, and opens it on the automation page.</summary>
        [RelayCommand]
        private void CreateChain(Segment? segment)
        {
            if (Make(segment) is { } chain)
            {
                ShowChain?.Invoke(chain);
            }
        }

        /// <summary>
        ///  Runs the chain an answer describes, here, with every step's output in the transcript.
        /// </summary>
        /// <remarks>
        ///  The chain equivalent of Run on a code block, and for the same reason: the answer and
        ///  what it did are read in one place, without going to another page to watch it. It is
        ///  run by the automation page all the same - created there first if it is not yet - so it
        ///  behaves exactly as it does when started from there, stop-on-failure and all.
        ///
        ///  Gated by the same once-a-session question as a code block, because it is the same
        ///  thing: scripts a model wrote, about to run against the user's project.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanRunCode))]
        private async Task RunChain(Segment? segment)
        {
            if (segment is not { IsChain: true } || IsRunning || Automation is not { } automation)
            {
                return;
            }

            if (workingDirectory().Length == 0)
            {
                Say(ChatRole.Problem, Strings.Text("AiNeedWorkspace"));
                return;
            }

            if (!acceptedRunningGeneratedCode)
            {
                pending = segment;
                IsConfirmingRun = true;
                OnPropertyChanged(nameof(ConfirmBlurb));

                return;
            }

            if (Make(segment) is not { } chain)
            {
                return;
            }

            IsRunning = true;
            runningChain = chain;

            Turn output = new(ChatRole.Output, Strings.Format("AiChainHeader", chain.Name) + "\n");
            AddTurn(output);

            // The chain writes its log on the UI thread a line at a time. Gathered and handed to the
            // transcript once per pass, as a code block's output is, so a chatty step does not
            // re-split the whole turn for every line it prints.
            StringBuilder batch = new();
            bool queued = false;

            void Copy(object? sender, NotifyCollectionChangedEventArgs change)
            {
                if (change.NewItems is not { } lines
                    || change.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace))
                {
                    return;
                }

                // A replacement is a step's heading settling to its tick or cross. The transcript is
                // written forwards only, so the outcome goes in as a line of its own under the
                // step's output, which reads the same way: what ran, what it said, how it went.
                foreach (OutputLine line in lines.OfType<OutputLine>())
                {
                    batch.Append(line.Text).Append('\n');
                }

                if (queued)
                {
                    return;
                }

                queued = true;

                Dispatcher.UIThread.Post(
                    () =>
                    {
                        output.Append(batch.ToString());
                        batch.Clear();
                        queued = false;
                        ScrollToEnd?.Invoke();
                    },
                    DispatcherPriority.Background);
            }

            chain.Output.CollectionChanged += Copy;

            try
            {
                await automation.RunChainCommand.ExecuteAsync(chain);

                // Behind the last batch, at the same priority, so the summary lands after it.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

                output.Append("\n" + chain.Status);
                Status = chain.Status;
            }
            catch (Exception exception)
            {
                output.Append("\n" + Strings.Format("AiCouldNotRunLine", exception.Message));
                Status = Strings.Format("AiCouldNotRun", exception.Message);
            }
            finally
            {
                chain.Output.CollectionChanged -= Copy;
                runningChain = null;
                IsRunning = false;

                ScrollToEnd?.Invoke();
            }
        }

        /// <summary>
        ///  The chain an answer describes, made if it has not been already.
        /// </summary>
        /// <remarks>
        ///  Remembered on the block, so Create chain and then Run chain - or Run chain twice - use
        ///  the one chain rather than adding "Report 2" and "Report 3". Made again if it has since
        ///  been deleted on the automation page.
        ///
        ///  Read from the whole reply rather than the block the button sits on, because the chain
        ///  block is only a list of names - the scripts are in the blocks above it.
        ///
        ///  A command that is already in the deck under the same name is used as it is when the
        ///  script is the same, so asking twice does not add it twice. When the script differs it
        ///  is added beside the existing one under a new name, and the chain points at that: the
        ///  user's own command is never overwritten by something a model wrote.
        /// </remarks>
        private ChainItem? Make(Segment? segment)
        {
            if (segment is not { IsChain: true } || Automation is not { } automation)
            {
                return null;
            }

            if (segment.Chain is { } made && automation.Chains.Contains(made))
            {
                return made;
            }

            if (Turns.FirstOrDefault(turn => turn.Segments.Contains(segment)) is not { } turn)
            {
                return null;
            }

            if (ChainPlan.Parse(turn.Raw.ToString()) is not { } plan)
            {
                Say(ChatRole.Notice, Strings.Text("AiNoChainInAnswer"));

                return null;
            }

            Dictionary<string, string> renamed = new(StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (PlannedCommand planned in plan.Commands)
            {
                CustomCommand? existing = FindCommand?.Invoke(planned.Name);

                if (existing is not null && Same(existing.Command, planned.Body))
                {
                    continue;
                }

                CustomCommand command = planned.ToCommand();

                if (existing is not null)
                {
                    command.Name = UniqueCommandName?.Invoke(planned.Name) ?? $"{planned.Name} 2";
                    renamed[planned.Name] = command.Name;

                    Say(ChatRole.Notice, Strings.Format("AiChainRenamed", planned.Name, command.Name));
                }

                addCommand(command);
                added++;
            }

            List<string> steps = [.. plan.Steps.Select(step => renamed.TryGetValue(step, out string? to) ? to : step)];

            List<string> missing = [.. steps
                .Where(step => FindCommand?.Invoke(step) is null)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            ChainItem chain = automation.Add(new CommandChain
            {
                Name = plan.Name,
                Steps = steps,
                StopOnFailure = plan.StopOnFailure,
            });

            segment.Chain = chain;

            Say(ChatRole.Notice, Strings.Format("AiChainCreated", chain.Name, steps.Count, added));

            if (missing.Count > 0)
            {
                Say(ChatRole.Notice, Strings.Format(
                    "AiChainMissing", string.Join(", ", missing.Select(step => $"\"{step}\""))));
            }

            return chain;
        }

        /// <summary>The same script, give or take line endings and trailing space.</summary>
        private static bool Same(string left, string right) =>
            left.ReplaceLineEndings("\n").Trim() == right.ReplaceLineEndings("\n").Trim();
        #endregion

        /// <summary>Raised when the transcript should be scrolled down. Set by the view.</summary>
        public Action? ScrollToEnd { get; set; }

        /// <summary>Puts text on the clipboard. Set by the view, which owns the top level.</summary>
        public Action<string>? Copy { get; set; }

        /// <summary>V2 offered both of these on a right click over the transcript.</summary>
        [RelayCommand]
        private void CopyTurn(Turn? turn)
        {
            if (turn is not null)
            {
                Copy?.Invoke(turn.Raw.ToString());
            }
        }

        [RelayCommand]
        private void CopyAll()
        {
            Copy?.Invoke(string.Join(
                Environment.NewLine + Environment.NewLine,
                Turns.Select(turn => $"{turn.Who} {turn.Stamp}{Environment.NewLine}{turn.Raw}")));
        }

        /// <summary>A line from the app itself - progress, a failure, or a note.</summary>
        private void Say(ChatRole role, string message) => AddTurn(new Turn(role, message));

        private void AddTurn(Turn turn)
        {
            Turns.Add(turn);
            OnPropertyChanged(nameof(IsEmpty));
            ScrollToEnd?.Invoke();
        }

        /// <summary>A readable name from the code itself, so the deck does not fill with "Snippet".</summary>
        private static string Name(string body)
        {
            string first = body
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#')) ?? "Snippet";

            return first.Length <= 40 ? first : first[..40].TrimEnd() + "…";
        }

        private static string Describe(TimeSpan elapsed) => elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds:F0} ms"
            : elapsed.TotalSeconds < 60
                ? $"{elapsed.TotalSeconds:F1} s"
                : $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";

        private AiClient Client() => new()
        {
            BaseUrl = BaseUrl,
            Model = Model,
            ApiKey = ApiKey,
        };

        /// <summary>Writes the endpoint back to the profile so it survives a restart.</summary>
        private void Save()
        {
            AiProfile profile = settings.AiProfileFor(Provider);

            profile.BaseUrl = BaseUrl;
            profile.Model = Model;
            profile.ApiKey = ApiKey;
            profile.Models = Models.ToList();

            settings.AiProvider = Provider.Name;
            settings.AiBaseUrl = BaseUrl;
            settings.AiModel = Model;
            settings.AiApiKey = ApiKey;

            settings.Save();
        }
    }
}
