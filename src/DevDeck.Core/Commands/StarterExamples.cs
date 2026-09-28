namespace DevDeck.Core
{
    /// <summary>
    ///  Small worked examples of parameters and chains, for learning from rather than for use.
    /// </summary>
    /// <remarks>
    ///  The starter deck answers real questions, which makes it a poor place to learn the
    ///  mechanics: a script that finds the base branch is mostly about git. These do nothing
    ///  useful on purpose - say hello, count some files, write a line to a log - so the only thing
    ///  in each one worth reading is how the value got in and how it gets passed on.
    ///
    ///  Every name starts with "Example:" so the lot can be found, and deleted, together. The one
    ///  thing any of them writes is a log file in the temp folder, never the project.
    /// </remarks>
    internal static class StarterExamples
    {
        /// <summary>Recorded in <see cref="AppSettings.SeededExtras"/> once these have been offered.</summary>
        public const string SeedKey = "examples-1";

        public static List<CustomCommand> Commands()
        {
            bool windows = OperatingSystem.IsWindows();

            return
            [
                // Parameters: a row under the body, passed on every run.
                Script("Example: hello with a parameter", windows ? HelloPs : HelloSh, windows,
                    ("Name", "DevDeck")),

                // {{...}}: asked for in a box each time Run is pressed.
                Script("Example: ask before running", windows ? AskPs : AskSh, windows),

                // The reusable step: logs its parameter by hand, or what it was handed in a chain.
                Script("Example: write to the log", windows ? WriteLogPs : WriteLogSh, windows,
                    ("Message", "Hello from DevDeck"), ("Level", "info")),

                Script("Example: show the log", windows ? ShowLogPs : ShowLogSh, windows,
                    ("Lines", "10")),

                Script("Example: count files", windows ? CountPs : CountSh, windows,
                    ("Pattern", "*.md")),

                Script("Example: double it", windows ? DoublePs : DoubleSh, windows,
                    ("Number", "21")),

                Script("Example: hand on several values", windows ? SeveralPs : SeveralSh, windows),

                Script("Example: read several values", windows ? ReadSeveralPs : ReadSeveralSh, windows),

                Script("Example: fail on purpose", windows ? FailPs : FailSh, windows,
                    ("Code", "1")),
            ];
        }

        public static List<CommandChain> Chains() =>
        [
            // The smallest pipeline: step one's last line is the log's path, step two reads it.
            new()
            {
                Name = "Example: log a message",
                Steps = ["Example: write to the log", "Example: show the log"],
                StopOnFailure = true,
            },

            // A value changing as it travels: a count, then twice it, then a log line saying so.
            new()
            {
                Name = "Example: count, double, log",
                Steps =
                [
                    "Example: count files",
                    "Example: double it",
                    "Example: write to the log",
                    "Example: show the log",
                ],
                StopOnFailure = true,
            },

            // More than one value at once, as NAME=value lines.
            new()
            {
                Name = "Example: several values",
                Steps = ["Example: hand on several values", "Example: read several values"],
                StopOnFailure = true,
            },

            // The last step never runs: that is the point.
            new()
            {
                Name = "Example: stop on failure",
                Steps =
                [
                    "Example: hello with a parameter",
                    "Example: fail on purpose",
                    "Example: hello with a parameter",
                ],
                StopOnFailure = true,
            },
        ];

        #region Scripts

        private const string HelloPs = """
            param([string]$Name = 'world')

            # PARAMETERS are the rows under the body. PowerShell gets each one as -Name "value",
            # and the param() line above picks it up by name. Change the Name row and run again.
            "Hello, $Name!"
            ''
            'The same value is in the environment, for scripts that would rather read it there:'
            "DEVDECK_ARG_NAME = $env:DEVDECK_ARG_NAME"
            """;

        private const string HelloSh = """
            # PARAMETERS are the rows under the body. A shell script gets them in order as $1, $2.
            # Change the Name row and run again.
            name="${1:-world}"

            echo "Hello, $name!"
            echo
            echo "The same value is in the environment, for scripts that would rather read it there:"
            echo "DEVDECK_ARG_NAME = $DEVDECK_ARG_NAME"
            """;

        private const string AskPs = """
            # {{Name}} is asked for in a box every time you press Run, and your answer is pasted
            # in before the script starts. {{Name:world}} suggests "world" as the answer.
            # Handy for values that change every run; use a parameter row for ones that do not.
            $name     = '{{Name:world}}'
            $greeting = '{{Greeting:Hello}}'

            "$greeting, $name!"
            """;

        private const string AskSh = """
            # {{Name}} is asked for in a box every time you press Run, and your answer is pasted
            # in before the script starts. {{Name:world}} suggests "world" as the answer.
            # Handy for values that change every run; use a parameter row for ones that do not.
            name='{{Name:world}}'
            greeting='{{Greeting:Hello}}'

            echo "$greeting, $name!"
            """;

        private const string WriteLogPs = """
            param([string]$Message = 'Hello from DevDeck', [string]$Level = 'info')

            # Run by hand, I log my Message parameter. In a chain, I log what the step before me
            # handed on instead: its name and its last line.
            if ($env:DEVDECK_PREVIOUS_NAME) {
                $Message = "$env:DEVDECK_PREVIOUS_NAME said $env:DEVDECK_PREVIOUS_LINE"
            }

            $log = Join-Path ([IO.Path]::GetTempPath()) 'DevDeck\examples.log'
            New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null

            $line = '{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level.ToUpper(), $Message
            Add-Content -LiteralPath $log -Value $line -Encoding UTF8

            "Wrote: $line"
            ''
            # The next step in a chain reads my LAST line, so the one value meant for it goes last.
            'The log file, on my last line for the next step:'
            $log
            """;

        private const string WriteLogSh = """
            # Parameters arrive in order: $1 is Message, $2 is Level.
            message="${1:-Hello from DevDeck}"
            level="${2:-info}"

            # Run by hand, I log my Message parameter. In a chain, I log what the step before me
            # handed on instead: its name and its last line.
            if [ -n "$DEVDECK_PREVIOUS_NAME" ]; then
              message="$DEVDECK_PREVIOUS_NAME said $DEVDECK_PREVIOUS_LINE"
            fi

            log="${TMPDIR:-/tmp}/DevDeck/examples.log"
            mkdir -p "$(dirname "$log")"

            line="$(date '+%Y-%m-%d %H:%M:%S') [$(echo "$level" | tr '[:lower:]' '[:upper:]')] $message"
            echo "$line" >> "$log"

            echo "Wrote: $line"
            echo
            # The next step in a chain reads my LAST line, so the one value meant for it goes last.
            echo "The log file, on my last line for the next step:"
            echo "$log"
            """;

        private const string ShowLogPs = """
            param([int]$Lines = 10)

            # In a chain the path comes from the step before me; by hand I use the usual place.
            $log = if ($env:DEVDECK_PREVIOUS_NAME) { $env:DEVDECK_PREVIOUS_LINE }
                   else { Join-Path ([IO.Path]::GetTempPath()) 'DevDeck\examples.log' }

            if (-not (Test-Path -LiteralPath $log)) {
                "No log at $log yet. Run 'Example: write to the log' first."
                exit 1
            }

            "The last $Lines lines of $log :"
            ''
            Get-Content -LiteralPath $log -Tail $Lines
            """;

        private const string ShowLogSh = """
            lines="${1:-10}"

            # In a chain the path comes from the step before me; by hand I use the usual place.
            if [ -n "$DEVDECK_PREVIOUS_NAME" ]; then
              log="$DEVDECK_PREVIOUS_LINE"
            else
              log="${TMPDIR:-/tmp}/DevDeck/examples.log"
            fi

            if [ ! -f "$log" ]; then
              echo "No log at $log yet. Run 'Example: write to the log' first."
              exit 1
            fi

            echo "The last $lines lines of $log :"
            echo
            tail -n "$lines" "$log"
            """;

        private const string CountPs = """
            param([string]$Pattern = '*.md')

            $skip = '\\(\.git|node_modules|bin|obj|dist|target|publish)\\'
            $files = @(Get-ChildItem -Recurse -File -Filter $Pattern -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch $skip })

            "$($files.Count) files matching $Pattern under $(Get-Location)"
            ''
            'The count, alone on my last line for the next step:'
            $files.Count
            """;

        private const string CountSh = """
            pattern="${1:-*.md}"

            count=$(find . -type f -name "$pattern" \
              -not -path '*/.git/*' -not -path '*/node_modules/*' -not -path '*/bin/*' \
              -not -path '*/obj/*' -not -path '*/dist/*' -not -path '*/target/*' \
              -not -path '*/publish/*' 2>/dev/null | wc -l | tr -d ' ')

            echo "$count files matching $pattern under $(pwd)"
            echo
            echo "The count, alone on my last line for the next step:"
            echo "$count"
            """;

        private const string DoublePs = """
            param([int]$Number = 21)

            # A chain's value wins over my parameter; by hand, the Number row is used.
            if ($env:DEVDECK_PREVIOUS_NAME) {
                $handed = $env:DEVDECK_PREVIOUS_LINE

                if ($handed -notmatch '^-?\d+$') {
                    "'$env:DEVDECK_PREVIOUS_NAME' handed me '$handed', which is not a whole number."
                    exit 1
                }

                $Number = [int]$handed
                "Handed $Number by '$env:DEVDECK_PREVIOUS_NAME' (step $env:DEVDECK_STEP of $env:DEVDECK_STEPS)."
            }
            else {
                "Nobody handed me a number, so I use my Number parameter: $Number."
            }

            ''
            'Doubled, on my last line:'
            $Number * 2
            """;

        private const string DoubleSh = """
            number="${1:-21}"

            # A chain's value wins over my parameter; by hand, the Number row is used.
            if [ -n "$DEVDECK_PREVIOUS_NAME" ]; then
              number="$DEVDECK_PREVIOUS_LINE"

              case "$number" in
                ''|*[!0-9-]*)
                  echo "'$DEVDECK_PREVIOUS_NAME' handed me '$number', which is not a whole number."
                  exit 1 ;;
              esac

              echo "Handed $number by '$DEVDECK_PREVIOUS_NAME' (step $DEVDECK_STEP of $DEVDECK_STEPS)."
            else
              echo "Nobody handed me a number, so I use my Number parameter: $number."
            fi

            echo
            echo "Doubled, on my last line:"
            echo $((number * 2))
            """;

        private const string SeveralPs = """
            # One value travels as my last line. Several travel as NAME=value lines: the next step
            # gets everything I print in DEVDECK_PREVIOUS and picks out the ones it wants.
            "USER=$env:USERNAME"
            "FOLDER=$(Split-Path -Leaf (Get-Location))"
            "BRANCH=$(git branch --show-current 2>$null)"
            "TIME=$(Get-Date -Format 'HH:mm:ss')"
            """;

        private const string SeveralSh = """
            # One value travels as my last line. Several travel as NAME=value lines: the next step
            # gets everything I print in DEVDECK_PREVIOUS and picks out the ones it wants.
            echo "USER=$(whoami)"
            echo "FOLDER=$(basename "$(pwd)")"
            echo "BRANCH=$(git branch --show-current 2>/dev/null)"
            echo "TIME=$(date '+%H:%M:%S')"
            """;

        private const string ReadSeveralPs = """
            if (-not $env:DEVDECK_PREVIOUS_NAME) {
                'Nobody handed me anything. Run the "Example: several values" chain.'
                exit 0
            }

            # Every NAME=value line the step before me printed, into a table.
            $values = @{}

            foreach ($row in $env:DEVDECK_PREVIOUS -split "`r?`n") {
                if ($row -match '^(\w+)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
            }

            "From '$env:DEVDECK_PREVIOUS_NAME' I got $($values.Count) values:"
            ''
            "User   : $($values.USER)"
            "Folder : $($values.FOLDER)"
            "Branch : $(if ($values.BRANCH) { $values.BRANCH } else { '(not a git repository)' })"
            "Time   : $($values.TIME)"
            """;

        private const string ReadSeveralSh = """
            if [ -z "$DEVDECK_PREVIOUS_NAME" ]; then
              echo 'Nobody handed me anything. Run the "Example: several values" chain.'
              exit 0
            fi

            # Picks one NAME=value line out of everything the step before me printed.
            value() { echo "$DEVDECK_PREVIOUS" | sed -n "s/^$1=//p" | head -1; }

            branch="$(value BRANCH)"

            echo "From '$DEVDECK_PREVIOUS_NAME' I got:"
            echo
            echo "User   : $(value USER)"
            echo "Folder : $(value FOLDER)"
            echo "Branch : ${branch:-(not a git repository)}"
            echo "Time   : $(value TIME)"
            """;

        private const string FailPs = """
            param([int]$Code = 1)

            # Any exit code but 0 is a failure. A chain with "Stop at the first step that fails"
            # ticked ends here, and the steps after me never run. Set Code to 0 to let it through.
            "Exiting with $Code on purpose."
            exit $Code
            """;

        private const string FailSh = """
            code="${1:-1}"

            # Any exit code but 0 is a failure. A chain with "Stop at the first step that fails"
            # ticked ends here, and the steps after me never run. Set Code to 0 to let it through.
            echo "Exiting with $code on purpose."
            exit "$code"
            """;

        #endregion

        private static CustomCommand Script(
            string name, string body, bool windows, params (string Name, string Value)[] parameters) => new()
            {
                Name = name,
                Command = body,
                Kind = windows ? CommandKind.PowerShell : CommandKind.Bash,
                Arguments = [.. parameters.Select(p => new CommandArgument { Name = p.Name, Value = p.Value })],
            };
    }
}
