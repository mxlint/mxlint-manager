using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MxLintManager.Stores
{
    internal sealed class ExtensionManifestStore
    {
        private const string ManifestFileName = ".mxlint.json";

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public async Task WriteAsync(string extensionDirectory, ExtensionManifest manifest, CancellationToken ct = default)
        {
            Directory.CreateDirectory(extensionDirectory);
            var path = Path.Combine(extensionDirectory, ManifestFileName);

            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
                await JsonSerializer.SerializeAsync(stream, manifest, Options, ct);
            File.Move(tempPath, path, overwrite: true);
        }

        public ExtensionManifest? Read(string extensionDirectory)
        {
            var path = Path.Combine(extensionDirectory, ManifestFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ExtensionManifest>(json, Options);
            }
            catch (JsonException) { return null; }
            catch (IOException) { return null; }
        }
    }
}
