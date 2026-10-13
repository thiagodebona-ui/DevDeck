# Changelog

## 1.0.1 - 2026-10-08 18:12

- **Quick run on the Commands page.** Type or paste anything, pick **Run with**, press **Run** -
  without making it a command first. It has the same editor, colouring, output and Stop as a
  command, remembers what you typed, and **Save as command** keeps it when a one-off turns out to
  be worth keeping.
- **JavaScript in Run with.** Commands and Quick run can be JavaScript, run by Node.js, with
  colouring, parameters in `process.argv`, AI conversion to and from the other languages, and
  `javascript` blocks from the Assistant run as JavaScript.
- **The Assistant writes real JavaScript.** It is told about the `javascript` fence when Node.js is
  installed, and never to wrap code in a script that saves it and runs it - asked for JavaScript,
  it used to hand back a PowerShell here-string piped into `node`.
- **A smaller, more useful starter deck.** A new install starts with nine JavaScript commands
  instead of two dozen, and two chains instead of seven: *Project overview*, *What changed vs
  main*, *Find TODOs*, *Who is using a port*, *Heavy build folders*, and a *Release notes* chain
  that turns everything since the last tag into notes. Each one works on every platform and shows
  off one feature. An existing deck is left as it is; **Add the starter commands** adds the new
  ones.
- **A new floating widget.** The dial now looks like an instrument: a tick-mark bezel, a glowing
  memory ring, CPU, GPU and disk rings in their own colours with a matching legend, used and total
  memory under the reading, and a rim that lights up under the pointer.
- **Size it, fade it, leave it where you put it.** Right-click the widget for Small, Medium, Large
  or Extra large and four opacities, or use Ctrl+wheel and Shift+wheel for anything between. It
  remembers its size, opacity and place, and snaps flush to a screen edge it is dropped near.
- **The widget shows more.** A graph of the last two minutes of memory behind the reading, a notch
  on the ring at the session's peak, and a halo that pulses red past 90%.
- **Stop where it runs.** A running block in the Assistant swaps its Run for Stop, and its output
  has a Stop under it while it runs, instead of only the button in the status bar.
- **A message's delete is visible.** The name, time and delete cross over each message sit on a
  surface of their own, so they no longer vanish into a background image.
- **Open a grouped process.** On Memory/CPU, the arrow on a grouped row ("node ×14") lists every
  process in it with its own memory and CPU, and **Spare** or **End** for each one.
- **A new Running page.** Every port and container has its actions on its own row - Open, Copy
  URL, Stop, Start, Restart, Logs - instead of select-then-find-the-button. Ports show as pills
  with what they are for, tiles carry icons, and an empty list says why it is empty.
- **A new Toolbox.** Search the tools, read them in their groups, and work in two labelled panels:
  Input with Paste and Clear, Output with Text or Tree, Use as input and Copy. Errors show at the top
  of the output.
- **A new Clipboard page.** Clips are cards, and the selected one is shown in full beside the list
  with Copy, Open, Pin and Delete. Recording is a banner with one clear button, the list filters to
  **Pinned** or **Links**, and **Clear all** asks before deleting pinned clips too.

## 1.0.0 - 2026-10-07 20:35

The first release of DevDeck.

- **Commands.** A deck of your everyday commands and scripts - PowerShell, cmd, bash and more - run
  with one click, with their output streaming live and a pill on every command that a chain or a
  watch uses.
- **Assistant.** Chat with a local model (Ollama, LM Studio) or any OpenAI-compatible service. It
  writes commands and whole chains for you, converts a command to another shell, and explains a
  failure from its output.
- **Automation.** Chains run several commands in a row, each step able to read the output of the
  ones before it; watches run a command when files change. A chain step opens its command on the
  Commands page in one click.
- **HTTP.** A lightweight API client with environments, saved requests in groups, curl import,
  parsed JSON with a tree view, and whole groups sent at once.
- **Running.** What is listening on which port, your containers, health checks, and the heaviest
  processes, each a click away from being ended.
- **Toolbox.** Offline text tools: JSON and XML formatting, Base64, URL, JWT, hashes, UUIDs, regex
  and more.
- **Clipboard.** An opt-in history of what you copied, searchable, with pinning.
- **Screenshots.** Every Win+Shift+S capture in one gallery - thumbnails, list or details,
  favourites, multi-select copy, and drag out to any app.
- **Memory/CPU.** Live gauges for memory, processor, disk and GPU, and the largest processes grouped
  by program ("node ×14"), with **Spare** to keep one out of every trim.
- **Memory cleanup.** **Quick**, **Recommended** and **Deep** presets, or ten steps to tick yourself:
  trimming working sets, combining identical pages, flushing the file and registry caches, purging
  the standby cache and more. Steps that need an administrator ask Windows once, a row of bars shows
  what your last cleans freed, and an optional automatic clean runs when memory passes a threshold.
- **Free up disk space.** Temporary files, the recycle bin, crash dumps and the NuGet, npm, Yarn and
  pip caches, each measured before anything is deleted.
- **Floating widget.** A small always-on-top memory gauge for the desktop; click it to clean.
- **Themes.** Twenty themes, light and dark, and seven living ones with an animated background:
  **Aurora**, **Matrix**, **Cosmos**, **Synthwave**, **Ember**, **Frost** and **Sakura**. Any
  background effect can be worn with any theme, or switched off.
- **English and Brazilian Portuguese,** switched instantly, without a restart.
- **Everyday comforts.** Keep-awake, a global hotkey, desktop notifications when a long command
  finishes, a command palette, start with Windows, and a portable install that keeps everything
  in its own folder.
