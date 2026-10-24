using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace PenBoard.Services;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\PenBoard.SingleInstance";
    private static readonly string PipeName = "PenBoard.Toggle." + Process.GetCurrentProcess().SessionId;

    private Mutex? _mutex;
    private bool _ownsMutex;
    private CancellationTokenSource? _cts;

    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(false, MutexName);
            try
            {
                _ownsMutex = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous process exited without releasing the mutex.
                _ownsMutex = true;
            }

            if (!_ownsMutex)
            {
                _mutex.Dispose();
                _mutex = null;
            }
            return _ownsMutex;
        }
        catch (Exception ex)
        {
            Logger.Log("SingleInstance.TryAcquire: " + ex);
            return false;
        }
    }

    public void StartServer(Action<string> onCommand)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8, false, 256, leaveOpen: false);
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        onCommand(line.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    Logger.Log("Pipe server: " + ex.Message);
                    try
                    {
                        await Task.Delay(150, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }, token);
    }

    public static bool SendCommand(string command, int attempts = 12)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.None);
                client.Connect(250);
                using var writer = new StreamWriter(client, Encoding.UTF8, 256, leaveOpen: false)
                {
                    AutoFlush = true
                };
                writer.WriteLine(command);
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Logger.Log("SendCommand: " + ex.Message);
                Thread.Sleep(100);
            }
        }

        return false;
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch
        {
            // ignore
        }

        try
        {
            if (_ownsMutex && _mutex is not null)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _mutex?.Dispose();
            _mutex = null;
            _ownsMutex = false;
        }
    }
}
