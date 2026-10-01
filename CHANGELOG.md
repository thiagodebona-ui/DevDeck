# Changelog

## 1.2.0

- **A Changelog page.** A new section in the rail lists what every version brought, read from the
  changelog built into the app, so it works offline. The first time a new version starts, it opens
  on this page once. **Check for newer versions** adds what is waiting in any release you have not
  installed yet, and links to its download.
- **Start with the system.** Settings > Behaviour has a new switch: **Start DevDeck when I sign in**.
  It is for your user only, and opens the deck minimised so it does not get in front of anything.
  On Windows this is an entry under the registry's Run key, and on Linux an autostart file. Turning
  it off removes it, and the tick box always shows what the system really has.
- **Update checks show what changed.** When Settings finds a newer build, it lists the changelog of
  every version between yours and that one, not just the version number.

## 1.1.0

- **Examples of parameters and chains.** Nine small `Example:` commands - say hello with a
  parameter, ask before running, write to a log, count files, double a number, hand on several
  values, fail on purpose - and four `Example:` chains that pass values between them. They are
  added once to an existing install, and **Add the starter commands** brings back any you delete.
- **`param()` works in PowerShell commands.** DevDeck put a line of its own at the top of every
  PowerShell script, which PowerShell then refused: `param` has to come first, so a script that did
  what the Parameters hint says failed with "The term 'param' is not recognized". That line now goes
  after the script's `param()` block.
- **The cursor goes where you click in the body editor.** On a script with Windows line endings,
  every comment line was drawn with an extra blank line under it, so the code you could see was
  lower than where the editor really had it - and clicking it could only put the cursor at the end.
  Only the comments at the top could be clicked into.

## 1.0.1

- **The chain output really does grow with the chains section now.** 1.0.0 meant to do this but the
  log stayed at its smallest height: the form around it was stretched to the section's height, so
  the empty space under the log was counted as fields above it and there was never room left over.

## 1.0.0

The first stable release. The version starts again at 1.0.0: "3" counted internal rewrites, not
releases, and this is the first one meant for everyone. If you are on 3.0.0-alpha.1, download this
one by hand - that build reads 1.0.0 as older and will not offer it.

- **A new icon**, in the app's own steel and gold, and now embedded in the executable as well, so
  Explorer, shortcuts and a pinned taskbar entry show it instead of the generic one.
- **The chain output grows with the chains section.** Dragging the grip down makes the log taller,
  instead of only adding empty space under a log that stayed 320 pixels high.
- **Check for updates works.** It was asking a repository the releases are not published to, so it
  could never find one.
- **Faster first launch.** The app is precompiled, so a new copy shows its window in about 5s
  instead of 14s on a machine with a virus scanner, and warm starts are quicker too.
- **A second click during startup no longer opens a second deck.** DevDeck listens for a later
  copy from the moment it starts rather than once its window is up.

## 3.0.0-alpha.1

The V3 rebuild, from a shell with one working panel to a complete deck.

### Latest

- **A tidy install folder.** A published build is now just the `DevDeck` launcher and a `lib`
  folder holding the app and its .NET runtime, instead of about 220 files side by side. Settings,
  history and conversations still sit beside the launcher on Windows. It still needs no .NET
  installed.
- **The chains section is resizable.** Drag the grip under it to set its height, and the chain
  editor scrolls inside. The height is remembered.
- **A reset restarts the app.** Resetting every setting now closes DevDeck and opens it again on
  the fresh settings, instead of asking you to do it yourself.
- **A chain shows its own output.** Every step's output lands in one log on the Automation page,
  under a heading per step and its exit code, instead of taking you to the Commands page. Chains
  also run side by side now: each has its own Stop, and a chain that reaches a command another
  chain is running waits for it rather than restarting it.
- **Automation sections fold.** Chains, file watches and the project deck each collapse from their
  header, and stay how they were left.
- **The Running page watches more:** tiles for listening ports, containers, running deck commands
  and health; **health checks** (URLs asked on every refresh, with status code and
  response time); a **"is this port free?"** check that names the owner; what the deck has running
  with how long and a Stop; the heaviest processes with an End button; a filter over both lists; a
  PID column; Copy URL; and an auto-refresh switch. The containers half only appears when Docker is
  answering and has containers.
- **Memory/CPU gains Disk and GPU gauges** (disk active time and 3D engine load, as Task Manager
  reports them, plus a used-and-free bar for the system drive, read every five seconds). The widget gets both as thin inner rings. Every
  gauge now glides to a new reading instead of jumping, and each process row has an End button.
