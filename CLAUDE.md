# DevDeck

A cross-platform developer command deck: .NET 10, C# 14, Avalonia 12, CommunityToolkit.Mvvm.

- `src/DevDeck.Core` - everything that is not UI. No UI package may be referenced here.
- `src/DevDeck.App` - the Avalonia app: views, view models, themes, icons.
- `tests/DevDeck.Core.Tests` - xUnit. It is not in the solution; run it directly:
  `dotnet test tests/DevDeck.Core.Tests`.

## Where things are

- **Running a command:** `Core/Commands/ScriptFile.cs` writes the temp script and builds the command
  line per kind; `App/ViewModels/CommandItem.cs` runs it and owns its output.
- **The deck:** `App/ViewModels/CommandsViewModel.cs` (in-use marks, link pills, reordering);
  `App/Views/CommandsView.axaml(.cs)`.
- **Chains and watches:** `App/ViewModels/AutomationViewModel.cs` (`RunChain` is the runner);
  `Core/Commands/CommandChain.cs`, `FileWatch.cs`, `RunContext.cs` (the `DEVDECK_*` variables a step
  gets), `ChainOutputs.cs` (the per-run `stepN.txt` folder).
- **Assistant:** `App/ViewModels/AssistantViewModel.cs`; `Core/Ai/AiBriefing.cs` (the system
  message, including the chain format), `ChainPlan.cs` (reads a chain out of a reply),
  `ScriptConversion.cs` (rewrites a command for a new Run with).
- **Changelog page:** `Core/Platform/Changelog.cs` parses `CHANGELOG.md`, which is embedded in the
  build. Headings are `## x.y.z - YYYY-MM-DD HH:mm`.
- **Startup:** `App/App.axaml.cs` shows `Views/Splash` first and builds the deck behind it, except at
  sign-in (minimised), when there is no splash.
- **Icon:** `tools/make-icon.ps1` draws the mark the splash shows into `Assets/DevDeck.ico` and
  `docs/images/icon.png`. Change the mark there, not by editing the .ico.
- **Themes:** one `ResourceDictionary` per theme in `App/Themes`, all defining the same brush keys;
  listed in `Core/Platform/AppTheme.cs`. Themes define brushes, not colours.
- **Shared view helpers:** `ListReorder` (drag and Alt+Up/Down for a list), `TextMode` (Select
  text), `MarkdownText` and `AnsiText` (inlines built in code).

## Releasing a version

A release is not done until every step below is. Do all of them, without being asked.

1. **Version:** bump `<Version>` in `Directory.Build.props`, the only place it is written. Bump the
   **patch** number (1.2.0 -> 1.2.1) unless told otherwise. "Minor change" means the patch number.
2. **Changelog:** add a `## x.y.z - YYYY-MM-DD HH:mm` section, with the release time, at the top
   of `CHANGELOG.md`, newest first. Each change is a bullet starting with a bold lead-in:
   `- **What changed.** Why it matters.` The release workflow uses the section as the GitHub release
   notes, and the app's Changelog page shows it change by change. Tests fail if the running version
   has no section, or a section has no date.
3. **README:** update everything the release changes: feature sections, badges, SDK and version
   mentions. Search it for stale version numbers before committing. Screenshots sit in collapsed
   `<details>` blocks; keep new ones that way.
4. **Build and test:** `dotnet build DevDeck.Avalonia.sln -c Release` (no warnings allowed) and
   `dotnet test tests/DevDeck.Core.Tests -c Release` must pass.
5. **Publish:** commit, push `main`, then push an annotated tag `vx.y.z`. The Release workflow builds
   every platform and publishes the release. Watch it until it finishes.
6. **Deploy locally:** install the release's `DevDeck-win-x64.zip` into the local install folder
   named in `CLAUDE.local.md`. Back the folder up first, close DevDeck and wait for the process to
   exit, then replace only `DevDeck.exe` and `lib/`. `settings.json`, `secrets.json`, `history.json`
   and `clips.json` live beside the executable on Windows and must never be overwritten or deleted.
   Start it through Explorer and check that the window title shows the new version.

## Conventions

- **Text:** every user-visible string is a row in `gen.py` (English and Brazilian Portuguese).
  Run `python gen.py` to regenerate `Strings.cs` and `Tr.cs`. If Python is not available, add the
  same entries to both generated files by hand, in the same position and format as the generator
  would write them. Use the app's own words for its controls in both languages.
- **Comments:** match the existing style. Explain why, at the same density as the code around it.
- **Settings:** settings are not read back after startup. Every panel holds the object loaded once.
  A new setting needs a default that keeps older settings files working unchanged.
- **Line endings:** source files are CRLF. Git normalises on commit, but a mixed file is a noisy
  diff. `sed -i` in Git Bash can rewrite a file with LF, so check with a byte count rather than
  `grep`, which ignores the CR there.
- **Tests:** a test that reads translated text belongs in the `[Collection("Strings")]` collection,
  because the language tests switch the app's language while they run.

## Pitfalls already met

- **Avalonia 12:** `Watermark` is `PlaceholderText`, `SystemDecorations` is `WindowDecorations`. A
  style animation cannot animate `RenderTransform` - it throws at start-up - so animate `Margin` or
  `Opacity`. Fluent shrinks a pressed `Button` to 98% about its centre, which moves the edges of a
  full-width button out from under the pointer; set `RenderTransform` to `none` on `:pressed` for
  those. A disabled control shows its tooltip only with `ToolTip.ShowOnDisabled="True"`.
- **cmd.exe:** run a line as `cmd.exe /s /c "<line>"` (`ScriptFile.Cmd`). Without `/s`, a quoted
  script path followed by quoted arguments loses its first and last quote and splits at a space.
- **A closed window cannot be shown again.** At sign-out Windows closes the hidden main window too;
  anything that re-shows it must check it has not been closed.
- **Shutdown inside a tray menu callback freezes.** Post it to the dispatcher instead.

## Trying the app

- DevDeck is single-instance per user: starting a second copy hands its request to the running one
  and exits. Close the installed copy, or every copy you start, before testing another build.
- A build run from its own folder is portable and keeps its own settings there, so a copy of
  `src/DevDeck.App/bin/Release/net10.0` in a scratch folder is a safe test install.
- When the user is at the machine, do not drive the UI with the mouse or keyboard: focus cannot be
  taken reliably and clicks land in their windows. Use Windows UI Automation to press buttons and
  `PrintWindow` to capture the window, neither of which needs focus.
