using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeViewer.Services;

/// <summary>
/// Ensures only a single instance of Code Viewer runs and passes files opened from
/// File Explorer context menus directly to the running window.
/// </summary>
public static class SingleInstanceService
{
    private static readonly string PipeName = $"CodeViewer_InstancePipe_{Environment.UserName}";
    private static Mutex? _mutex;
    private static Action<string[]>? _argsHandler;

    public static bool TryRegisterSingleInstance(string[] args)
    {
        try
        {
            var mutexName = $@"Local\CodeViewer_Mutex_{Environment.UserName}";
            _mutex = new Mutex(true, mutexName, out var isFirstInstance);

            if (isFirstInstance)
            {
                StartPipeServer();
                return true;
            }

            // A primary instance is already active: forward arguments to it and exit
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(600);
                using var writer = new StreamWriter(client, Encoding.UTF8);
                foreach (var arg in args)
                {
                    writer.WriteLine(arg);
                }
                writer.Flush();
                return false;
            }
            catch
            {
                // Fall back to opening a second instance if communication fails
                return true;
            }
        }
        catch
        {
            return true;
        }
    }

    public static void SetArgsHandler(Action<string[]> handler)
    {
        _argsHandler = handler;
    }

    private static void StartPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync().ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var receivedList = new List<string>();
                    string? line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            receivedList.Add(line);
                        }
                    }

                    if (receivedList.Count > 0 && _argsHandler != null)
                    {
                        _argsHandler(receivedList.ToArray());
                    }
                }
                catch
                {
                    // Ignore transient pipe disconnects and continue listening
                }
            }
        });
    }
}
