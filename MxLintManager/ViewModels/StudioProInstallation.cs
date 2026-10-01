using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MxLintManager.Enums;
using MxLintManager.Helpers;
using MxLintManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MxLintManager.ViewModels
{
    public partial class StudioProInstallation(MxLintInstallerService installer) : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionText))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
        private bool isInstalled;

        [ObservableProperty]
        private bool isUpgradable;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
        private bool isRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionText))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        private string? installedVersion;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionText))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        private GitHubRelease? selectedRelease;

        private readonly MxLintInstallerService _installer = installer;
        public string StudioProVersion { get; set; } = string.Empty;
        public string InstallationPath { get; set; } = string.Empty;
        public string ExtensionPath { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;

        // < 0: selected is newer (upgrade), > 0: selected is older (downgrade), 0: same
        private int CompareInstalledToSelected() =>
            VersionHelper.Compare(InstalledVersion ?? string.Empty, SelectedRelease?.TagName ?? string.Empty);

        public string ActionText
        {
            get
            {
                if (!IsInstalled) return "Install";
                if (SelectedRelease is null) return "Installed";

                return CompareInstalledToSelected() switch
                {
                    < 0 => "Upgrade",
                    > 0 => "Downgrade",
                    _ => "Installed"
                };
            }
        }

        [RelayCommand(CanExecute = nameof(CanInstall))]
        private async Task InstallAsync()
        {
            await _installer.InstallAsync(this);
            IsInstalled = true;
            InstalledVersion = SelectedRelease?.TagName;
        }

        private bool CanInstall() =>
            !IsRunning
            && SelectedRelease is not null
            && (!IsInstalled || CompareInstalledToSelected() != 0);

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private async Task RemoveAsync()
        {
            await _installer.RemoveAsync(this);
            IsInstalled = false;
            InstalledVersion = "Unknown";
        }

        private bool CanRemove() => !IsRunning && IsInstalled;
    }
}
