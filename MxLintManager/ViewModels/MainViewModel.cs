using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MxLintManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace MxLintManager.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IStudioProMonitor _monitor;
        private readonly Dispatcher _dispatcher;
        private readonly MxLintInstallerService _mxLintInstallerService;

        public ObservableCollection<StudioProInstance> RunningInstances { get; } = new();
        public ObservableCollection<StudioProInstallation> StudioProVersions { get; } = new();
        public ObservableCollection<GitHubRelease> AvailableReleases { get; } = new();

        /// <summary>Filtered view over <see cref="StudioProVersions"/>. Bind ItemsSource to this.</summary>
        public ICollectionView StudioProVersionsView { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LastScannedLabel))]
        private DateTime? lastScanned;

        public string LastScannedLabel => LastScanned is { } time ? $"Last scanned: {time:g}" : "Not scanned yet";

        [ObservableProperty]
        private string searchQuery = "";

        [ObservableProperty]
        private string summaryLabel = "";

        [ObservableProperty]
        private GitHubRelease? latest;

        [ObservableProperty]
        private bool isLoadingReleases;

        // ----- Filtering -----

        [ObservableProperty]
        private string selectedFilter = "all";

        partial void OnSearchQueryChanged(string value) => StudioProVersionsView.Refresh();

        partial void OnSelectedFilterChanged(string value) => StudioProVersionsView.Refresh();

        private bool FilterInstallations(object item)
        {
            if (item is not StudioProInstallation i) return false;

            var matchesFilter = SelectedFilter switch
            {
                "installed" => i.IsInstalled,
                "none" => !i.IsInstalled,
                "running" => i.IsRunning,
                _ => true
            };

            return matchesFilter && MatchesSearch(i);
        }

        private bool MatchesSearch(StudioProInstallation i)
        {
            var query = SearchQuery?.Trim();
            if (string.IsNullOrEmpty(query)) return true;

            return Contains(i.StudioProVersion, query) || Contains(i.InstallationPath, query) || Contains(i.InstalledVersion, query);
        }

        private static bool Contains(string? source, string query) => source?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

        // ----- Scanning state -----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScanButtonLabel))]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
        private bool isScanning;

        public string ScanButtonLabel => IsScanning ? "Scanning..." : "Scan now";
        public bool CanScan => !IsScanning;

        // ----- Construction -----

        public MainViewModel(IStudioProMonitor monitor)
        {
            _monitor = monitor;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _mxLintInstallerService = new MxLintInstallerService();

            // Create the view before anything can call Refresh() on it.
            StudioProVersionsView = CollectionViewSource.GetDefaultView(StudioProVersions);
            StudioProVersionsView.Filter = FilterInstallations;

            _monitor.StateChanged += OnStateChanged;
            _monitor.Start();
            Apply(_monitor.IsRunning, _monitor.Instances);
        }

        public async Task InitializeAsync()
        {
            await ScanCommand.ExecuteAsync(null);
            await LoadReleasesCommand.ExecuteAsync(null);

            foreach (var row in StudioProVersions)
                row.SelectedRelease ??= AvailableReleases.FirstOrDefault();
        }

        // ----- Commands -----

        [RelayCommand(CanExecute = nameof(CanScan))]
        private async Task ScanAsync()
        {
            IsScanning = true;
            try
            {
                _monitor.Refresh();

                // Do the slow work off the UI thread...
                var installations = await Task.Run(() =>
                    _mxLintInstallerService.GetInstalledVersions(_monitor, _mxLintInstallerService).ToList());

                // ...and touch the collection back on the UI thread.
                StudioProVersions.Clear();
                foreach (var studioPro in installations)
                {
                    studioPro.SelectedRelease = Latest;
                    StudioProVersions.Add(studioPro);
                }

                RefreshRunningStates(); // also re-applies the filter
            }
            finally
            {
                IsScanning = false;
                LastScanned = DateTime.Now;
            }
        }

        [RelayCommand]
        private async Task LoadReleasesAsync()
        {
            IsLoadingReleases = true;
            try
            {
                AvailableReleases.Clear();
                var releases = await _mxLintInstallerService.GetReleaseList();
                foreach (var release in releases)
                    AvailableReleases.Add(release);

                var newest = AvailableReleases.FirstOrDefault();
                foreach (var studioProInstallation in StudioProVersions)
                    studioProInstallation.SelectedRelease ??= newest;

                Latest ??= newest;
            }
            finally
            {
                IsLoadingReleases = false;
            }
        }

        // ----- Monitor handling -----

        private void OnStateChanged(object? sender, StudioProStateChangedEventArgs e)
            => _dispatcher.Invoke(() => Apply(e.IsRunning, e.Instances));

        private void Apply(bool running, IReadOnlyCollection<StudioProInstance> instances)
        {
            RunningInstances.Clear();
            foreach (var i in instances) RunningInstances.Add(i);

            RefreshRunningStates();
        }

        private void RefreshRunningStates()
        {
            foreach (var studioProInstallation in StudioProVersions)
                studioProInstallation.IsRunning = _monitor.IsVersionRunning(studioProInstallation.InstallationPath);

            // IsRunning changed after the items were added, so re-evaluate the filter.
            StudioProVersionsView.Refresh();
        }

        public void Dispose()
        {
            _monitor.StateChanged -= OnStateChanged;
            _monitor.Dispose();
        }
    }
}