# DevDeck

A cross-platform developer command deck: .NET 10, C# 14, Avalonia 12, CommunityToolkit.Mvvm.

- `src/DevDeck.Core` - everything that is not UI. No UI package may be referenced here.
- `src/DevDeck.App` - the Avalonia app: views, view models, icons.
- `tests/DevDeck.Core.Tests` - xUnit. It is not in the solution; run it directly:
  `dotnet test tests/DevDeck.Core.Tests`.

## Releasing a version

A release is not done until every step below is. Do all of them, without being asked.

1. **Version:** bump `<Version>` in `Directory.Build.props`, the only place it is written. Bump the
   **patch** number (1.2.0 -> 1.2.1) unless told otherwise. "Minor change" means the patch number.
2. **Changelog:** add a `## x.y.z` section at the top of `CHANGELOG.md`, newest first. The release
   workflow uses it as the GitHub release notes, and the app's Changelog page shows it. A test
   fails if the running version has no section.
3. **README:** update everything the release changes: badges, SDK and version mentions, feature
   sections. Search it for stale version numbers before committing.
4. **Build and test:** `dotnet build DevDeck.Avalonia.sln -c Release` and
   `dotnet test tests/DevDeck.Core.Tests -c Release` must pass.
5. **Publish:** commit, push `main`, then push the tag `vx.y.z`. The Release workflow builds every
   platform and publishes the release. Watch it until it finishes.
6. **Deploy locally:** install the release's `DevDeck-win-x64.zip` into the local install folder
   named in `CLAUDE.local.md`. Replace only `DevDeck.exe` and `lib/`. `settings.json`,
   `secrets.json` and `history.json` live beside the executable on Windows and must never be
   overwritten or deleted. Start it and check that the window title shows the new version.

## Conventions

- **Text:** every user-visible string is a row in `gen.py` (English and Brazilian Portuguese).
  Run `python gen.py` to regenerate `Strings.cs` and `Tr.cs`. If Python is not available, add
  the same entries to both generated files by hand, matching the generator's output exactly.
- **Comments:** match the existing style. Explain why, at the same density as the code around it.
- **Settings:** settings are not read back after startup. Every panel holds the object loaded once.
