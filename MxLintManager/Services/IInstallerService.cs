using MxLintManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace MxLintManager.Services
{
    internal interface IInstallerService
    {
        public IEnumerable<StudioProInstallation> GetInstalledVersions(IStudioProMonitor monitor, MxLintInstallerService installer);
        public Task InstallAsync(StudioProInstallation studioPro, CancellationToken cancellationToken = default);
        public Task RemoveAsync(StudioProInstallation studioPro);
        public Task<List<GitHubRelease>> GetReleaseList(CancellationToken cancellationToken = default);

    }
}
