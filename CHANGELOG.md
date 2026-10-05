# Changelog

## 1.3.0 - 2026-10-05 17:53

- **A Screenshots page.** Every Win+Shift+S capture in one place, newest first - read from the
  folder Snipping Tool saves to, so captures taken while DevDeck was closed are there too, and a
  new one appears as you take it.
- **Thumbnails, List or Details.** Switch how the gallery is drawn; Details scrolls sideways rather
  than squeezing a column. A divider between the gallery and the preview drags to the width you
  like, and both are remembered.
- **Favourites.** Star a screenshot from its tile or the toolbar, and narrow the gallery to
  **Favourites only**. A favourite stays where it was taken rather than jumping to the top.
- **Select several screenshots** with Ctrl or Shift: **Copy path** copies every path, **Copy image**
  copies them as files to paste into Teams or a folder, **Favourite** and **Delete** act on all of
  them, and dragging one out takes the lot. Delete sends files to the Recycle Bin.
- **Keep images you copy** (off until you turn it on) also collects captures that never reach a
  file, without keeping the same capture twice.
- **Run a whole HTTP group.** The ▷ on a group header sends every request in it at once, and turns
  into a ■ that cancels the ones still out. A line under the list says how the run went.
- **Icons on every button,** across the whole app.
- **Narrow windows no longer break the layout.** The HTTP response bar, the Running page, the
  Commands run controls and the Memory/CPU gauges rearrange themselves instead of cutting buttons
  off or breaking words one per line.
- **Memory/CPU in Portuguese.** The cleanup steps, what they did, and the gauge captions were still
  in English.
- **A new page lands beside its neighbour** in a menu you have rearranged, instead of at the bottom.

## 1.2.9 - 2026-10-02 18:10

- **Large responses no longer freeze the window.** A JSON reply of a few megabytes used to stop
  the app for seconds every time it was shown - even when adding a request. Only the lines on
  screen are drawn now, and the colouring is worked out in the background.
- **Parsed JSON.** A JSON reply in HTTP opens as a tree you walk with the arrow keys - Right opens
  a node, Left closes it - with **Source** beside it for the text. Huge documents stay quick.
- **Responses are drawn, not dumped.** **Preview** shows images, **SVG**, the pages of a **PDF**,
  the text of an **HTML** page, **Markdown** and **CSV** as a table; **Source** shows what arrived,
  or a hex dump for a file.
- **Open a response** in the app your system uses for it - a browser, a PDF viewer, a player.
- **Save picks the right name.** The server's suggested file name, or the extension from the
  content type, so an `image/png` saves as `.png`. Generic types are read from the file's first
  bytes, and a response too large to show is still saved whole.
- **Send requests in parallel.** A ▶ beside every saved request sends it at once, without waiting
  for others, and each request keeps its own reply.
- **Drag requests** to reorder them, onto a group to file them, or out of their group. **Alt+Up** /
  **Alt+Down** move the selected one.
- **Renaming a request shows straight away,** inside a group too.
- **Clear chat** empties the Assistant conversation without saving it; **New chat** still keeps it
  in History.
- **Delete a message** in the Assistant with the × beside it. The model does not see it again.
- **Why Run is greyed out.** In the Assistant, Run and Run chain now say when something started from
  the chat is still running.
- **No more freezes on chatty commands.** A command printing without pause, on the Commands page,
  in a chain or in the Assistant, no longer locks the window.
- **See how long it has been running.** The status bar counts up while a command runs - *Running
  for 3m 07s*.
- **What DevDeck is using.** A line above the version in the menu shows the memory, CPU and disk of
  DevDeck and every command it started.
- **Toolbox icons**, coloured JSON and XML results, and a **Tree** view for the JSON tools and JWT
  decode.
- **Each Toolbox tool keeps its own input.** Switching tools no longer carries text into one that
  cannot use it.

## 1.2.8 - 2026-10-01 19:13