- **The request list shows each request's state:** a turning ring while it is sending, then its last
  status code in green, amber or red. The code is saved, so it is still there after a restart.
- **Ctrl+Enter sends** from anywhere on the HTTP page.
- **Commands take parameters.** A list of name/value rows under the body, each switchable, passed on
  every run as real arguments - `-name "value"` to PowerShell, `%1 %2` to batch, `$1 $2` to shell
  scripts - and as `DEVDECK_ARG_NAME` / `DEVDECK_ARG_1` in the environment.
- **Environment variables for every command**, under Settings beside Secrets. A fresh install and an
  upgrade both get `appName` and `appVersion`, a `secretExample` secret holding the app's name and
  version, and a "Show the example secret" command that prints all three.
- **A new command's body can be typed into.** The editor's input layer was as wide as its text, so an
  empty body was a target a caret wide.
- **Colourful icons**, fifty of them, including new ones for APIs, terminals, CPUs, health checks and
  clinical systems. The group icon menu works again.

### Commands

- **Parameters** — `{{branch}}` in a command line is asked for when the command runs, with the
  last value offered back. A command is now a template rather than a fixed string.
- **Chains** — one entry can be several steps, stopping at the first failure.
- **File-watch triggers** — a command runs when a path changes, debounced so a save that touches
  forty files is one run. The command is picked from a list rather than typed, the folder is
  chosen with a picker, and **what changed is passed to the command**: the watch name, the folder,
  the kind of change, the time, the first file, the count and the whole list arrive as
  `DEVDECK_*` environment variables. Firing raises a notification, which each rule can turn off.
- **Starter chains and a starter watch** alongside the starter commands, so a fresh install opens
  on something that can be run rather than on three empty panels. The watch ships switched off.
- **"What changed just now"**, an eleventh starter command that prints every variable a watch
  passes it. It is the worked example: turn on the watch, save a file, read the output, then write
  your own.
- **`.devdeck.json`** — a deck that lives in the repository and travels with it.
- **Run history** — what each command did and how long it took, which is also what orders the
  tray menu.
- **Notification on a long run** — anything past the threshold says so when it finishes, because
  the point of a background run is not watching it.
- **Import runnables** — scripts and npm targets already in the working directory, found and
  offered rather than typed out again.
- **Starter commands** on a fresh install, so the deck is not empty on first open.

### Output

- **ANSI colour** rendered rather than printed as escape codes.
- **`file.cs:42` is a link** that opens the file at the line.
- **Syntax highlighting** in the command editor.

### The palette

- **Ctrl+K** — three letters of a command name, Enter. The fastest route to anything.

### Assistant

- **Repository briefing** — the model is told what project it is in before it is asked anything.
- **Attachments** — files by button, by picker or by drag-and-drop, each showing its token cost
  before it is sent. Binaries are refused; anything oversized is truncated and says so.
- **Saved conversations** — one file each, reopened into a chat that can be continued rather than
  only read. "Clear chat" became "New chat" and saves first, which removes the hesitation that
  kept people in a conversation past the point the window could hold it.
- **Prompt library** — six starters seeded, and anything worth keeping saved alongside them. A
  prompt drops into the composer rather than sending, because a library that fires on selection
  is one misclick from a question nobody meant to pay for.
- **Approval before running generated code** — the model may propose; the user runs it.
- **"Explain this failure"** — a failed run goes to the assistant with its output attached.
- **Token and cost readout** — measured where the endpoint reports usage and marked with a tilde
  where it does not, no money at all for a model whose price is not published, zero for a local
  one, and the context window as a percentage that turns amber at 70%.

### Tools

- **HTTP client** with a **curl importer** that takes a command pasted straight out of a browser's
  devtools - method, URL, headers, cookies and body - the way Postman does. There is a button for
  it, but the address bar is where people try it first, so **Ctrl+V of a curl into the address bar
  becomes the whole request**: method, headers, cookies and body, over the top of the one being
  edited. Caught before the box sees it, because a devtools curl is a dozen lines joined by
  backslashes and a single-line box would have flattened it into something unparseable.
- **A response pane that renders what came back**, not what the header claimed: the content type
  chooses what to attempt and the body decides. JSON and XML are indented, HTML is left byte for
  byte because re-indenting it would change what is inside a `<pre>`, a binary is described rather
  than drawn, and the format is named beside the status code. An API that labels its JSON
  `text/plain` still gets it formatted; an HTML error page from a proxy under a JSON content type
  is shown as the page it is.
