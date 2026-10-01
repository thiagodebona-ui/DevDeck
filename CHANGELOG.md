# Changelog

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
