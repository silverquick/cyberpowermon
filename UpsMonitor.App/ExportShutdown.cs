namespace UpsMonitor.App;

internal static class ExportShutdown
{
    internal static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    internal static async Task WaitForWorkerAsync(Task? worker, TimeSpan timeout)
    {
        if (worker is null) return;
        try
        {
            // Only wait for the background worker, never its dispatcher-bound UI continuation.
            await worker.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A stalled disk may exceed the shutdown budget; its temp file can then remain.
        }
        catch (Exception)
        {
            // Cancellation/failure completes after the worker's finally has cleaned its temp file.
            // Normal UI execution reports export errors; shutdown must still dispose other services.
        }
    }
}