- **Chains pass values between their steps.** A chain was a list of commands that happened to run
  in order; it is now a pipeline. Each step is handed what the one before it printed -
  `DEVDECK_PREVIOUS_LINE` for the last non-empty line, which is the value a script meant to feed
  another one ends by echoing, and `DEVDECK_PREVIOUS` for all of it, plus the previous step's name
  and exit code. Environment variables rather than arguments, because a command here is a script
  body rather than an argv, and splicing a path into one breaks on the first space or ampersand.
  Each step sees only its immediate predecessor: a chain is `a | b | c`, and accumulating
  everything would hand the fourth step three outputs with no way to tell them apart. Capped at
  sixteen thousand characters, with the truncation announced in-band - Windows refuses to start a
  process whose whole environment block passes 32,767, and a silent loss of half a log is a bug
  report about missing data. Two latent bugs fell out of the same work: chain steps were being
  told `DEVDECK_TRIGGER=manual`, because the function that said otherwise existed and was never
  called, and the chain's name was arriving as `DEVDECK_WATCH_NAME`.
- **A worked example to run on a first launch**, seeded: the chain **"Pass values between steps"**
  and the three commands it is built from. Step one finds the newest source file and prints its
  path last and alone, step two measures it and prints a count, step three prints everything it
  was given - including the difference between the whole output and the last line, and the fact
  that it can see step two but not step one. Each one also says something sensible run on its own,
  because that is how anyone will first meet them.
- **A running command now moves.** The mark on a running row was a still six-point dot, which is
  indistinguishable from a decoration until you already know what it means; it is a slowly turning
  dashed ring, borrowed from the memory panel so that one motion means one thing app-wide. The
  left rail gained the same ring on any section with work going on in it - the gap that prompted
  this was running a chain from the Automation panel, where nothing anywhere said that commands
  were executing on a page you were not looking at. Running a chain now also brings that page up.
- **An icon on a request, and on a group** - chosen from a grid of fifty-eight, right-click a
  request for "Icon..." or a group header for "Group icon...". Stored as a name rather than as
  path data, so the shapes can be redrawn in a later build without touching anybody's saved
  requests, and a settings file edited by hand can say `"Icon": "database"` and mean it. A name
  that matches nothing draws no icon rather than a placeholder. A group's icon lives beside the
  requests rather than on them, because a group is not an object - it is the set of distinct
  group names assembled for drawing - so moving a request between groups cannot carry the wrong
  icon with it.
- **A group header you can find**, twice the weight it was: its own background, a rule under it,
  the name at the size of the rows beneath rather than smaller than them, and the count moved off
  the name so the two can be drawn at different weights. It used to be the quietest thing in a
  list it was supposed to be organising.
- **A response-headers pane built for hunting**, because that is what is done with thirty
  headers: a count at the top, a filter that matches names *and* values - half the time what is
  being looked for is a cookie's domain or a cache directive, and only the person typing knows
  which side of the colon they mean - names sorted rather than left in the order the server sent
  them, banded rows so a value that wraps onto three lines still reads as belonging to the name
  on its left, and right-click to copy one header whole rather than dragging a selection across
  the wrap.
- **Resizable panes in the HTTP panel**, and the split is remembered. Headers, the body you send
  and the reply are three stacked panes with a drag handle between each pair; ten header rows and
  a short body are where they start, which is where the fixed heights used to leave them for good.
  A request being read is mostly reply and a request being built is mostly headers, and the deck
  should not need rearranging every time the work changes shape. Two heights are stored rather
  than three - the reply takes the remainder, so a taller window gives its extra room to the
  thing you sent the request to see rather than to the form above it.
- **The response body is coloured**, the way the command editor is. A JSON body is mostly
  punctuation, and flat monospace leaves the eye to find the field names in it; they are now a
  colour apart from their values, with numbers, `true`/`false`/`null` and strings each their own.
  XML and HTML get the same treatment, with the words between the tags left plain because those
  are the part being read. The text stays selectable and copyable, and a body past about a
  megabyte is shown plain rather than laid out as a hundred thousand separate runs - colour is
  not worth a window that looks like it has hung.
- **Environments** — a named set of values each, so a request is written once against
  `{{host}}` and `{{token}}` rather than once per deployment, and switched with the picker at the
  top of the panel. A value marked secret goes to the machine's vault instead of to
  `settings.json`, which is what makes a deck safe to commit or to copy between machines. A name
  the chosen environment does not define is sent exactly as written and says so, rather than
  going out as an empty string and producing a 401 nobody can explain.
- **Groups and colour flags on saved requests.** Forty requests in one flat list read as forty
  similar lines; the same forty under collapsible headings, each flagged, read as staging and
  production. Which groups are collapsed is remembered, and both the group and the flag travel
  with the deck.
