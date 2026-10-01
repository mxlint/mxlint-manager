using MxLintManager.Stores;
using MxLintManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxLintManager.Services
{
    public class MxLintInstallerService : IInstallerService
    {
        private readonly HttpClient _httpClient = new();
        private ExtensionManifestStore _manifestStore;

        private const string Owner = "mxlint";
        private const string Repository = "mxlint-extension";
        private readonly string _downloadDirectory = Path.Combine(AppContext.BaseDirectory, "downloads");

        public MxLintInstallerService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MxLintManager", "1.0"));
            _manifestStore = new ExtensionManifestStore();
        }

        public async Task InstallAsync(StudioProInstallation studioPro, CancellationToken cancellationToken = default)
        {
            if (studioPro == null || studioPro.SelectedRelease == null)
            {
                return;
            }

            string zipFIle = await DownloadReleaseAsync(studioPro.SelectedRelease.TagName, cancellationToken);

            string extensionDirectory = Path.Combine(studioPro.InstallationPath, "modeler", "extensions");

            Directory.CreateDirectory(extensionDirectory);

            ZipFile.ExtractToDirectory(zipFIle, extensionDirectory, overwriteFiles: true);

            await _manifestStore.WriteAsync(Path.Combine(extensionDirectory, "MxLintExtension"), new ExtensionManifest
            {
                Version = studioPro.SelectedRelease.TagName,
                InstalledUtc = DateTime.UtcNow,
                SourceTag = studioPro.SelectedRelease.TagName,
            });
        }

        public Task RemoveAsync(StudioProInstallation studioPro)
        {
            string extensionDirectory = Path.Combine(studioPro.InstallationPath, "modeler", "extensions", "MxLintExtension");

            if (Directory.Exists(extensionDirectory))
            {
                Directory.Delete(extensionDirectory, recursive: true);
            }

            return Task.CompletedTask;
        }

        public async Task<List<GitHubRelease>> GetReleaseList(CancellationToken cancellationToken = default)
        {
            string apiUrl = $"https://api.github.com/repos/{Owner}/{Repository}/releases";

            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            List<GitHubRelease>? release = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, cancellationToken: cancellationToken);

            if (release == null)
            {
                throw new InsufficientExecutionStackException("Unable to parse Github releases.");
            }

            return release;
        }

        private async Task<string> DownloadReleaseAsync(string version, CancellationToken cancellationToken = default)
        {
            GitHubReleaseAsset asset;
            if (version == "latest")
            {
                asset = await GetLatestReleaseAssetAsync(cancellationToken);
            }
            else
            {
                asset = await GetReleaseAssetByTagAsync(version, cancellationToken);
            }


            Directory.CreateDirectory(_downloadDirectory);

            string zipFilePath = Path.Combine(_downloadDirectory, asset.Name);

            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var fileStream = File.Create(zipFilePath);

            await downloadStream.CopyToAsync(fileStream, cancellationToken);

            return zipFilePath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<GitHubReleaseAsset> GetLatestReleaseAssetAsync(CancellationToken cancellationToken)
        {
            string apiUrl = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";

            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);

            if (release == null)
            {
                throw new InvalidOperationException("Unable to parse GitHub release.");
            }

            GitHubReleaseAsset? zipAsset = release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (zipAsset == null)
            {
                throw new InvalidOperationException("No ZIP asset found in latest release.");
            }

            return zipAsset;
        }

        private async Task<GitHubReleaseAsset> GetReleaseAssetByTagAsync(string releaseTag, CancellationToken cancellationToken)
        {
            string apiUrl = $"https://api.github.com/repos/{Owner}/{Repository}/releases/tags/{releaseTag}";

            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);

            if (release == null)
            {
                throw new InvalidOperationException("Unable to parse GitHub release.");
            }

            GitHubReleaseAsset? gitHubReleaseAsset = release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (gitHubReleaseAsset == null)
            {
                throw new InvalidOperationException("No ZIP asset found in latest release.");
            }

            return gitHubReleaseAsset;
        }

        public IEnumerable<StudioProInstallation> GetInstalledVersions(IStudioProMonitor _studioProMonitor, MxLintInstallerService installer)
        {
            string root = @"C:\Program Files\Mendix";

            if (!Directory.Exists(root))
                yield break;

            foreach (string directory in Directory.GetDirectories(root))
            {
                string? version = Path.GetFileName(directory);
                if (!Version.TryParse(version, out _))
                    continue;

                bool isMxLintInstalled = false;
                bool isRunning = _studioProMonitor.IsVersionRunning(directory);
                string mxLintVersion = "Unknown";
                string extensionPath = Path.Combine(directory, "modeler", "extensions", "MxLintExtension");

                if (!Directory.Exists(extensionPath))
                {
                    extensionPath = "";
                    yield return new StudioProInstallation(installer)
                    {
                        StudioProVersion = version,
                        InstallationPath = directory,
                        IsInstalled = isMxLintInstalled,
                        IsRunning = isRunning,
                        InstalledVersion = mxLintVersion,
                        ExtensionPath = extensionPath,
                    };
                }
                else
                {

                    //string? dll = Directory.GetFiles(extensionPath, "MxLintExtension.dll", SearchOption.AllDirectories).FirstOrDefault();

                    //if (dll != null)
                    //{
                    //    var fileVersion = FileVersionInfo.GetVersionInfo(dll);

                    //    mxLintVersion = fileVersion.ProductVersion ?? "Unknown";
                    //}
                    ExtensionManifest? manifest = _manifestStore.Read(extensionPath);

                    mxLintVersion = manifest == null ? "Unknown" : manifest.Version;

                    isMxLintInstalled = true;

                    yield return new StudioProInstallation(installer)
                    {
                        StudioProVersion = version,
                        InstallationPath = directory,
                        IsInstalled = isMxLintInstalled,
                        IsRunning = isRunning,
                        InstalledVersion = mxLintVersion,
                        ExtensionPath = extensionPath,
                    };
                }
            }

        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    public class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

