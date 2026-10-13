namespace DevDeck.Core
{
    /// <summary>
    ///  The commands, chains and watch a brand new install starts with.
    /// </summary>
    /// <remarks>
    ///  A first run used to open on an empty deck and the words "No commands yet", which tells you
    ///  nothing about what a command is for. These are the answer - and there are few of them on
    ///  purpose. An earlier deck shipped two dozen, half of them toy examples, and a new user had
    ///  to delete most of it before the deck was theirs.
    ///
    ///  The bar each one had to clear: it answers a question a developer actually asks, it is worth
    ///  a button rather than being quicker to type, and it means something when run against a
    ///  project folder. Between them they also show every feature a command can use - a parameter
    ///  row, a value asked for at run time, a secret, a chain handing on a value, a file watch - so
    ///  there is a working example of each without a single command that exists only to be one.
    ///
    ///  All JavaScript, run by Node.js, so one body runs the same on all three platforms. The deck
    ///  used to carry a PowerShell and a shell copy of every script, and the two drifted. Only
    ///  Node's built-in modules are used, so there is nothing to install.
    ///
    ///  Nothing here writes, deletes, installs or pushes. A command that shipped with the app and
    ///  changed a repository the first time it was clicked would be indefensible, so the ones about
    ///  cleanup report rather than act, and leave the decision where it belongs.
    /// </remarks>
    internal static class StarterCommands
    {
        /// <summary>The starter deck, in the order it should appear.</summary>
        public static List<CustomCommand> For() =>
        [
            // The orientation question: what this folder is, how to run it, where git says you are.
            Make("Project overview", OverviewJs),

            // Nobody remembers that two dots is commits and three dots is the diff, which is
            // exactly why this is worth a button. Finds the base branch rather than assuming.
            Make("What changed vs main", ChangedVsMainJs),

            // The parameter-row example: the words to look for are a row under the body.
            Make("Find TODOs", TodosJs, ("Words", "TODO|FIXME|HACK")),

            // The asked-at-run-time example: "port already in use" is a daily occurrence, and the
            // answer is buried in a different tool on every platform.
            Make("Who is using a port", PortJs),

            // The folders that are safe to delete and expensive to keep. Reports only.
            Make("Heavy build folders", BuildFoldersJs),

            // The chain example, and a real pipeline: step one finds the last release tag and
            // hands it on, step two turns everything since it into release notes.
            Make("Last release tag", LastTagJs),
            Make("Release notes since the tag", ReleaseNotesJs),

            // The file watch example. Run by hand it says what a watch would hand it.
            Make("What changed just now", ChangedNowJs),

            // The secrets example: the vault is seeded with one, and this prints it, so
            // {{secret:NAME}} can be seen working before anyone trusts it with a real token.
            SecretExample(),
        ];

        /// <summary>The name of the secret the vault is seeded with.</summary>
        public const string SecretExampleName = "secretExample";

        /// <summary>The command that prints it.</summary>
        public const string SecretExampleCommand = "Show the example secret";

        /// <summary>What the example secret holds: the app's name and version.</summary>
        public static string SecretExampleValue => $"DevDeck {AppVersion.Number}";

        public static CustomCommand SecretExample() => Make(SecretExampleCommand, SecretExampleJs);

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

        /// <summary>
        ///  The chains a fresh install starts with.
        /// </summary>
        /// <remarks>
        ///  Two, built only from starter commands so neither is broken on the first run, and
        ///  chosen to show the two things a chain is for: running several questions in one go, and
        ///  passing a value from one step to the next.
        /// </remarks>
        public static List<CommandChain> Chains() =>
        [
            // Three independent questions, cheapest first. Off: a folder with no main branch
            // should not stop the other two from answering.
            new()
            {
                Name = "Morning check",
                Steps = ["Project overview", "What changed vs main", "Find TODOs"],
                StopOnFailure = false,
            },

            // A genuine pipeline, which is why it stops on failure: step two handed no tag would
            // write release notes for the whole history, rather than say that step one failed.
            new()
            {
                Name = "Release notes",
                Steps = ["Last release tag", "Release notes since the tag"],
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
        // Each script is whole on its own, small git helper included, because each one is also
        // there to be read and copied - and one that leans on a shared file cannot be.

        private const string OverviewJs = """
            // What this folder is, how to run it, and where git says you are - the questions
            // to answer before touching a project you have not opened in a while.
            const fs = require('fs');
            const path = require('path');
            const { execFileSync } = require('child_process');

            function git(...args) {
              try {
                return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
              } catch {
                return null;
              }
            }

            const here = process.cwd();
            console.log(`Folder   ${here}`);

            if (fs.existsSync('package.json')) {
              const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
              const manager = fs.existsSync('pnpm-lock.yaml') ? 'pnpm' : fs.existsSync('yarn.lock') ? 'yarn' : 'npm';

              console.log(`Project  ${pkg.name ?? path.basename(here)} ${pkg.version ?? ''} (Node, ${manager})`);

              const scripts = Object.entries(pkg.scripts ?? {});

              if (scripts.length > 0) {
                console.log('Scripts');

                for (const [name, line] of scripts) {
                  console.log(`  ${manager} run ${name.padEnd(14)} ${line}`);
                }
              }
            } else {
              const projects = fs.readdirSync(here).filter(file =>
                /\.(sln|slnx|csproj|fsproj)$|^(Cargo\.toml|go\.mod|pyproject\.toml|requirements\.txt|pom\.xml|build\.gradle(\.kts)?|Makefile)$/.test(file));

              console.log(`Project  ${projects.length > 0 ? projects.join(', ') : 'no project file in this folder'}`);
            }

            const branch = git('branch', '--show-current');

            if (branch === null) {
              console.log('Git      not a repository');
              process.exit(0);
            }

            const counts = git('rev-list', '--left-right', '--count', '@{upstream}...HEAD');
            const [behind, ahead] = counts ? counts.split(/\s+/) : [];
            const dirty = (git('status', '--porcelain') ?? '').split('\n').filter(Boolean).length;

            console.log(`Branch   ${branch || '(detached)'} - ${counts ? `${ahead} ahead, ${behind} behind upstream` : 'no upstream'}`);
            console.log(`Changes  ${dirty > 0 ? `${dirty} uncommitted file(s)` : 'working tree clean'}`);
            console.log(`Last     ${git('log', '-1', '--format=%h %s (%cr, %an)') ?? 'no commits yet'}`);
            """;

        private const string ChangedVsMainJs = """
            // Your branch against its base: the commits only it has, and the files they change.
            const { execFileSync } = require('child_process');

            function git(...args) {
              try {
                return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
              } catch {
                return null;
              }
            }

            if (git('rev-parse', '--is-inside-work-tree') === null) {
              console.log('Not a git repository. Choose a workspace that is one.');
              process.exit(0);
            }

            const base = ['main', 'master', 'develop', 'origin/main', 'origin/master']
              .find(name => git('rev-parse', '--verify', '--quiet', name) !== null);

            if (!base) {
              console.log('No main, master or develop branch here.');
              process.exit(0);
            }

            console.log(`Comparing ${git('branch', '--show-current') || 'HEAD'} against ${base}.`);
            console.log('');
            console.log('Commits on this branch:');
            console.log(git('log', '--oneline', `${base}..HEAD`) || '  none');
            console.log('');
            console.log('Files changed:');
            console.log(git('diff', '--stat', `${base}...HEAD`) || '  none');
            """;

        /// <remarks>
        ///  Capped, and deliberately: a large checkout can have thousands of hits, and the output
        ///  panel has to keep every line it is given. The head of the list answers the question;
        ///  the count at the end says what was left out.
        /// </remarks>
        private const string TodosJs = """
            // The words to look for are the Words parameter under this script, which arrives
            // as process.argv[2]. Change it to look for anything else - it is a regular expression.
            const fs = require('fs');
            const path = require('path');

            const words = new RegExp(`\\b(${process.argv[2] || 'TODO|FIXME|HACK'})\\b`);
            const skip = new Set(['.git', 'node_modules', 'bin', 'obj', 'dist', 'build', 'target', '.next', '.venv', 'coverage']);
            const most = 200;
            const hits = [];

            function search(folder) {
              let entries;

              try {
                entries = fs.readdirSync(folder, { withFileTypes: true });
              } catch {
                return;
              }

              for (const entry of entries) {
                const full = path.join(folder, entry.name);

                if (entry.isDirectory()) {
                  if (!skip.has(entry.name)) search(full);
                  continue;
                }

                // Text files only: anything over a megabyte, or with a NUL in it, is not source.
                if (!entry.isFile() || fs.statSync(full).size > 1024 * 1024) continue;

                const text = fs.readFileSync(full, 'utf8');
                if (text.includes('\0')) continue;

                text.split('\n').forEach((line, index) => {
                  if (words.test(line)) {
                    hits.push(`${path.relative('.', full)}:${index + 1}  ${line.trim().slice(0, 160)}`);
                  }
                });
              }
            }

            search('.');

            for (const hit of hits.slice(0, most)) console.log(hit);

            if (hits.length === 0) {
              console.log('None found.');
            } else if (hits.length > most) {
              console.log(`\nShowing ${most} of ${hits.length}. Narrow the Words, or run it in a subfolder.`);
            } else {
              console.log(`\n${hits.length} found.`);
            }
            """;

        /// <remarks>
        ///  Reports and stops there: ending the process is one click on the Running page, which
        ///  shows what it is first. Listening sockets are recognised by their foreign address of
        ///  port 0 rather than by netstat's "LISTENING", which Windows translates.
        /// </remarks>
        private const string PortJs = """
            // The port is asked for each time you press Run, because it is a different one
            // every time. To end whatever holds it, use the Stop on the Running page.
            const { execFileSync } = require('child_process');

            const port = '{{Port:3000}}'.trim();

            if (!/^\d+$/.test(port)) {
              console.log(`"${port}" is not a port number.`);
              process.exit(1);
            }

            function run(file, args) {
              try {
                return execFileSync(file, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
              } catch {
                return '';
              }
            }

            const owners = new Map();

            if (process.platform === 'win32') {
              for (const line of run('netstat', ['-ano']).split('\n')) {
                const [protocol, local, remote, , pid] = line.trim().split(/\s+/);

                if (protocol === 'TCP' && local?.endsWith(`:${port}`) && remote?.endsWith(':0') && !owners.has(pid)) {
                  const task = run('tasklist', ['/FI', `PID eq ${pid}`, '/FO', 'CSV', '/NH']);
                  owners.set(pid, { name: task.split(',')[0].replace(/"/g, '').trim() || 'unknown', address: local });
                }
              }
            } else {
              let pid = null;

              for (const line of run('lsof', ['-nP', `-iTCP:${port}`, '-sTCP:LISTEN', '-Fpcn']).split('\n')) {
                if (line.startsWith('p')) pid = line.slice(1);
                if (line.startsWith('c') && pid) owners.set(pid, { name: line.slice(1), address: '' });
                if (line.startsWith('n') && owners.has(pid)) owners.get(pid).address ||= line.slice(1);
              }
            }

            if (owners.size === 0) {
              console.log(`Port ${port} is free.`);
            }

            for (const [pid, owner] of owners) {
              console.log(`Port ${port} is used by ${owner.name} (pid ${pid}), listening on ${owner.address}`);
            }
            """;

        private const string BuildFoldersJs = """
            // The folders that are safe to delete and expensive to keep, biggest first, looking
            // four levels down. It only reports: what to delete is your call.
            const fs = require('fs');
            const path = require('path');

            const names = new Set(['node_modules', 'bin', 'obj', 'dist', 'build', 'target', '.next', '.venv', '__pycache__', 'coverage']);
            const found = [];

            function size(folder) {
              let total = 0;

              try {
                for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
                  const full = path.join(folder, entry.name);

                  if (entry.isDirectory()) total += size(full);
                  else if (entry.isFile()) total += fs.statSync(full).size;
                }
              } catch {
                // A folder we may not read, or a link to nowhere: count what we could.
              }

              return total;
            }

            function look(folder, depth) {
              let entries;

              try {
                entries = fs.readdirSync(folder, { withFileTypes: true });
              } catch {
                return;
              }

              for (const entry of entries) {
                if (!entry.isDirectory() || entry.name === '.git') continue;

                const full = path.join(folder, entry.name);

                if (names.has(entry.name)) found.push({ path: path.relative('.', full), bytes: size(full) });
                else if (depth < 4) look(full, depth + 1);
              }
            }

            look('.', 1);

            const megabytes = bytes => `${(bytes / 1024 / 1024).toFixed(1).padStart(9)} MB`;

            found.sort((a, b) => b.bytes - a.bytes);

            for (const folder of found.slice(0, 20)) console.log(`${megabytes(folder.bytes)}  ${folder.path}`);

            if (found.length === 0) {
              console.log('No build folders here.');
            } else {
              console.log(`\n${megabytes(found.reduce((sum, folder) => sum + folder.bytes, 0))}  in ${found.length} folders`);
            }
            """;

        /// <summary>
        ///  Step one of the release notes chain: produces the one value the next step needs.
        /// </summary>
        /// <remarks>
        ///  The tag goes on its own final line deliberately. A step is read by the next one through
        ///  DEVDECK_PREVIOUS_LINE - the last line with anything on it - so a script meant to feed
        ///  another ends by printing one value and nothing else, and everything it wants to say to
        ///  the human goes above that.
        /// </remarks>
        private const string LastTagJs = """
            // Step one of the "Release notes" chain. It finds the last release tag and prints
            // it alone on its last line, which is the line the next step is handed.
            const { execFileSync } = require('child_process');

            function git(...args) {
              try {
                return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
              } catch {
                return null;
              }
            }

            if (git('rev-parse', '--is-inside-work-tree') === null) {
              console.log('Not a git repository. Choose a workspace that is one.');
              process.exit(1);
            }

            const tag = git('describe', '--tags', '--abbrev=0');

            if (tag) {
              console.log(`The last release is ${tag}, tagged ${git('log', '-1', '--format=%cr', tag)}.`);
              console.log(tag);
            } else {
              const first = git('rev-list', '--max-parents=0', 'HEAD')?.split('\n')[0];

              if (!first) {
                console.log('No commits yet, so there is nothing to release.');
                process.exit(1);
              }

              console.log('No tags yet, so everything since the first commit counts.');
              console.log(first);
            }
            """;

        /// <summary>
        ///  Step two: consumes step one's value.
        /// </summary>
        /// <remarks>
        ///  Written so it still does its job when run on its own from the deck, which is how anyone
        ///  will first meet it. A step that only works inside its chain teaches nothing about what
        ///  the chain is doing.
        /// </remarks>
        private const string ReleaseNotesJs = """
            // Step two of the "Release notes" chain. In the chain, the tag arrives from step one
            // in DEVDECK_PREVIOUS_LINE; run on its own, it finds the tag itself. The commits are
            // grouped by their conventional prefix (feat:, fix:), ready to paste into a changelog.
            const { execFileSync } = require('child_process');

            function git(...args) {
              try {
                return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
              } catch {
                return null;
              }
            }

            const from = process.env.DEVDECK_PREVIOUS_NAME;
            const since = from ? process.env.DEVDECK_PREVIOUS_LINE : git('describe', '--tags', '--abbrev=0');
            const log = git('log', '--no-merges', '--format=%s', since ? `${since}..HEAD` : 'HEAD');

            if (log === null) {
              console.log(`Could not read the history since ${since ?? 'the first commit'}.`);
              process.exit(1);
            }

            const groups = { Features: [], Fixes: [], Other: [] };

            for (const subject of log.split('\n').filter(Boolean)) {
              const match = subject.match(/^(\w+)(\([^)]*\))?!?:\s*(.+)$/);
              const type = match?.[1].toLowerCase();

              if (type === 'feat') groups.Features.push(match[3]);
              else if (type === 'fix') groups.Fixes.push(match[3]);
              else groups.Other.push(subject);
            }

            console.log(`Since ${since ?? 'the first commit'}${from ? `, handed on by "${from}"` : ''}:`);
            console.log('');

            let any = false;

            for (const [title, items] of Object.entries(groups)) {
              if (items.length === 0) continue;

              any = true;
              console.log(`## ${title}`);
              for (const item of items) console.log(`- ${item}`);
              console.log('');
            }

            if (!any) console.log('Nothing new since then.');
            """;

        // Prints the run context rather than doing anything with it - which is the point. Someone
        // writing their own watch command needs to know what they are given and what it looks
        // like, and the fastest way to learn that is to run this once and read the output.
        private const string ChangedNowJs = """
            // What a file watch hands its command, printed so you can see what to read when
            // you write your own. Turn on "Show me what changed" on the Automation page and
            // save a file to see it.
            const env = process.env;

            if (env.DEVDECK_TRIGGER !== 'watch') {
              console.log('Started by hand, so there is nothing that changed to report.');
              console.log('Turn on the "Show me what changed" watch and save a file to see the rest.');
              process.exit(0);
            }

            const fields = [
              ['Watch', 'DEVDECK_WATCH_NAME'],
              ['Folder', 'DEVDECK_WATCH_FOLDER'],
              ['What', 'DEVDECK_CHANGED_KIND'],
              ['When', 'DEVDECK_CHANGED_AT'],
              ['File', 'DEVDECK_CHANGED_FILE'],
              ['Count', 'DEVDECK_CHANGED_COUNT'],
            ];

            for (const [label, name] of fields) console.log(`${label.padEnd(7)} ${env[name] ?? ''}`);

            // One path per line: a path may hold a space or a semicolon, but never a newline.
            console.log('');
            console.log('All of them:');

            for (const file of (env.DEVDECK_CHANGED_FILES ?? '').split('\n').filter(Boolean)) {
              console.log(`  ${file}`);
            }
            """;

        private const string SecretExampleJs = """
            // The worked example for secrets and environment variables.
            //
            // {{secret:secretExample}} is filled in from the vault when this runs, so the value is
            // never stored in the command or in settings.json. appName and appVersion are ordinary
            // environment variables, set on every command from Settings > Environment variables.
            console.log('Secret name  : secretExample');
            console.log('Secret value : {{secret:secretExample}}');
            console.log(`appName      : ${process.env.appName}`);
            console.log(`appVersion   : ${process.env.appVersion}`);
            """;
        #endregion

        private static CustomCommand Make(
            string name, string body, params (string Name, string Value)[] parameters) => new()
            {
                Name = name,
                Command = body,
                Kind = CommandKind.JavaScript,
                Arguments = [.. parameters.Select(p => new CommandArgument { Name = p.Name, Value = p.Value })],
            };
    }
}