- **The request tree is resizable**, and its width is remembered, for the decks whose request
  names are paths rather than words.
- **Renaming a saved request** - double-click it, or right-click for a menu. "Name it after the
  URL" stayed, because the automatic name is right often enough to keep.
- **Clipboard history**, searchable, and sized for the thing it is most worth having: the ceiling
  is a megabyte rather than a preview's worth, and a command line is kept even though it carries a
  bearer token, because dropping a devtools curl for containing a credential would be refusing the
  payload because of the envelope. A token copied on its own is still refused. Hovering a row
  shows the head of the clip, since large ones all look alike for their first hundred characters.
- **Ports** — what is listening, and on what.
- **Containers** — what is running now.
- **Text tools** — hashes, encodings, JSON, UUIDs.
- **Dev toolbox** and a **log panel** with its own filter.

### The window

- **An icon for every section in the rail**, and for the buttons that start, stop, add, delete,
  copy, refresh and browse. Drawn as stroked paths in the app's own foreground colour rather than
  an icon font or emoji: a font that fails to load draws a column of empty boxes, and emoji are
  drawn by the system, so the same character is a flat glyph on one desktop and a colour cartoon
  on the next and neither follows the theme. These go dark with the theme and brass with the
  selection, like everything else.

### Languages

- **English and Brazilian Portuguese**, switched in Settings. The window already on screen
  changes with it - every label, every status line, the rail, the palette and the transcript
  already in the assistant - rather than the setting taking effect on the next start. That is the
  whole reason the strings are bound rather than read: a language setting that needs a restart is
  one people try once and then work around.
- **Following the desktop is the default.** Somebody in Brazil should not have to find this
  setting at all. What is stored is the choice rather than what it resolved to, so "follow the
  system" stays a live rule and an explicit choice stays explicit - a user who deliberately picked
  English is not moved off it the next time they log into a Portuguese machine.
- A Portuguese-speaking desktop of any region gets the Brazilian translation. It is not the right
  dialect for Lisbon, but it is far closer to readable than the alternative.
- The table is a plain C# file rather than `.resx` and satellite assemblies, because the app ships
  as one self-contained folder per platform and a missing satellite is an exception at load rather
  than a word in English. A key with no word behind it renders as the key, which is an obvious bug
  on screen rather than a blank label nobody notices.
- Diagnostic lines stay in English on purpose - see the README.

### Reaching the app

- **Tray icon** with the most-run commands on its menu, which also settles the old problem of a
  window that hides behind the floating widget and could not be brought back.
- **Global hotkey** on Windows, with an honest message and a pointer to `devdeck show` on macOS
  and Linux, where a running process cannot claim a system key.
- **CLI companion** — `devdeck` on the PATH.
- **Deep links** — `devdeck://` so a link can run a command.
- **Single instance** — a second launch surfaces the first.

### Settings and platform

- **Settings moved off `BaseDirectory`** to the per-user config directory, which was the thing
  blocking any macOS build, with a portable mode kept for the Windows case it was right for.
- **Secrets** through `DataProtection` - DPAPI on Windows, AES-GCM under a 0600 key file
  elsewhere, with the format chosen by reading the value rather than by asking the OS.
- **Update check** that checks and never installs, and never on startup.
- **Themes**, light and dark.

### Fixed

- **Keep-awake did nothing at all.** The setting was carried over from V2 and the tick box was
  drawn, but no code behind it ever came across - the flag was saved and nothing read it, so the
  machine slept and locked exactly as if the switch were off. It now holds the machine up on all
  three platforms: `SetThreadExecutionState` on Windows, `caffeinate` on macOS,
  `systemd-inhibit` on Linux, asserted from a thread that owns it, because that state dies with
  the thread that set it.

  The second half is new, and is what the report was actually about: **not sleeping and not
  locking are different settings.** A managed Windows machine locks on the inactivity policy,
  which reads how long since the OS last saw a keypress and never consults the power request -
  which is why the switch appeared to do nothing even when the power half was working. A separate
  "stay available" switch holds that clock back with one F15 a minute, sent only after 45 seconds
  of genuine quiet so it never competes for the keyboard. The same clock is the one chat apps read
  to mark you Away. The settings page now shows what keep-awake is actually doing rather than only
  what was asked for, so a request the system refuses is visible instead of silent.
