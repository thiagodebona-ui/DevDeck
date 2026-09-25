using System.IO.Pipes;
using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  Keeps one copy of the app running, and gives later copies a way to talk to it.
    /// </summary>
    /// <remarks>
    ///  Two things at once, because they are the same thing. A deck is a window you leave open, so
    ///  a second one started by accident from a launcher is a nuisance: two windows editing one
    ///  settings file, last save wins. And a link handed over by the desktop arrives as a fresh
    ///  process, which is useless unless it can reach the copy that is already running.
    ///
    ///  A named pipe does both: whoever manages to create it is the app, and anyone who finds it
    ///  already there is a messenger. On Unix .NET backs these with a socket file under the
    ///  temporary folder, so this needs no per-platform branch.
    ///
    ///  The name carries the user's name, because two people on one machine are two users of one
    ///  app and neither one's window should answer the other's links.
    ///
    ///  There is a race here, between failing to connect and managing to listen, and it is left in
    ///  deliberately: closing it needs a second lock whose own lifetime then has to be managed, and
    ///  the cost of losing the race is that two windows open on the same second - which is what
    ///  happens today anyway.
    /// </remarks>
    internal static class SingleInstance
    {
        /// <summary>How long a messenger waits before deciding nobody is listening.</summary>
        /// <remarks>
        ///  Short, because this is on the path of every start: a slow check would make the ordinary
        ///  case - no other copy running - feel like the app hanging before it draws anything.
        /// </remarks>
        private const int ConnectMilliseconds = 400;

        private static NamedPipeServerStream? server;

        private static CancellationTokenSource? stopping;

        public static string Name { get; } = $"devdeck-{Environment.UserName}";

        /// <summary>
        ///  Hands a request to the copy that is already running.
        /// </summary>
        /// <returns>True when it was delivered, so this process should exit without a window.</returns>
        public static bool Handoff(string payload)
        {
            try
            {
                using NamedPipeClientStream client = new(".", Name, PipeDirection.Out);

                client.Connect(ConnectMilliseconds);

                byte[] bytes = Encoding.UTF8.GetBytes(payload);

                client.Write(bytes, 0, bytes.Length);
                client.Flush();

                return true;
            }
            catch (Exception)
            {
                // Nobody listening, or the pipe belongs to something that is no longer alive. Either
                // way this process is the app now - which is not a failure worth reporting.
                return false;
            }
        }

        /// <summary>
        ///  Starts listening for requests from later copies.
        /// </summary>
        /// <remarks>
        ///  One connection at a time and one message per connection. A messenger writes, closes and
        ///  exits; there is no conversation to keep open, and a single instance means a single
        ///  waiting reader is never a queue.
        /// </remarks>
        public static void Listen(Action<string> arrived)
        {
            stopping = new CancellationTokenSource();

            _ = Task.Run(() => Loop(arrived, stopping.Token));
        }

        public static void Stop()
        {
            try
            {
                stopping?.Cancel();

                // Disposing the stream is what unblocks a wait that is currently parked in
                // WaitForConnectionAsync; cancelling the token alone would not.
                server?.Dispose();
            }
            catch (Exception)
            {
                // Shutdown. Nothing left that could act on this.
            }
        }

        private static async Task Loop(Action<string> arrived, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    server = new NamedPipeServerStream(
                        Name,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using StreamReader reader = new(server, Encoding.UTF8);

                    string payload = await reader.ReadToEndAsync(token);

                    if (payload.Length > 0)
                    {
                        arrived(payload.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    AppLog.Instance.Failure("link", "Could not listen for links", exception);

                    // A pipe that cannot be created will not start working on the next pass, and a
                    // tight retry loop would fill the log with the same line forever.
                    return;
                }
                finally
                {
                    server?.Dispose();
                    server = null;
                }
            }
        }
    }
}
