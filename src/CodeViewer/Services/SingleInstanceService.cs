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
    private static readonly string SafeUserName = (Environment.UserName ?? "Default").Replace('\\', '_').Replace('/', '_').Replace(':', '_');
    private static string _customSuffix = string.Empty;

    public static string CustomSuffix
    {
        get => _customSuffix;
        set => _customSuffix = value;
    }

    private static string PipeName => string.IsNullOrEmpty(_customSuffix)
        ? $"CodeViewer_InstancePipe_{SafeUserName}"
        : $"CodeViewer_InstancePipe_{SafeUserName}_{_customSuffix}";

    private static string MutexName => string.IsNullOrEmpty(_customSuffix)
        ? $@"Local\CodeViewer_Mutex_{SafeUserName}"
        : $@"Local\CodeViewer_Mutex_{SafeUserName}_{_customSuffix}";

    private static Mutex? _mutex;
    private static Action<string[]>? _argsHandler;
    private static CancellationTokenSource? _serverCts;

    public const string ActivateToken = "__ACTIVATE__";

    public static bool TryRegisterSingleInstance(string[] args)
    {
        bool isFirstInstance = false;
        if (_mutex == null)
        {
            try
            {
                _mutex = new Mutex(true, MutexName, out isFirstInstance);
            }
            catch (AbandonedMutexException)
            {
                isFirstInstance = true;
            }
            catch
            {
                isFirstInstance = true;
            }

            if (isFirstInstance)
            {
                StartPipeServer();
                return true;
            }
            else
            {
                try { _mutex?.Dispose(); } catch { }
                _mutex = null;
            }
        }

        // Secondary instance: Forward arguments or activation signal to the primary instance
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            if (args.Length == 0)
            {
                writer.WriteLine(ActivateToken);
            }
            else
            {
                foreach (var arg in args)
                {
                    writer.WriteLine(arg);
                }
            }
            writer.Flush();
            return false;
        }
        catch
        {
            // Primary instance failed to respond / crashed. Fall back to opening this instance as primary.
            StartPipeServer();
            return true;
        }
    }

    public static void SetArgsHandler(Action<string[]> handler)
    {
        _argsHandler = handler;
    }

    public static void Stop()
    {
        try
        {
            _serverCts?.Cancel();
            _serverCts?.Dispose();
            _serverCts = null;

            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
                _mutex = null;
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static void StartPipeServer()
    {
        _serverCts?.Cancel();
        _serverCts = new CancellationTokenSource();
        var token = _serverCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var receivedList = new List<string>();
                    string? line;
                    while ((line = await reader.ReadLineAsync(token).ConfigureAwait(false)) != null)
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
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, token);
    }
}
