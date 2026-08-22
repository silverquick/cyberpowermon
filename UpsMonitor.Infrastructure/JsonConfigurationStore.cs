using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpsMonitor.Infrastructure;

public sealed class JsonConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly AppPaths _paths;

    public JsonConfigurationStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.ConfigurationFile))
        {
            var defaults = new AppConfiguration();
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        await using var stream = new FileStream(
            _paths.ConfigurationFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false)
            ?? new AppConfiguration();
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.SharedDirectory);
        var temporaryFile = _paths.ConfigurationFile + ".tmp";
        await using (var stream = new FileStream(
            temporaryFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryFile, _paths.ConfigurationFile, overwrite: true);
    }
}
