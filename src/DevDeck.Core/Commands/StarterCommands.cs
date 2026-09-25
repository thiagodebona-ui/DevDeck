namespace DevDeck.Core
{
    /// <summary>
    ///  The ten commands a brand new install starts with.
    /// </summary>
    /// <remarks>
    ///  A first run used to open on an empty deck and the words "No commands yet", which tells you
    ///  nothing about what a command is for. These are the answer.
    ///
    ///  The bar each one had to clear: it answers a question a developer actually asks, it is worth
    ///  a button rather than being quicker to type, and it means something when run against a
    ///  project folder. That rules out `git status` on its own - anyone who wants it has already
    ///  typed it - in favour of the things nobody remembers the flags for: the triple-dot diff
    ///  against the base branch, which build folders are eating the disk, what is holding port 3000.
    ///
    ///  Nothing here writes, deletes, installs or pushes. A command that shipped with the app and
    ///  changed a repository the first time it was clicked would be indefensible, so the two that
    ///  people actually want for cleanup - build folders and outdated packages - report rather than
    ///  act, and leave the decision where it belongs.
    ///
    ///  Most are per-platform scripts rather than one-liners. A Shell command runs through
    ///  `cmd /c` on Windows and `/bin/sh -c` elsewhere, so anything with a percent sign, a pipeline
    ///  or a conditional in it behaves differently on the two - and a starter command that works on
    ///  one machine and not the next is worse than none.
    /// </remarks>
    internal static class StarterCommands
    {
        /// <summary>The starter deck for this machine, in the order it should appear.</summary>
        public static List<CustomCommand> For()
        {
            bool windows = OperatingSystem.IsWindows();

            return
            [
                // 1. The orientation question, and the only one simple enough to be portable as
                //    typed: branch, upstream, what is dirty, and the commit you are sitting on.
                Make("Where am I",
                    "git status -sb && git log -1 --oneline --decorate",
                    CommandKind.Shell),

                // 2. Nobody remembers that two dots is commits and three dots is the diff, which is
                //    exactly why this is worth a button. Finds the base branch rather than assuming.
                Script("What changed vs main", windows ? ChangedVsMainPs : ChangedVsMainSh, windows),

                // 3. "What did I do today" - for standups, and for picking up where you left off.
                Script("Today's commits", windows ? TodayPs : TodaySh, windows),

                // 4. git grep rather than a plain search: it skips .git, build output and anything
                //    ignored, which is the difference between twenty hits and twenty thousand.
                Script("TODOs and FIXMEs", windows ? TodosPs : TodosSh, windows),

                // 5. Where the disk went, one level down. The usual answer to "why is this checkout
                //    four gigabytes".
                Script("What is taking up space", windows ? FolderSizesPs : FolderSizesSh, windows),

                // 6. The same question aimed at the folders that are safe to delete and expensive
                //    to keep. Reports only - deleting them is the user's call, not the app's.
                Script("Heavy build folders", windows ? BuildFoldersPs : BuildFoldersSh, windows),

                // 7. Whichever ecosystem this folder actually is, rather than guessing one.
                Script("Outdated packages", windows ? OutdatedPs : OutdatedSh, windows),

                // 8. "Port already in use" is a daily occurrence and the answer is always buried in
                //    a different tool on every platform.
                Script("What is on my dev ports", windows ? PortsPs : PortsSh, windows),

                // 9. The "works on my machine" check. Each tool is asked separately so a missing one
                //    reports as missing instead of killing the whole run.
                Script("Tool versions", windows ? VersionsPs : VersionsSh, windows),

                // 10. Detached, because it opens something rather than finishing something -
                //     watching it would hold the pipes of an editor nobody is going to close.
                Make("Open in VS Code", "code .", CommandKind.Shell, detached: true),

                // 11. The worked example for file watches. Runs on its own too, where it reports
                //     that nothing triggered it - which is itself the answer to "what would a
                //     watch pass me".
                Script("What changed just now", windows ? ChangedNowPs : ChangedNowSh, windows),

                // 12-14. The worked example for chains, and the reason there are three of them:
                //        passing a value is only visible once it has been passed twice. Step one
                //        produces a path, step two turns that path into a number, step three
                //        reports what it was handed - and step three can see step two but not
                //        step one, which is the rule the three of them exist to demonstrate.
                Script("Chain: find the newest file", windows ? HandOnePs : HandOneSh, windows),
                Script("Chain: measure what I was given", windows ? HandTwoPs : HandTwoSh, windows),
                Script("Chain: show what I was given", windows ? HandThreePs : HandThreeSh, windows),

                // 15. The worked example for secrets: the vault is seeded with one, and this
                //     prints it, so {{secret:NAME}} can be seen working before anyone trusts it
                //     with a real token.
                SecretExample(),
            ];
        }

        /// <summary>The name of the secret the vault is seeded with.</summary>
        public const string SecretExampleName = "secretExample";

        /// <summary>The command that prints it.</summary>
        public const string SecretExampleCommand = "Show the example secret";

        /// <summary>What the example secret holds: the app's name and version.</summary>
        public static string SecretExampleValue => $"DevDeck {AppVersion.Number}";

        public static CustomCommand SecretExample()
        {
            bool windows = OperatingSystem.IsWindows();

            return Script(SecretExampleCommand, windows ? SecretExamplePs : SecretExampleSh, windows);
        }

        /// <summary>
        ///  Puts the example secret in the vault, or brings it up to this version.
        /// </summary>
        /// <remarks>
        ///  Only a value that is still the seeded one - "DevDeck" and a version - is touched. One
        ///  the user has changed to something else is theirs, and one they deleted stays deleted
        ///  once the seed has been recorded as done.
        /// </remarks>
        public static void SeedSecret(bool firstTime)
        {
            string? held = SecretVault.Instance.Value(SecretExampleName);

            if (held is null ? firstTime : held != SecretExampleValue && held.StartsWith("DevDeck ", StringComparison.Ordinal))
            {
                SecretVault.Instance.Set(SecretExampleName, SecretExampleValue);
            }
        }

        /// <summary>
        ///  Adds the example environment variables - appName and appVersion - and keeps
        ///  appVersion on this build. Returns whether anything changed.
        /// </summary>
        /// <remarks>
        ///  Added once, on the first launch that knows about them; a variable the user deletes
        ///  afterwards stays deleted. appVersion is moved forward on an upgrade only while it
        ///  still holds a bare version number, so one the user has set to anything else is left.
        /// </remarks>
        public static bool SeedVariables(List<CommandArgument> variables, bool firstTime)
        {
            bool changed = false;

            CommandArgument? Find(string name) =>
                variables.Find(variable => variable.Name.Equals(name, StringComparison.Ordinal));

            if (firstTime)
            {
                if (Find("appName") is null)
                {
                    variables.Add(new CommandArgument { Name = "appName", Value = "DevDeck" });
                    changed = true;
                }

                if (Find("appVersion") is null)
                {
                    variables.Add(new CommandArgument { Name = "appVersion", Value = AppVersion.Number });
                    changed = true;
                }
            }

            if (Find("appVersion") is { } version
                && version.Value != AppVersion.Number
                && System.Text.RegularExpressions.Regex.IsMatch(version.Value, @"^\d+\.\d+\.\d+(-[\w.]+)?$"))
            {
                version.Value = AppVersion.Number;
                changed = true;
            }

            return changed;
        }

        private const string SecretExamplePs = """
            # The worked example for secrets and environment variables.
            #
            # {{secret:secretExample}} is filled in from the vault when this runs, so the value is
            # never stored in the command or in settings.json. appName and appVersion are ordinary
            # environment variables, set on every command from Settings > Environment variables.
            Write-Output "Secret name  : secretExample"
            Write-Output "Secret value : {{secret:secretExample}}"
            Write-Output "appName      : $env:appName"
            Write-Output "appVersion   : $env:appVersion"
            """;

        private const string SecretExampleSh = """
            # The worked example for secrets and environment variables.
            #
            # {{secret:secretExample}} is filled in from the vault when this runs, so the value is
            # never stored in the command or in settings.json. appName and appVersion are ordinary
            # environment variables, set on every command from Settings > Environment variables.
            echo "Secret name  : secretExample"
            echo "Secret value : {{secret:secretExample}}"
            echo "appName      : $appName"
            echo "appVersion   : $appVersion"
            """;

        /// <summary>
        ///  The chains a fresh install starts with.
        /// </summary>
        /// <remarks>
        ///  Two, for the same reason the deck ships ten commands rather than none: "No chains yet"
        ///  explains neither what a chain is nor why anyone would want one. Both are built only
        ///  from starter commands, so neither is broken on the first run.
        /// </remarks>
        public static List<CommandChain> Chains() =>
        [
            // The morning routine. Ordered so the cheap orientation question comes first and the
            // slow one last, which is the order you would run them by hand.
            new()
            {
                Name = "Morning check",
                Steps = ["Where am I", "Today's commits", "What changed vs main"],

                // Off: these are three independent questions rather than a pipeline, and a folder
                // with no upstream branch should not stop the other two from answering.
                StopOnFailure = false,
            },

            // The other half of what a deck is for: the housekeeping questions nobody runs until
            // the disk is full.
            new()
            {
                Name = "Health check",
                Steps = ["Tool versions", "Heavy build folders", "Outdated packages"],
                StopOnFailure = false,
            },

            // The worked example. Unlike the other two this one is a genuine pipeline: each step
            // is fed by the one before it, which is also why it is the only shipped chain that
            // stops on failure - step two given no path has nothing to measure, and running it
            // anyway would produce an error about an empty filename rather than about step one.
            new()
            {
                Name = "Pass values between steps",
                Steps =
                [
                    "Chain: find the newest file",
                    "Chain: measure what I was given",
                    "Chain: show what I was given",
                ],
                StopOnFailure = true,
            },
        ];

        /// <summary>
        ///  The watch rules a fresh install starts with.
        /// </summary>
        /// <remarks>
        ///  Off. Every other starter is inert until clicked, and a rule that arrived switched on
        ///  would start running commands against whatever folder the user first opened, before
        ///  they had read what it does. The switch is one click and the row says what it will do.
        /// </remarks>
        public static List<WatchRule> Watches() =>
        [
            new()
            {
                Name = "Show me what changed",
                Command = "What changed just now",
                Pattern = "*.cs;*.axaml;*.ts;*.tsx;*.js;*.py;*.go;*.rs;*.java;*.json;*.md",
                Enabled = false,
            },
        ];

        /// <summary>
        ///  The environments a fresh install starts with.
        /// </summary>
        /// <remarks>
        ///  Three, named for the three deployments nearly every project has, and each one holding
        ///  the same single variable with no value in it. Empty is the point. An environment with
        ///  nothing in it is a menu entry that does nothing and teaches nothing; one that already
        ///  has the variable every request needs turns the first pasted curl into a working setup,
        ///  because the importer moves the Authorization value it finds into whichever of these is
        ///  selected and leaves <c>{{Authorization}}</c> behind in the request.
        ///
        ///  That is also what keeps the token out of settings.json: marked secret, the value goes
        ///  to the OS credential store under the environment's name, and what is saved beside the
        ///  request is a reference to it. Shipping three of them rather than one is what makes that
        ///  useful - the same request, run against production and against a local box, differing
        ///  only in which environment is picked from the menu.
        ///
        ///  Local is last and Prod first, in the order they are most often reached for, and the
        ///  active one is left unset: choosing production by default is how a request meant for a
        ///  local box goes somewhere it should not.
        /// </remarks>
        public static List<HttpEnvironment> Environments() =>
        [
            new()
            {
                Name = "Prod",
                Values = [Authorization()],
            },
            new()
            {
                Name = "Dev",
                Values = [Authorization()],
            },
            new()
            {
                Name = "Local",
                Values = [Authorization()],
            },
        ];

        /// <summary>The one variable every seeded environment has: empty, secret, and ready.</summary>
        private static EnvironmentValue Authorization() => new()
        {
            Name = "Authorization",
            Value = string.Empty,

            // Secret from the start, because the first value to land here will be a bearer token -
            // and a value that arrives in a non-secret variable has already been written to
            // settings.json in plain text by the time anybody thinks to tick the box.
            IsSecret = true,
            Enabled = true,
        };

        #region Scripts
        private const string ChangedVsMainPs = """
            $base = $null
            foreach ($name in 'main', 'master', 'develop') {
                if (git rev-parse --verify --quiet $name) { $base = $name; break }
            }

            if (-not $base) { 'No main, master or develop branch here.'; return }

            "Comparing against $base."
            ''
            'Commits on this branch:'
            git log --oneline "$base..HEAD"
            ''
            'Files changed:'
            git diff --stat "$base...HEAD"
            """;

        private const string ChangedVsMainSh = """
            base=""
            for name in main master develop; do
              if git rev-parse --verify --quiet "$name" >/dev/null 2>&1; then base="$name"; break; fi
            done

            if [ -z "$base" ]; then echo "No main, master or develop branch here."; exit 0; fi

            echo "Comparing against $base."
            echo
            echo "Commits on this branch:"
            git log --oneline "$base..HEAD"
            echo
            echo "Files changed:"
            git diff --stat "$base...HEAD"
            """;

        /// <remarks>
        ///  A one-liner until it was reported as broken. `git log --since=midnight` prints nothing
        ///  and exits zero on a day you have not committed yet, so the command looked like it had
        ///  failed when it had in fact answered: nothing. It now says so, and says the same thing
        ///  when the folder is not a repository at all - which was the other way to get silence.
        /// </remarks>
        private const string TodayPs = """
            if (-not (git rev-parse --is-inside-work-tree 2>$null)) {
                'Not a git repository. Choose a workspace that is one.'; return
            }

            $log = git log --since=midnight --oneline --stat

            if (-not $log) { 'No commits yet today.'; return }

            $log
            """;

        private const string TodaySh = """
            if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
              echo "Not a git repository. Choose a workspace that is one."; exit 0
            fi

            log=$(git log --since=midnight --oneline --stat)

            if [ -z "$log" ]; then echo "No commits yet today."; exit 0; fi

            echo "$log"
            """;

        /// <remarks>
        ///  Capped, and deliberately. A bare `git grep` across a large checkout returns tens of
        ///  thousands of hits, and the panel has to keep every one of them - so the freeze people
        ///  reported was the app being handed more output than any human was going to read. The
        ///  head of the list answers the question; the count at the end says what was left.
        ///
        ///  The two runs are not one pipeline because the total has to be known before the list is
        ///  cut, and a count taken after `head` would only ever report the cap back.
        /// </remarks>
        private const string TodosPs = """
            if (-not (git rev-parse --is-inside-work-tree 2>$null)) {
                'Not a git repository. Choose a workspace that is one.'; return
            }

            $hits = git grep -n -E 'TODO|FIXME|HACK|XXX'

            if (-not $hits) { 'None found. '; return }

            $hits | Select-Object -First 200

            if ($hits.Count -gt 200) {
                ''
                "Showing the first 200 of $($hits.Count). Narrow it with a folder to see the rest."
            }
            """;

        private const string TodosSh = """
            if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
              echo "Not a git repository. Choose a workspace that is one."; exit 0
            fi

            hits=$(git grep -n -E 'TODO|FIXME|HACK|XXX')

            if [ -z "$hits" ]; then echo "None found."; exit 0; fi

            total=$(echo "$hits" | wc -l | tr -d ' ')

            echo "$hits" | head -200

            if [ "$total" -gt 200 ]; then
              echo
              echo "Showing the first 200 of $total. Narrow it with a folder to see the rest."
            fi
            """;

        private const string FolderSizesPs = """
            Get-ChildItem -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
                $bytes = (Get-ChildItem $_.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                    Measure-Object Length -Sum).Sum
                [PSCustomObject]@{
                    SizeMB = [math]::Round((($bytes, 0 -ne $null)[0] / 1MB), 1)
                    Folder = $_.Name
                }
            } | Sort-Object SizeMB -Descending | Format-Table -AutoSize
            """;

        private const string FolderSizesSh = """
            du -sh ./*/ 2>/dev/null | sort -rh | head -25
            """;

        private const string BuildFoldersPs = """
            $names = 'node_modules', 'bin', 'obj', 'dist', 'build', 'target', '.venv', '__pycache__', '.next'

            Get-ChildItem -Recurse -Directory -Depth 4 -Force -ErrorAction SilentlyContinue |
                Where-Object { $names -contains $_.Name } |
                ForEach-Object {
                    $bytes = (Get-ChildItem $_.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                        Measure-Object Length -Sum).Sum
                    [PSCustomObject]@{
                        SizeMB = [math]::Round((($bytes, 0 -ne $null)[0] / 1MB), 1)
                        Path   = Resolve-Path -Relative $_.FullName
                    }
                } | Sort-Object SizeMB -Descending | Select-Object -First 25 | Format-Table -AutoSize
            """;

        // -prune, so it reports node_modules without walking the hundred thousand files inside it.
        private const string BuildFoldersSh = """
            find . -maxdepth 5 -type d \( -name node_modules -o -name bin -o -name obj \
                -o -name dist -o -name build -o -name target -o -name .venv \
                -o -name __pycache__ -o -name .next \) -prune -print0 2>/dev/null |
              xargs -0 du -sh 2>/dev/null | sort -rh | head -25
            """;

        private const string OutdatedPs = """
            $found = $false

            if (Test-Path package.json) {
                $found = $true
                '== npm =='
                npm outdated
            }

            if (Get-ChildItem -Path . -Recurse -Depth 2 -Include *.sln, *.csproj, *.fsproj -File -ErrorAction SilentlyContinue | Select-Object -First 1) {
                $found = $true
                ''
                '== dotnet =='
                dotnet list package --outdated
            }

            if (Test-Path requirements.txt) {
                $found = $true
                ''
                '== pip =='
                pip list --outdated
            }

            if (-not $found) { 'No package.json, project file or requirements.txt in this folder.' }
            """;

        private const string OutdatedSh = """
            found=0

            if [ -f package.json ]; then found=1; echo "== npm =="; npm outdated; fi

            if ls ./*.sln ./*.csproj ./*.fsproj >/dev/null 2>&1; then
              found=1; echo; echo "== dotnet =="; dotnet list package --outdated
            fi

            if [ -f requirements.txt ]; then found=1; echo; echo "== pip =="; pip list --outdated; fi

            if [ "$found" -eq 0 ]; then echo "No package.json, project file or requirements.txt in this folder."; fi
            """;

        private const string PortsPs = """
            $ports = 3000, 3001, 4200, 5000, 5173, 5432, 6379, 8000, 8080, 8081, 9229, 27017

            $listening = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
                Where-Object { $ports -contains $_.LocalPort } |
                Select-Object LocalAddress, LocalPort,
                    @{ Name = 'Process'; Expression = { (Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName } },
                    @{ Name = 'PID'; Expression = { $_.OwningProcess } } |
                Sort-Object LocalPort

            if ($listening) { $listening | Format-Table -AutoSize }
            else { 'Nothing is listening on the usual dev ports.' }
            """;

        private const string PortsSh = """
            ports="3000|3001|4200|5000|5173|5432|6379|8000|8080|8081|9229|27017"

            if command -v lsof >/dev/null 2>&1; then
              lsof -nP -iTCP -sTCP:LISTEN 2>/dev/null | awk -v p="$ports" 'NR == 1 || $9 ~ ":(" p ")$"'
            elif command -v ss >/dev/null 2>&1; then
              ss -tlnp 2>/dev/null | grep -E ":($ports)[[:space:]]"
            else
              echo "Neither lsof nor ss is installed."
            fi
            """;

        // Prints the run context rather than doing anything with it - which is the point. Someone
        // writing their own watch command needs to know what they are given and what it looks
        // like, and the fastest way to learn that is to run this once and read the output.
        private const string ChangedNowPs = """
            if (-not $env:DEVDECK_TRIGGER) { 'No trigger set. Run this from a file watch.'; return }

            "Trigger : $env:DEVDECK_TRIGGER"

            if ($env:DEVDECK_TRIGGER -ne 'watch') {
                ''
                'Started by hand, so there is nothing that changed to report.'
                'Turn on the "Show me what changed" watch and save a file to see the rest.'
                return
            }

            "Watch   : $env:DEVDECK_WATCH_NAME"
            "Folder  : $env:DEVDECK_WATCH_FOLDER"
            "What    : $env:DEVDECK_CHANGED_KIND"
            "When    : $env:DEVDECK_CHANGED_AT"
            "File    : $env:DEVDECK_CHANGED_NAME"
            "Path    : $env:DEVDECK_CHANGED_FILE"
            "Count   : $env:DEVDECK_CHANGED_COUNT"
            ''
            'All of them:'

            # One path per line, which is why the list is newline-separated: a path may contain a
            # space or a semicolon, and may not contain a newline.
            $env:DEVDECK_CHANGED_FILES -split "`n" | ForEach-Object { "  $_" }
            """;

        private const string ChangedNowSh = """
            if [ -z "$DEVDECK_TRIGGER" ]; then
              echo "No trigger set. Run this from a file watch."
              exit 0
            fi

            echo "Trigger : $DEVDECK_TRIGGER"

            if [ "$DEVDECK_TRIGGER" != "watch" ]; then
              echo
              echo "Started by hand, so there is nothing that changed to report."
              echo 'Turn on the "Show me what changed" watch and save a file to see the rest.'
              exit 0
            fi

            echo "Watch   : $DEVDECK_WATCH_NAME"
            echo "Folder  : $DEVDECK_WATCH_FOLDER"
            echo "What    : $DEVDECK_CHANGED_KIND"
            echo "When    : $DEVDECK_CHANGED_AT"
            echo "File    : $DEVDECK_CHANGED_NAME"
            echo "Path    : $DEVDECK_CHANGED_FILE"
            echo "Count   : $DEVDECK_CHANGED_COUNT"
            echo
            echo "All of them:"

            # Quoted, so a path with a space stays one line rather than becoming two.
            echo "$DEVDECK_CHANGED_FILES" | while IFS= read -r path; do
              [ -n "$path" ] && echo "  $path"
            done
            """;

        /// <summary>
        ///  Step one: produces a value, and puts it where the next step will look.
        /// </summary>
        /// <remarks>
        ///  The path goes on its own final line deliberately. A step is read by the next one
        ///  through DEVDECK_PREVIOUS_LINE - the last line with anything on it - so a script meant
        ///  to feed another ends by echoing one value and nothing else, and everything it wants to
        ///  say to the human goes above that.
        /// </remarks>
        private const string HandOnePs = """
            $skip = '\\(\.git|node_modules|bin|obj|dist|target|publish)\\'

            $newest = Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue -Include `
                    *.cs, *.ts, *.tsx, *.js, *.py, *.go, *.rs, *.java, *.md |
                Where-Object { $_.FullName -notmatch $skip } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1

            if (-not $newest) {
                'No source files under this folder, so there is nothing to hand on.'
                'Point the deck at a project folder and run the chain again.'
                exit 1
            }

            "The newest source file here, changed $($newest.LastWriteTime.ToString('yyyy-MM-dd HH:mm')):"
            ''
            'The next step reads my last line, so the path goes last and on its own:'
            $newest.FullName
            """;

        private const string HandOneSh = """
            newest=$(find . -type f \( -name '*.cs' -o -name '*.ts' -o -name '*.tsx' \
                -o -name '*.js' -o -name '*.py' -o -name '*.go' -o -name '*.rs' \
                -o -name '*.java' -o -name '*.md' \) \
              -not -path '*/.git/*' -not -path '*/node_modules/*' -not -path '*/bin/*' \
              -not -path '*/obj/*' -not -path '*/dist/*' -not -path '*/target/*' \
              -not -path '*/publish/*' -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1)

            if [ -z "$newest" ]; then
              echo "No source files under this folder, so there is nothing to hand on."
              echo "Point the deck at a project folder and run the chain again."
              exit 1
            fi

            echo "The newest source file here:"
            echo
            echo "The next step reads my last line, so the path goes last and on its own:"
            echo "$newest"
            """;

        /// <summary>
        ///  Step two: consumes step one's value and produces one of its own.
        /// </summary>
        /// <remarks>
        ///  Written so it still says something sensible when run on its own from the deck, which
        ///  is how anyone will first meet it. A step that only works inside its chain teaches
        ///  nothing about what the chain is doing.
        /// </remarks>
        private const string HandTwoPs = """
            $path = $env:DEVDECK_PREVIOUS_LINE

            if (-not $env:DEVDECK_PREVIOUS_NAME) {
                'Nobody handed me anything - I was started by hand rather than by a chain.'
                'Run the "Pass values between steps" chain to see what I do with what I am given.'
                exit 0
            }

            "Step $env:DEVDECK_STEP of $env:DEVDECK_STEPS in the chain $env:DEVDECK_CHAIN"
            "Handed to me by : $env:DEVDECK_PREVIOUS_NAME (which exited $env:DEVDECK_PREVIOUS_EXIT)"
            "The value       : $path"
            ''

            if (-not (Test-Path -LiteralPath $path)) {
                "That is not a path I can find, so I have nothing to measure."
                exit 1
            }

            $lines = @(Get-Content -LiteralPath $path -ErrorAction SilentlyContinue).Count
            $bytes = (Get-Item -LiteralPath $path).Length

            "Lines           : $lines"
            "Bytes           : $bytes"
            ''
            'And the one number I hand to the step after me:'
            $lines
            """;

        private const string HandTwoSh = """
            path="$DEVDECK_PREVIOUS_LINE"

            if [ -z "$DEVDECK_PREVIOUS_NAME" ]; then
              echo "Nobody handed me anything - I was started by hand rather than by a chain."
              echo 'Run the "Pass values between steps" chain to see what I do with what I am given.'
              exit 0
            fi

            echo "Step $DEVDECK_STEP of $DEVDECK_STEPS in the chain $DEVDECK_CHAIN"
            echo "Handed to me by : $DEVDECK_PREVIOUS_NAME (which exited $DEVDECK_PREVIOUS_EXIT)"
            echo "The value       : $path"
            echo

            if [ ! -f "$path" ]; then
              echo "That is not a path I can find, so I have nothing to measure."
              exit 1
            fi

            lines=$(wc -l < "$path" | tr -d ' ')
            bytes=$(wc -c < "$path" | tr -d ' ')

            echo "Lines           : $lines"
            echo "Bytes           : $bytes"
            echo
            echo "And the one number I hand to the step after me:"
            echo "$lines"
            """;

        /// <summary>
        ///  Step three: shows everything a step is given, including what it is not given.
        /// </summary>
        /// <remarks>
        ///  The one that answers "what can a step actually see". It prints the whole of the
        ///  previous step's output as well as its last line, so the difference between
        ///  DEVDECK_PREVIOUS and DEVDECK_PREVIOUS_LINE is visible rather than described - and it
        ///  says out loud that step one is not among what it was handed.
        /// </remarks>
        private const string HandThreePs = """
            if (-not $env:DEVDECK_PREVIOUS_NAME) {
                'Nobody handed me anything - I was started by hand rather than by a chain.'
                'Run the "Pass values between steps" chain to see the rest.'
                exit 0
            }

            "Step $env:DEVDECK_STEP of $env:DEVDECK_STEPS in the chain $env:DEVDECK_CHAIN"
            ''
            "The step before me was '$env:DEVDECK_PREVIOUS_NAME', and it exited $env:DEVDECK_PREVIOUS_EXIT."
            "Its last line - the value meant for me - was: $env:DEVDECK_PREVIOUS_LINE"
            ''
            'Everything it printed, which I also get:'

            $env:DEVDECK_PREVIOUS -split "`n" | ForEach-Object { "  | $_" }

            ''
            'Only the step immediately before me is handed on, which is why I can see step two'
            'and not step one. Anything that has to travel further than one step, re-print.'
            """;

        private const string HandThreeSh = """
            if [ -z "$DEVDECK_PREVIOUS_NAME" ]; then
              echo "Nobody handed me anything - I was started by hand rather than by a chain."
              echo 'Run the "Pass values between steps" chain to see the rest.'
              exit 0
            fi

            echo "Step $DEVDECK_STEP of $DEVDECK_STEPS in the chain $DEVDECK_CHAIN"
            echo
            echo "The step before me was '$DEVDECK_PREVIOUS_NAME', and it exited $DEVDECK_PREVIOUS_EXIT."
            echo "Its last line - the value meant for me - was: $DEVDECK_PREVIOUS_LINE"
            echo
            echo "Everything it printed, which I also get:"

            echo "$DEVDECK_PREVIOUS" | while IFS= read -r line; do
              echo "  | $line"
            done

            echo
            echo "Only the step immediately before me is handed on, which is why I can see step two"
            echo "and not step one. Anything that has to travel further than one step, re-print."
            """;

        private const string VersionsPs = """
            foreach ($tool in 'git', 'node', 'npm', 'dotnet', 'python', 'docker') {
                if (Get-Command $tool -ErrorAction SilentlyContinue) {
                    $version = (& $tool --version 2>&1 | Select-Object -First 1)
                    '{0,-8} {1}' -f $tool, $version
                }
                else { '{0,-8} not installed' -f $tool }
            }
            """;

        private const string VersionsSh = """
            for tool in git node npm dotnet python3 docker; do
              if command -v "$tool" >/dev/null 2>&1; then
                printf '%-8s %s\n' "$tool" "$("$tool" --version 2>&1 | head -1)"
              else
                printf '%-8s not installed\n' "$tool"
              fi
            done
            """;
        #endregion

        /// <summary>A script, as PowerShell on Windows and as a shell script everywhere else.</summary>
        /// <summary>
        ///  Brings starter commands in <paramref name="deck"/> up to the version this build ships,
        ///  and returns the names that were replaced.
        /// </summary>
        /// <remarks>
        ///  Two of the starters shipped broken and were fixed in the source, which did nothing for
        ///  anyone who already had them: <see cref="For"/> seeds on a genuinely fresh start only, so
        ///  every existing install kept the old body. "Today's commits" stayed a bare
        ///  `git log --since=midnight`, which prints nothing and exits zero on a day with no commits
        ///  - the reported symptom was "it produces no output", and that was literally true. "TODOs
        ///  and FIXMEs" stayed an uncapped `git grep` that hands the panel tens of thousands of
        ///  lines and freezes it.
        ///
        ///  A shipped command is only replaced when the stored body still matches one this app is
        ///  known to have shipped. That is the whole safety argument: an edited command does not
        ///  match, so a user's own work is never overwritten by an upgrade, and a command they
        ///  deleted is not resurrected - only what is still there and still untouched is repaired.
        ///  Matching on the name alone would fail both of those tests.
        ///
        ///  Kind is set too, because the broken Today's commits was stored as a Batch file while its
        ///  replacement is a PowerShell script, and a body without its interpreter is worse than the
        ///  bug it replaces.
        /// </remarks>
        public static List<string> Refresh(List<CustomCommand> deck)
        {
            bool windows = OperatingSystem.IsWindows();
            List<string> mended = [];

            foreach (CustomCommand shipped in For())
            {
                CustomCommand? held = deck.Find(
                    command => command.Name.Equals(shipped.Name, StringComparison.Ordinal));

                if (held is null || held.Command == shipped.Command)
                {
                    continue;
                }

                if (!WasShipped(shipped.Name, held.Command, windows))
                {
                    continue;
                }

                held.Command = shipped.Command;
                held.Kind = shipped.Kind;
                mended.Add(shipped.Name);
            }

            return mended;
        }

        /// <summary>Whether <paramref name="body"/> is a version of this command the app once shipped.</summary>
        /// <remarks>
        ///  Compared with whitespace collapsed, because the file has been round-tripped through JSON
        ///  and both line ending conventions since it was written, and a command is not user-edited
        ///  merely because it now has CRLFs.
        /// </remarks>
        private static bool WasShipped(string name, string body, bool windows)
        {
            string[] known = name switch
            {
                "Today's commits" =>
                [
                    "git log --since=midnight --oneline --stat",
                    windows ? TodaySh : TodayPs,
                ],
                "TODOs and FIXMEs" =>
                [
                    "git grep -n -E \"TODO|FIXME|HACK|XXX\"",
                    "git grep -n -E 'TODO|FIXME|HACK|XXX'",
                    windows ? TodosSh : TodosPs,
                ],
                _ => [],
            };

            return Array.Exists(known, one => Flat(one) == Flat(body));
        }

        private static string Flat(string text) =>
            string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static CustomCommand Script(string name, string body, bool windows) =>
            Make(name, body, windows ? CommandKind.PowerShell : CommandKind.Bash);

        private static CustomCommand Make(
            string name, string body, CommandKind kind, bool detached = false) => new()
            {
                Name = name,
                Command = body,
                Kind = Runnable(kind) ? kind : CommandKind.Shell,
                Detached = detached,
            };

        /// <summary>
        ///  Whether this machine has an interpreter for the kind.
        /// </summary>
        /// <remarks>
        ///  Only bash can be genuinely absent here - it is the one the Windows fallback needs, and
        ///  a Windows box without Git for Windows or WSL has none. Falling back to Shell keeps the
        ///  entry in the deck, where it fails with a readable error if it is ever clicked, rather
        ///  than the starter deck silently differing between two machines for no visible reason.
        /// </remarks>
        private static bool Runnable(CommandKind kind) => kind switch
        {
            CommandKind.Batch => OperatingSystem.IsWindows(),
            CommandKind.Bash => ScriptFile.FindBash() is not null,
            _ => true,
        };
    }
}
