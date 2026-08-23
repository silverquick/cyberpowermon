using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UpsMonitor.Hid;

internal sealed class HidDeviceSession : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly IntPtr _preparsedData;
    private readonly HidDescriptor _descriptor;
    private readonly byte[] _featureReportIds;
    private readonly byte[] _featureReportBuffer;
    private readonly ConcurrentDictionary<long, string?> _indexedStringCache = new();
    private readonly ConcurrentDictionary<byte, byte[]> _latestInputReports = new();
    private readonly CancellationTokenSource _readCancellation = new();
    private readonly FileStream? _inputStream;
    private readonly Task? _inputTask;
    private Exception? _inputError;
    private bool _disposed;

    private HidDeviceSession(
        SafeFileHandle handle,
        IntPtr preparsedData,
        HidDescriptor descriptor,
        bool canReadInput)
    {
        _handle = handle;
        _preparsedData = preparsedData;
        _descriptor = descriptor;
        _featureReportIds = descriptor.Capabilities
            .Where(item => item.ReportKind == HidReportKind.Feature)
            .Select(item => item.ReportId)
            .Distinct()
            .ToArray();
        _featureReportBuffer = descriptor.FeatureReportByteLength > 0
            ? new byte[descriptor.FeatureReportByteLength]
            : [];

        if (canReadInput && descriptor.InputReportByteLength > 0)
        {
            _inputStream = new FileStream(
                handle,
                FileAccess.Read,
                Math.Max(1, (int)descriptor.InputReportByteLength),
                isAsync: true);
            _inputTask = Task.Run(ReadInputLoopAsync);
        }
    }

    internal HidDescriptor Descriptor => _descriptor;

    internal static HidDeviceSession Open(HidDeviceCandidate candidate)
    {
        var accessModes = new[]
        {
            HidNative.GenericRead | HidNative.GenericWrite,
            HidNative.GenericRead,
            0u,
        };

        foreach (var access in accessModes)
        {
            var handle = HidDeviceEnumerator.Open(
                candidate.Device.DevicePath,
                access,
                HidNative.FileAttributeNormal | HidNative.FileFlagOverlapped);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                continue;
            }

            if (!HidNative.HidD_GetPreparsedData(handle, out var preparsedData))
            {
                handle.Dispose();
                continue;
            }

            try
            {
                var descriptor = HidDescriptorReader.Read(preparsedData);
                return new HidDeviceSession(handle, preparsedData, descriptor, (access & HidNative.GenericRead) != 0);
            }
            catch
            {
                HidNative.HidD_FreePreparsedData(preparsedData);
                handle.Dispose();
                throw;
            }
        }

        throw new IOException(
            $"Unable to open HID device {candidate.Device.DisplayName}.",
            new Win32Exception(Marshal.GetLastWin32Error()));
    }

    internal IReadOnlyList<HidDataValue> ReadValues()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureDeviceIsPresent();

        var values = new Dictionary<HidCapability, HidDataValue>();
        var featureReadSucceeded = false;
        if (_descriptor.FeatureReportByteLength > 0 && _featureReportBuffer.Length > 0)
        {
            foreach (var reportId in _featureReportIds)
            {
                Array.Clear(_featureReportBuffer, 0, _featureReportBuffer.Length);
                _featureReportBuffer[0] = reportId;
                if (!HidNative.HidD_GetFeature(_handle, _featureReportBuffer, _featureReportBuffer.Length))
                {
                    continue;
                }

                featureReadSucceeded = true;
                Merge(HidReportParser.Parse(_preparsedData, _descriptor, HidReportKind.Feature, _featureReportBuffer));
            }
        }

        foreach (var report in _latestInputReports.Values)
        {
            Merge(HidReportParser.Parse(_preparsedData, _descriptor, HidReportKind.Input, report));
        }

        if (_inputError is not null && !featureReadSucceeded)
        {
            throw new IOException("HID input report reading failed.", _inputError);
        }

        return values.Values.Select(ResolveIndexedString).ToArray();

        void Merge(IEnumerable<HidDataValue> source)
        {
            foreach (var value in source)
            {
                values[value.Capability] = value;
            }
        }
    }

    private HidDataValue ResolveIndexedString(HidDataValue value)
    {
        if (!HidUsageCatalog.IsStringIndex(value.Capability.UsagePage, value.Capability.Usage)
            || value.RawValue is <= 0 or > byte.MaxValue)
        {
            return value;
        }

        var text = _indexedStringCache.GetOrAdd(value.RawValue, raw =>
        {
            var buffer = new StringBuilder(256);
            return HidNative.HidD_GetIndexedString(
                _handle,
                checked((uint)raw),
                buffer,
                buffer.Capacity * sizeof(char))
                ? buffer.ToString().TrimEnd('\0')
                : null;
        });

        return text is not null ? value with { TextValue = text } : value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _readCancellation.Cancel();
        _inputStream?.Dispose();

        if (_inputTask is not null)
        {
            try
            {
                _inputTask.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch
            {
            }
        }

        HidNative.HidD_FreePreparsedData(_preparsedData);
        _handle.Dispose();
        _readCancellation.Dispose();
    }

    private async Task ReadInputLoopAsync()
    {
        if (_inputStream is null)
        {
            return;
        }

        try
        {
            while (!_readCancellation.IsCancellationRequested)
            {
                var report = new byte[_descriptor.InputReportByteLength];
                var bytesRead = await _inputStream.ReadAsync(report, _readCancellation.Token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new IOException("The HID device closed the input stream.");
                }

                _latestInputReports[report[0]] = report;
            }
        }
        catch (OperationCanceledException) when (_readCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_readCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _inputError = exception;
        }
    }

    private void EnsureDeviceIsPresent()
    {
        var attributes = new HidNative.HiddAttributes { Size = Marshal.SizeOf<HidNative.HiddAttributes>() };
        if (!HidNative.HidD_GetAttributes(_handle, ref attributes))
        {
            throw new IOException("The HID device is no longer available.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }
}
