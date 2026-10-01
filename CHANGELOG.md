# Changelog

## 1.2.5

- **Commands in use are out of reach in the list.** A command that a chain or a watch runs is
  greyed out and cannot be selected, so it cannot be deleted from under the thing that runs it.
  Hover it to see which chain or watch uses it. It still runs from its chain, its watch, a link or
  the palette.
- **Reorder the deck.** Move the selected command with **Alt+Up** and **Alt+Down**, with the new
  arrow buttons under **New command**, or by dragging it. The order is saved.
- **Keyboard in the list.** The arrow keys move through the commands, skipping the ones in use, and
  **Delete** deletes the selected one. After a delete the next command is selected, so pressing
  Delete again works down the list.
- **When it was made and last ran.** Each command shows, under its name, its kind, when it was
  created and when it last ran - "PowerShell · created 20 Sep · ran 08:15". Commands created before
  this version show only when they last ran.

## 1.2.4

- **Help in the Assistant.** A **Help** button at the top of the Assistant lists what it can do -
  write a command, build a chain, explain a failure, read your files, change what it wrote - with
  an example prompt for each. Click an example to put it in the message box, ready to send or
  change first. Under it, tips for getting good answers.

## 1.2.3

- **The Assistant builds chains.** Ask for several commands that work together - "make three
  commands that check the repository and a chain that reports on them" - and the answer comes
  with one block per command and a chain block listing the steps. **Create chain** adds the
  commands to the deck and the chain to Automation, then opens it there. A command already in the
  deck with the same script is reused rather than added twice; one with a different script is
  added beside yours under a new name, so your own command is never overwritten.
- **A chain step can read every earlier step's output.** Each step now gets
  `DEVDECK_CHAIN_OUTPUTS`, a folder holding what every earlier step printed, as `step1.txt`,
  `step2.txt` and so on. Before, a step only saw the step just before it, so a final report could
  not see the first two checks. The folder is removed when the chain ends.

## 1.2.2

- **A command that a chain or a watch runs cannot be deleted.** The delete button is off for it,
  and hovering it names the chains and watches that use it. Take the command out of those first.
  Before, deleting it left a broken step or a watch that fired at nothing.
- **Select output across lines.** **Select text**, on the command output, the chain output and the
  Log page, shows the output as plain text you can drag across, select all and copy. Turn it off to
  go back to the live, coloured output.
- **AI settings are on the Settings page.** The provider, endpoint, model and key moved to a new
  **AI** section in Settings. The Assistant shows what it is using, with a button that opens those
  settings. Changes are saved as you make them, not only after the next question. **Refresh** now
  lines up with the model drop-down.
- **Four more themes:** Darcula, after the JetBrains and Visual Studio scheme, and three light
  ones: One Light, Nord Light and Gruvbox Light.
- **Tips on the Behaviour settings.** Hover any switch under Settings > Behaviour to see what it
  does.
- **Quit from the tray no longer freezes.** It closed the tray icon while its own menu was still
  open, which could hang the app with its window on screen.
- **No build warnings.** The views use the current Avalonia names for placeholders and window
  decorations.

## 1.2.1

- **Runs on .NET 10.** DevDeck moves from .NET 8 to .NET 10, the current long-term support release,
  and its code to C# 14. The download still carries its own runtime, so there is nothing to install.
- **The README's release steps are current.** They showed an old version number as the example, and
  did not mention the changelog entry each release now needs.

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
