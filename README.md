<div align="center">

<img src="docs/images/splash.png" alt="DevDeck" width="320" />

# DevDeck

### Your everyday developer commands, one click away.

Keep the commands and scripts you run every day on a deck of buttons. Click one, watch its output
live. Chain them, run them when files change, and let an AI assistant write them for you - with
your own local model if you like.

[![Release](https://img.shields.io/github/v/release/thiagodebona-ui/DevDeck?label=Release&color=D4A017)](https://github.com/thiagodebona-ui/DevDeck/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-2EA44F)](#-download)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT%20%2B%20Commons%20Clause-blue)](LICENSE)

### [⬇ Download for Windows](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-win-x64.zip)

Free · No installer · No .NET needed · Everything stays on your machine

</div>

![The Commands page](docs/images/commands.png)

---

## ⬇ Download

| Platform | Download | |
|---|---|---|
| **Windows** 10 / 11, 64-bit | [**DevDeck-win-x64.zip**](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-win-x64.zip) | Recommended |
| macOS, Apple Silicon | [DevDeck-osx-arm64.tar.gz](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-osx-arm64.tar.gz) | Preview |
| macOS, Intel | [DevDeck-osx-x64.tar.gz](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-osx-x64.tar.gz) | Preview |
| Linux, x64 | [DevDeck-linux-x64.tar.gz](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-linux-x64.tar.gz) | Preview |
| Linux, ARM64 | [DevDeck-linux-arm64.tar.gz](https://github.com/thiagodebona-ui/DevDeck/releases/latest/download/DevDeck-linux-arm64.tar.gz) | Preview |

## 🚀 Get started on Windows

1. **Unzip** `DevDeck-win-x64.zip` anywhere - your Desktop, `C:\Tools`, a USB stick.
2. **Run** `DevDeck.exe` from the `DevDeck` folder. Keep the `lib` folder next to it.
3. If Windows says *"Windows protected your PC"*, click **More info → Run anyway** (the app is not
   code-signed yet).

The deck arrives filled with starter commands, chains and examples, so there is something to click
straight away. Pick your project folder at the top of the Commands page and commands run there.

<details>
<summary><b>macOS and Linux (preview)</b></summary>

<br />

These builds come from the same code but have not been tried on real machines yet. A few features -
the global hotkey, batch scripts, memory cleanup - are Windows only.

**macOS**

```sh
tar -xzf DevDeck-osx-arm64.tar.gz          # or DevDeck-osx-x64.tar.gz on an Intel Mac
xattr -dr com.apple.quarantine DevDeck     # the app is not notarised, so macOS blocks it otherwise
./DevDeck/DevDeck
```

**Linux** - needs a desktop session (X11 or XWayland) with `libfontconfig`, `libICU` and `libX11`.

```sh
tar -xzf DevDeck-linux-x64.tar.gz          # or DevDeck-linux-arm64.tar.gz
./DevDeck/DevDeck
```

</details>

---

## ✨ What's inside

| | Page | What it does |
|---|---|---|
| ▶️ | **Commands** | Save commands and scripts, run them with one click, watch the output live. |
| 🤖 | **Assistant** | Chat with a local model (Ollama, LM Studio) or any OpenAI-compatible service. It writes commands and chains for you. |
| 🔗 | **Automation** | Run commands one after another, or automatically when files change. |
| 🌐 | **HTTP** | A light API client: environments, saved requests, curl import. |
| 📡 | **Running** | Ports in use, containers, health checks, heavy processes. |
| 🧰 | **Toolbox** | Offline JSON, XML, Base64, JWT, hashes, UUIDs, regex and more. |
| 📋 | **Clipboard** | A searchable history of what you copied (opt-in). |
| 🖼️ | **Screenshots** | Every Win+Shift+S capture in one gallery. |
| 📊 | **Memory/CPU** | Live gauges, processes grouped by program, memory and disk cleanup. |
| 🎨 | **Settings** | 20 themes - 7 of them animated - English or Portuguese, AI, hotkey and more. |

Click a section below for the details and a screenshot.

---

<details>
<summary><h3>▶️ Commands</h3></summary>

![Commands](docs/images/commands.png)

Each command is a saved script with a name. Select it, press **Run**, and its output streams in
below, with how long it ran and its exit code at the end.

- **Any kind of script:** PowerShell, Batch, Bash (Git Bash or WSL) or a single shell line, with
  syntax colouring. Change **Run with** and the AI can rewrite the script for the new language.
- **Parameters:** add name/value rows passed on every run, or write `{{branch}}` and DevDeck asks
  for a value each time (`{{branch:main}}` gives a default).
- **Secrets:** `{{secret:API_TOKEN}}` is filled in from an encrypted vault at run time and never
  shows in the command.
- **Readable output:** colours are kept, and `Program.cs:42` becomes a link that opens your editor
  at that line. **Select text** lets you copy any part of it.
- **Stop, or don't wait:** stop a run any time, or tick *Run and don't wait* for dev servers.
  **Explain this failure** sends a failed run to the Assistant.
- **Organised:** pills show which chains use a command, drag or **Alt+Up/Down** reorders the list,
  and **Ctrl+K** runs a command by typing a few letters of its name.
- **Shareable:** copy a `devdeck://` link to a command, or put a `.devdeck.json` in a repository so
  a team shares one deck.

</details>

<details>
<summary><h3>🔗 Automation - chains and file watches</h3></summary>

![Automation](docs/images/automation.png)

**Chains** run several commands one after another - *Morning check* runs *Where am I*, then
*Today's commits*, then *What changed vs main*.

- Build one by picking commands, or ask the Assistant to write it.
- Untick a step to skip it, and choose whether the chain stops at the first failure.
- One log shows every step: ✓ passed, ✗ failed, ■ stopped, ○ skipped.
- Each step can read what came before it through `DEVDECK_PREVIOUS`, `DEVDECK_PREVIOUS_LINE`,
  `DEVDECK_PREVIOUS_EXIT` and `DEVDECK_CHAIN_OUTPUTS` (a folder with every earlier step's output).

**Watches** run a command when files in a folder change - say, the tests whenever a `.cs` file is
saved. They wait for writing to stop, ignore `bin`, `obj`, `.git` and `node_modules`, and arrive
switched off.

</details>

<details>
<summary><h3>🤖 Assistant</h3></summary>

![Assistant](docs/images/assistant.png)

- **Your model:** a free, private local model through Ollama or LM Studio, or any OpenAI-compatible
  service with your own key. **Set up Ollama** helps you get started.
- **Writes commands:** code it writes has **Run** (output comes back into the chat) and **Add as
  command**. It always asks before running anything.
- **Builds chains:** ask for commands that work together and **Create chain** adds them to your deck.
- **Explains:** **Ask AI why** under any output asks the model why it behaved that way.
- Attach files, keep favourite **Prompts**, and reopen past chats from **History**. The status bar
  shows tokens, cost and how full the context is.

</details>

<details>
<summary><h3>🌐 HTTP</h3></summary>

![HTTP](docs/images/http.png)

- Send any request; **paste a curl command** and the method, headers and body are filled in.
- JSON and XML come back formatted and coloured, as a tree you can walk with the arrow keys.
  Images, SVG, PDF, HTML, Markdown and CSV get a preview.
- **Environments** (`{{host}}`, `{{token}}`) for Local, Dev and Prod, with secret values encrypted.
- **Saved requests** in groups and colours. ▶ sends one, ▷ sends a whole group at once.

</details>

<details>
<summary><h3>📡 Running</h3></summary>

![Running](docs/images/running.png)

- **Listening ports** and the program behind each - copy its URL, open it, or stop what holds it.
- **Containers** from Docker or Podman.
- **Health checks** with status code and response time, and **Is a port free?**
- What the deck is running now, and the **heaviest processes**.

</details>

<details>
<summary><h3>🧰 Toolbox</h3></summary>

![Toolbox](docs/images/toolbox.png)

All offline - nothing you paste leaves your machine.

| Data | Encoding | Inspect | Generate & text |
|---|---|---|---|
| JSON format / minify | Base64 | JWT decode | UUID |
| XML format | URL | Hash | Case conversion |
| | HTML escape | Timestamp | Sort lines, regex tester |

**Use as input** chains one tool into the next.

</details>

<details>
<summary><h3>📋 Clipboard</h3></summary>

![Clipboard](docs/images/clipboard.png)

A history of what you copied. **Off until you turn it on**, it skips anything that looks like a
token or password, and lets you search, pin and copy entries back.

</details>

<details>
<summary><h3>🖼️ Screenshots</h3></summary>

- Every **Win+Shift+S** capture appears by itself, including ones taken while DevDeck was closed.
- Thumbnails, list or details; search, **favourites**, and multi-select **copy path** or **copy
  image**.
- **Drag tiles out** into Teams, Jira, a browser upload or a folder.
- Optionally keeps images you copy that never became a file.

</details>

<details>
<summary><h3>📊 Memory/CPU</h3></summary>

![Memory and CPU](docs/images/memory.png)

- Live gauges for **memory**, **processor**, **disk** and **GPU**.
- **Largest processes**, grouped by program ("node ×14") or one by one. **End** one, or **Spare** it
  so it is never trimmed.
- **Clean memory** with a preset - **Quick** (no administrator prompt), **Recommended** or **Deep** -
  or tick the steps yourself. A row of bars shows what your last cleans freed.
- **Free up disk space:** temp files, recycle bin, crash dumps and the NuGet, npm, Yarn and pip
  caches - each measured before anything is deleted.
- **Automatic clean** when memory passes a threshold, and a small **floating widget** for the
  desktop.

</details>

<details>
<summary><h3>🎨 Settings and themes</h3></summary>

![Settings](docs/images/settings.png)

- **20 themes.** Classic dark and light ones (Steel, Midnight, Nord, Gruvbox, Darcula, Solarized,
  Paper, One Light, High Contrast), and seven **living themes** with an animated background:

  | Theme | Background |
  |---|---|
  | Aurora | Drifting northern lights |
  | Matrix | Falling green glyphs |
  | Cosmos | A starfield with shooting stars |
  | Synthwave | A neon grid rolling towards a striped sun |
  | Ember | Rising sparks |
  | Frost | Snowfall |
  | Sakura | Tumbling cherry petals (light) |

  **Background effect** puts any of them on any theme, or turns them off.
- **English or Brazilian Portuguese**, switched instantly.
- **AI** provider, model and key; **secrets**; **environment variables** for every command.
- **Keep the machine awake**, **start with Windows**, a **notification** when a long command
  finishes, and a **global hotkey**.

</details>

<details>
<summary><h3>📜 Log and Changelog</h3></summary>

![Changelog](docs/images/changelog.png)

The **Log** shows what DevDeck itself did this session - handy for bug reports. The **Changelog**
shows what each version brought, and **Check for newer versions** finds updates. The same notes are
in [CHANGELOG.md](CHANGELOG.md).

</details>

<details>
<summary><h3>⚡ Reaching DevDeck from anywhere</h3></summary>

<br />

- **Tray icon** with your most-used commands.
- **Global hotkey** to bring the window up from any app.
- **`devdeck run "Tool versions"`** from any terminal (Settings adds the `devdeck` command).
- **`devdeck://run/<command>`**, `devdeck://chain/<chain>` and `devdeck://show/<page>` links from a
  browser, wiki or chat.

</details>

---

## 💾 Your data

DevDeck is **portable**: everything is kept next to `DevDeck.exe`, so copying the folder takes your
whole deck with it. To **update**, replace `DevDeck.exe` and the `lib` folder and keep the rest.

<details>
<summary>Which file holds what</summary>

<br />

| File | What is in it |
|---|---|
| `settings.json` | Commands, chains, watches, requests, environments and preferences. |
| `secrets.json` | Secret values, **encrypted** with your Windows account - readable only by you, on this machine. |
| `history.json` | Past runs of each command. |
| `clips.json` | Clipboard history, if turned on. |
| `shots.json`, `shots/` | Favourite screenshots and kept clipboard images. |
| `conversations/` | Saved Assistant chats. |

To start over use **Settings → Reset everything**. If the folder is read-only DevDeck uses
`%APPDATA%\DevDeck`; on macOS `~/Library/Application Support/DevDeck`, on Linux `~/.config/devdeck`.

</details>

<details>
<summary><b>🛠️ Building from source</b></summary>

<br />

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), and Python 3 if you change any
text the app shows.

```sh
dotnet build DevDeck.sln -c Release
dotnet run --project src/DevDeck.App
dotnet test tests/DevDeck.Core.Tests
dotnet publish src/DevDeck.App -c Release -r win-x64 --self-contained -o publish/win-x64
```

```
src/DevDeck.Core/          everything that is not UI
src/DevDeck.App/           the Avalonia app: windows, pages, view models, themes, icons
tests/DevDeck.Core.Tests/  the tests (run directly; not in the solution)
gen.py                     every text in English and Portuguese - run it after editing text
CHANGELOG.md               what each version brought
```

**Releasing:** set the version in `Directory.Build.props`, add a `## <version> - <yyyy-MM-dd HH:mm>`
section to `CHANGELOG.md`, commit, and push a tag:

```sh
git tag -a v<version> -m "DevDeck <version>"
git push origin v<version>
```

GitHub Actions builds every platform and publishes the release.

Built with [Avalonia](https://avaloniaui.net/), [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet),
[Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) and [PDFtoImage](https://github.com/sungaila/PDFtoImage).

</details>

---

## 📄 License

**Free to use, change and share** under the [MIT license with the Commons Clause](LICENSE):
use it at home or at work, fork it, share your version - but don't **sell** DevDeck or a product
built mainly on it. That makes it *source-available* rather than open source in the strict sense.