- **The Running panel listed nothing at all.** Not a slow listing or a short one - empty, on a
  machine with forty-odd listeners on it. Every port is matched against `netstat` to find out what
  owns it, and not every port has an answer there: one held by another user's process, or started
  between the two calls, is simply not in that table. The code written to handle exactly that case
  was the thing that broke on it. A missed lookup leaves a tuple whose name is `null` rather than
  empty, and the "unknown" fallback read the length of it before checking, so the first
  unaccounted-for port threw and took the whole listing with it.

  What made it a bug worth two sessions rather than ten minutes is the blanket `catch` around the
  loop, which returned an empty list. An empty list is the honest answer to "what is listening" on
  a quiet machine, so the failure presented as a correct answer to a different question, with
  nothing in the log and nothing on screen to suggest otherwise. Both catches in that file now say
  what went wrong before they give up.
- **A container engine that is installed but not running looked like no containers.** Docker
  Desktop leaves its CLI on the PATH when the engine is stopped, so the check for one answered
  yes, and the panel showed an empty list under "Containers, through docker." with start and stop
  buttons that could not have worked. It now reads what the CLI complained about and says the
  engine is not running, and the buttons go with it.
- **The delete button on a header row could not be clicked.** Avalonia's scrollbar is drawn over
  the content rather than beside it, and it widens on hover - so approaching the `✕` at the end of
  a row grew the bar on top of it, and the button moved out from under the cursor as the cursor
  arrived. The list reserves that width now. This one was self-inflicted, and arrived with the
  scrolling header list it is fixing.
- **The whole right-click menu on a saved HTTP request was dead.** It opened, drew every item,
  and not one of them did anything - rename, duplicate, delete, all of them greyed. A `ContextMenu`
  is shown in a popup root of its own rather than inside the control it hangs off, so it inherits
  no `DataContext` from the row and the ancestor binding written inside it walked up into nothing:
  every `Command` bound to null, and a `MenuItem` with a null command draws itself disabled. The
  `⋯` button was never affected, because a `MenuFlyout` on a button in the panel binds straight to
  the panel's own view model - which is exactly why one worked and the other did not, and why the
  report read as "the three dots is working".

  The menu is now pointed at the view model as it opens, and the row under the pointer is selected
  at the same moment. That second half matters on its own: Avalonia does not select on a right
  click, and every item in the menu acts on "the selected request" - so without it the menu would
  open on one row and delete another.
- **The seeded example requests could not be deleted.** They were written into the settings file
  by a test fixture rather than by the app, under a key nothing read back, so they were drawn from
  one place and deleted from another and came back on the next start. They are gone, and the
  fixtures no longer write to a real settings file.
- **The clipboard capture switch existed twice, and the copy on the settings page was the broken
  one.** "Record what I copy, while the Clipboard page is open" sat under Behaviour, and the
  Clipboard panel had its own "Record what I copy" in its header. Both wrote the same stored flag,
  but only the panel's switch owns the poll timer that does the recording - the settings copy
  wrote the flag and stopped there. Ticking it therefore recorded nothing, left the panel's own
  box unticked, and unticking it while a capture was running stopped nothing: it changed the
  stored value out from under the panel that acts on it, and the two boxes disagreed on screen
  because they are separate view models with no notification between them.

  The settings copy is gone. The switch lives on the Clipboard page, next to the list it fills and
  the note explaining that it only runs while that page is open - which was the one thing the
  settings label said that the panel did not, and the panel says it better.

- **A command with a lot to say froze the window until it had said all of it.** Running the
  TODO search over a large repository locked the app for as long as the output took to arrive.
  The batching that collects output was already right; the view undid it. Output arrives as one
  row added per line, so a few thousand matches raised a few thousand collection-changed events,
  and every one of them posted its own scroll-to-the-bottom onto the UI thread. The queue was
  full of identical work before the first line was drawn. A scroll is now queued once per batch
  and reads the tail when it runs rather than when it was queued, so it always lands on the last
  line that actually arrived.

- **Refresh on the assistant's model list moved you to a different model.** Picking a model and
  pressing refresh left you on whatever happened to be first, which on a machine with a dozen
  models pulled is an expensive surprise rather than a cosmetic one. Nothing in the refresh logic
  was wrong: the list is the ComboBox's source and the selection is bound two-way, so emptying it
  before refilling it wrote an empty selection back into the view model, and the guard that would
  have restored the choice was comparing against that empty value. The choice is read before the
  list is touched, and both call sites now go through one helper rather than two copies of it.

- **A prerelease version number parsed as 0.0.0**, which would have made every published release
  look newer than the running build and left the update check permanently shouting at anyone on
  an alpha.

### Known gaps

Starting with the system is not built on any platform, global hotkeys are Windows-only, holding
the lock off is Windows-only (not sleeping works everywhere), the memory tooling is Windows-only
and staying that way, the translation covers what the app says rather than what it logs, and only
the Windows build has been run. See the README.
