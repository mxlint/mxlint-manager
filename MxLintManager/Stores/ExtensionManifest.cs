using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MxLintManager.Stores
{
    public sealed record ExtensionManifest
    {
        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("installedUtc")]
        public DateTimeOffset InstalledUtc { get; init; }

        [JsonPropertyName("sourceTag")]
        public string? SourceTag { get; init; }   // the GitHub release tag it came from, handy for updates
    }
}