- **A splash screen.** DevDeck opens on its mark - a terminal window with the deck and its chain
  under it, drawn in your theme's colours - while it gets ready, instead of nothing at all.
- **Runs on .NET 10** and C# 14. The download still carries its own runtime, so there is nothing to
  install.
- **A Changelog page** in the menu lists what every version brought, with the date and time it was
  released, one change per line. It opens once the first time a new version starts, and **Check
  for newer versions** adds anything you have not installed yet.
- **Rearrange the left menu** by dragging a section, or with **Alt+Up** / **Alt+Down**. The order is
  saved.
- **Commands show what they belong to.** Under each command, a pill names every chain and watch that
  runs it, with its place in the chain - "Morning check · 2/3". A chain's pills are all one colour,
  so the commands that work together stand out in the list.
- **Commands in use are protected.** A command a chain or a watch runs is greyed out and cannot be
  selected or deleted from under the thing that runs it. Hover it to see what uses it.
- **Arrange the deck.** Move a command with **Alt+Up** / **Alt+Down**, the arrow buttons under **New
  command**, or by dragging it. The arrow keys move through the list and **Delete** deletes.
- **When it was made and last ran.** Each command shows its kind, when it was created and when it
  last ran.
- **Parameter rows fill in placeholders.** A row under **Parameters** named like a `{{placeholder}}`
  fills it in, so the "Values for this run" box only asks for what is still missing.
- **Change the language, and the AI rewrites the script.** With a model set up, changing **Run
  with** - PowerShell to bash, say - has the AI convert the body, keeping `{{placeholders}}` and
  secrets as they are. A strip shows it working and **Undo** puts the original back.
- **Each run's output starts clean,** with no line left behind from the run before, and without the
  `powershell.exe -NoProfile ...` line every run used to start with.
- **Select output across lines.** **Select text** on the command output, the chain output and the
  Log page shows the output as plain text you can select and copy.
- **Batch and shell commands with a space in their name run.** A command such as "Example: ask
  before running" switched to batch failed with "...\Example_ is not recognized".
- **Steps can be switched off.** Untick a step in a chain to skip it without taking it out; the log
  marks it ○ switched off.
- **Clearer chain output.** Each step's heading shows how it went - ▶ running, ✓ passed, ✗ failed
  with its exit code, ■ stopped - instead of separate "exited with code" lines.
- **A step can read every earlier step's output,** from the folder in `DEVDECK_CHAIN_OUTPUTS`, as
  `step1.txt`, `step2.txt` and so on - not only the step just before it.
- **Examples of parameters and chains.** Small `Example:` commands and chains show parameters,
  asking before a run, logging, and passing values from one step to the next.
- **`param()` works in PowerShell commands,** and the cursor goes where you click in the body
  editor.
- **The Assistant builds chains.** Ask for several commands that work together and the answer comes
  with **Create chain**, which adds them to the deck and the chain to Automation, and **Run chain**,
  which runs it in the conversation. Your own commands are never overwritten.
- **Ask AI why** under any output in the chat sends the model what ran and what it printed, and asks
  why it behaved that way. **Delete** takes the output out of the conversation.
- **Help in the Assistant** lists what it can do, with an example prompt for each and tips for
  getting good answers.
- **AI settings are on the Settings page.** The provider, endpoint, model and key are under
  **Settings > AI**, saved as you change them; the Assistant uses what is chosen there.
- **Four more themes:** Darcula, One Light, Nord Light and Gruvbox Light.
- **Tips on the Behaviour settings,** and **Start DevDeck when I sign in**, which opens it minimised.
- **No more crash when Windows shuts down** with the memory widget open, and **quitting from the
  tray no longer freezes**.
- **The section arrows on the Automation page work** when you click the arrow itself, and the chain
  output grows with its section.

## 1.0.0 - 2026-09-28 17:36

The first stable release.

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
