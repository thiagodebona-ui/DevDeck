namespace DevDeck.Core
{
    /// <summary>
    ///  The folder one chain run keeps its steps' output in, for the steps after them to read.
    /// </summary>
    /// <remarks>
    ///  See <see cref="RunContext.Outputs"/> for why this exists. One folder per run, never shared:
    ///  two chains running at once, or the same chain started twice, must not read each other's
    ///  step2.txt.
    ///
    ///  Deleted when the run ends. The output is already in the chain's own log on screen, so what
    ///  is here is only a copy for the scripts, and a copy nobody cleans up is a temp folder that
    ///  grows with every run for as long as the app is used.
    ///
    ///  Never throws. A step that cannot have its output saved has still run; the chain carries on
    ///  without the file, and the later step that wanted it finds it missing and says so.
    /// </remarks>
    internal sealed class ChainOutputs : IDisposable
    {
        private ChainOutputs(string folder) => Folder = folder;

        /// <summary>The folder, as handed to each step in <see cref="RunContext.Outputs"/>.</summary>
        public string Folder { get; }

        /// <summary>Makes a fresh folder for one run, or null when the temp folder cannot be written.</summary>
        public static ChainOutputs? Create(string? root = null)
        {
            try
            {
                string folder = Path.Combine(
                    root ?? Path.Combine(Path.GetTempPath(), "DevDeck", "chains"),
                    $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24]);

                Directory.CreateDirectory(folder);

                return new ChainOutputs(folder);
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("chain", "Could not make a folder for the steps' output", exception);

                return null;
            }
        }

        /// <summary>Where step <paramref name="step"/>'s output goes, counting from one.</summary>
        public string PathFor(int step) => Path.Combine(Folder, $"step{step}.txt");

        /// <summary>Saves what a step printed.</summary>
        public void Write(int step, string output)
        {
            try
            {
                File.WriteAllText(PathFor(step), output);
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("chain", $"Could not save the output of step {step}", exception);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (Exception)
            {
                // A step left running with a file open, or an antivirus holding one. The folder is
                // under the temp directory, which the system clears in its own time.
            }
        }
    }
}
