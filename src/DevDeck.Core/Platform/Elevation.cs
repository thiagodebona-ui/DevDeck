using System.Diagnostics;
using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>One step's outcome, as the elevated helper writes it down.</summary>
    /// <remarks>
    ///  Ints rather than enums on the wire. The helper is a second copy of this executable, so in
    ///  practice the two agree - but a file left behind by an older build must not fail to parse,
    ///  and an int that no longer maps to a step is easier to discard than a failed deserialise.
    /// </remarks>
    internal sealed record PrivilegedStepResult(int Step, int Status, string Detail);

    internal sealed record PrivilegedResult(List<PrivilegedStepResult> Steps);

    /// <summary>
    ///  Runs the administrator-only cleanup steps in a second, elevated copy of the app.
    /// </summary>
    /// <remarks>
    ///  Without this, the steps that actually move the number in Task Manager - purging the standby
    ///  cache above all - reported "needs administrator" and did nothing, unless the user happened
    ///  to have started the app elevated. V2 solved it by relaunching itself, and so does this.
    ///
    ///  One helper for all of the privileged steps, so a clean costs a single UAC prompt rather
    ///  than one per step. The helper cannot have its output redirected - "runas" is only honoured
    ///  with UseShellExecute, which rules redirection out - so it reports back two ways: an exit
    ///  code that is a bit mask of the steps that succeeded, and a small JSON file with the detail.
    ///  The mask is the fallback for when the file could not be written.
    ///
    ///  Windows only, and deliberately so. On Linux and macOS the privileged step is a cache drop
    ///  that needs root, and silently prompting for a root password is not something an app should
    ///  do on the user's behalf.
    /// </remarks>
    internal static class Elevation
    {
        /// <summary>The argument that turns this executable into the helper.</summary>
        public const string Switch = "--purge-memory";

        /// <summary>Returned when the user dismissed the prompt, which is not a failure.</summary>
        public const int Declined = -1;

        private const int ErrorCancelled = 1223;

        /// <summary>Whether asking for elevation is possible at all here.</summary>
        public static bool Available => OperatingSystem.IsWindows();

        /// <summary>The steps in <paramref name="mask"/>, as a bit per <see cref="MemoryStep"/>.</summary>
        public static int MaskOf(IEnumerable<MemoryStep> steps) =>
            steps.Aggregate(0, (value, step) => value | Bit(step));

        public static IReadOnlyList<MemoryStep> StepsIn(int mask) =>
            MemoryClean.Order.Where(step => (mask & Bit(step)) != 0).ToList();

        public static int Bit(MemoryStep step) => 1 << (int)step;

        /// <summary>
        ///  Asks an elevated copy of this executable to run <paramref name="steps"/>.
        /// </summary>
        /// <remarks>
        ///  Never throws: every failure - no prompt, a declined prompt, a helper that died - comes
        ///  back as outcomes against the steps that were asked for, because a clean is a list of
        ///  independent attempts and the caller wants to report all of them.
        /// </remarks>
        public static async Task<IReadOnlyList<StepOutcome>> RunAsync(
            IReadOnlyList<MemoryStep> steps, CancellationToken token)
        {
            if (steps.Count == 0)
            {
                return [];
            }

            if (!Available)
            {
                return All(steps, StepStatus.NeedsAdmin, "this platform has no way to ask for elevation");
            }

            int mask = MaskOf(steps);
            string resultPath = Path.Combine(
                Path.GetTempPath(), $"devdeck-purge-{Guid.NewGuid():N}.json");

            int done;

            try
            {
                done = await StartElevatedAsync(mask, resultPath, token);
            }
            catch (Exception exception)
            {
                Forget(resultPath);

                return All(steps, StepStatus.Failed, exception.Message);
            }

            if (done == Declined)
            {
                Forget(resultPath);

                return All(steps, StepStatus.NeedsAdmin, "the administrator prompt was declined");
            }

            // The helper leaves a per-step account behind; the exit code is the fallback for when
            // it could not write one.
            PrivilegedResult? detail = ReadResult(resultPath);
            Forget(resultPath);

            if (detail is { Steps.Count: > 0 })
            {
                return Translate(detail);
            }

            return
            [
                .. steps.Select(step => (done & Bit(step)) != 0
                    ? new StepOutcome(step, StepStatus.Done, "done, as administrator")
                    : new StepOutcome(step, StepStatus.Failed, "the elevated helper could not run this")),
            ];
        }

        /// <summary>
        ///  The elevated helper's whole job: run the steps in <paramref name="mask"/> and report
        ///  which of them worked.
        /// </summary>
        /// <remarks>
        ///  Called from Main with no UI in the process at all - this copy of the executable exists
        ///  for a fraction of a second and never opens a window.
        /// </remarks>
        public static int RunPrivilegedSteps(int mask, string? resultPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return 0;
            }

            int done = 0;
            List<PrivilegedStepResult> reported = [];

            foreach (MemoryStep step in StepsIn(mask))
            {
                try
                {
                    StepOutcome outcome = MemoryClean.RunOneElevated(step);

                    reported.Add(new PrivilegedStepResult((int)step, (int)outcome.Status, outcome.Detail));

                    if (outcome.Status == StepStatus.Done)
                    {
                        done |= Bit(step);
                    }
                }
                catch (Exception exception)
                {
                    // One step failing must not cost the others; the mask says what got through.
                    reported.Add(new PrivilegedStepResult((int)step, (int)StepStatus.Failed, exception.Message));
                }
            }

            if (resultPath is not null)
            {
                WriteResult(resultPath, new PrivilegedResult(reported));
            }

            return done;
        }

        /// <summary>Starts a second, elevated copy of this executable.</summary>
        private static async Task<int> StartElevatedAsync(int mask, string resultPath, CancellationToken token)
        {
            string? executable = Environment.ProcessPath;

            if (string.IsNullOrEmpty(executable))
            {
                return 0;
            }

            ProcessStartInfo info = new(executable, $"{Switch} {mask} \"{resultPath}\"")
            {
                // "runas" is only honoured with UseShellExecute set - which also means the helper's
                // output cannot be redirected, hence the exit code and the result file.
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            try
            {
                using Process? helper = Process.Start(info);

                if (helper is null)
                {
                    return 0;
                }

                await helper.WaitForExitAsync(token);

                return helper.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception exception)
                when (exception.NativeErrorCode == ErrorCancelled)
            {
                return Declined;
            }
        }

        private static IReadOnlyList<StepOutcome> Translate(PrivilegedResult result)
        {
            List<StepOutcome> outcomes = [];

            foreach (PrivilegedStepResult step in result.Steps)
            {
                if (!Enum.IsDefined(typeof(MemoryStep), step.Step))
                {
                    // Written by a build that had a step this one does not.
                    continue;
                }

                StepStatus status = Enum.IsDefined(typeof(StepStatus), step.Status)
                    ? (StepStatus)step.Status
                    : StepStatus.Failed;

                outcomes.Add(new StepOutcome((MemoryStep)step.Step, status, step.Detail));
            }

            return outcomes;
        }

        private static IReadOnlyList<StepOutcome> All(
            IEnumerable<MemoryStep> steps, StepStatus status, string detail) =>
            [.. steps.Select(step => new StepOutcome(step, status, detail))];

        private static void WriteResult(string path, PrivilegedResult result)
        {
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(result));
            }
            catch (Exception)
            {
                // The exit code still carries the mask, which is why it exists.
            }
        }

        private static PrivilegedResult? ReadResult(string path)
        {
            try
            {
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<PrivilegedResult>(File.ReadAllText(path))
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Forget(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // A temp file we could not remove is not worth telling anyone about.
            }
        }
    }
}
