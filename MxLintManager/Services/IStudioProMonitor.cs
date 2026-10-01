using MxLintManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace MxLintManager.Services
{
    public sealed record StudioProInstance(int ProcessId, string? ExecutablePath, string? ProductVersion);

    public sealed class StudioProStateChangedEventArgs : EventArgs
    {
        public StudioProStateChangedEventArgs(bool isRunning, IReadOnlyList<StudioProInstance> instances) 
            => (IsRunning, Instances) = (isRunning, instances);

        public bool IsRunning { get; }
        public IReadOnlyList<StudioProInstance> Instances { get;  }
    }
    
    public interface IStudioProMonitor : IDisposable
    {
        bool IsRunning { get; }
        IReadOnlyList<StudioProInstance> Instances { get; }
        event EventHandler<StudioProStateChangedEventArgs>? StateChanged;
        void Start();
        void Stop();
        IReadOnlyList<StudioProInstance> Refresh();
        bool IsVersionRunning(string installDirectory);
        IReadOnlyList<StudioProInstance> GetRunningInstances(string installDirectory);
    }
}
