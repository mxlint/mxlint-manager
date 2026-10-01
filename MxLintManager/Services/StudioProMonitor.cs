using MxLintManager.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MxLintManager.Services
{
    public class StudioProMonitor : IStudioProMonitor
    {
        private const string ProcessName = "studiopro";
        private readonly TimeSpan _interval;
        private readonly object _sync = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private IReadOnlyList<StudioProInstance> _instances = Array.Empty<StudioProInstance>();
        public StudioProMonitor(TimeSpan? pollInterval = null) => _interval = pollInterval ?? TimeSpan.FromSeconds(2);
        public bool IsRunning => _instances.Count > 0;
        public IReadOnlyList<StudioProInstance> Instances => _instances;
        public event EventHandler<StudioProStateChangedEventArgs>? StateChanged;

        public void Start()
        {
            lock (_sync)
            {
                if (_loop is not null) return;
                _cts = new CancellationTokenSource();
                _loop = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (_sync) { cts = _cts; _cts = null; _loop = null; }
            cts?.Cancel();
            cts?.Dispose();
        }

        private async Task RunAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(_interval);
            try
            {
                while (await timer.WaitForNextTickAsync(token)) 
                    Refresh();
            }
            catch (OperationCanceledException) { }
        }

        public IReadOnlyList<StudioProInstance> Refresh()
        {
            var current = Scan();
            bool changed;
            lock (_sync)
            {
                changed = HasChanged(_instances, current);
                _instances = current;
            }
            if (changed)
            {
                StateChanged?.Invoke(this, new StudioProStateChangedEventArgs(current.Count > 0, current));
            }
            return current;
        }

        public bool IsVersionRunning(string installDirectory)
            => GetRunningInstances(installDirectory).Count > 0;

        public IReadOnlyList<StudioProInstance> GetRunningInstances(string installDirectory)
        {
            IReadOnlyList<StudioProInstance> snapshot;
            lock (_sync)
                snapshot = _instances;

            var matches = new List<StudioProInstance>();
            foreach (var instance in snapshot)
            {
                if (!StudioProLocationMatcher.IsInstanceOfVersion(instance, installDirectory))
                    continue;

                matches.Add(instance);
            }
            return matches;
        }

        private static bool HasChanged(IReadOnlyList<StudioProInstance> a, IReadOnlyList<StudioProInstance> b)
            => a.Count != b.Count || !a.Select(i => i.ProcessId).OrderBy(x => x).SequenceEqual(b.Select(i => i.ProcessId).OrderBy(x => x));

        private static IReadOnlyList<StudioProInstance> Scan()
        {
            var list = new List<StudioProInstance>();
            foreach (var proc in Process.GetProcessesByName(ProcessName))
            {
                try
                {
                    var path = TryGetPath(proc);
                    var version = path is not null && File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).ProductVersion : null;
                    list.Add(new StudioProInstance(proc.Id, path, version));
                }
                catch { list.Add(new StudioProInstance(proc.Id, null, null)); }
                finally {  proc.Dispose(); }
            }
            return list;
        }

        private static string? TryGetPath(Process proc)
        {
            try { return proc.MainModule?.FileName; }
            catch (Win32Exception) { return null;  }
            catch (InvalidOperationException) { return null; }
        }

        public void Dispose() => Stop();
    }

    public static class StudioProLocationMatcher
    {
        public static bool IsInstanceOfVersion(StudioProInstance instance, string installDirectory)
            => instance.ExecutablePath is not null
               && IsPathUnder(instance.ExecutablePath, installDirectory);

        private static bool IsPathUnder(string filePath, string directory)
        {
            var file = Path.GetFullPath(filePath);
            var dir = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;                 // the boundary guard
            return file.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }
    }
}
