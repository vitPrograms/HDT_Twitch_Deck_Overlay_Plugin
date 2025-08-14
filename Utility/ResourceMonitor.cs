using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace TwitchDeckOverlay.Utility
{
    public class ResourceMonitor : IDisposable
    {
        private readonly Timer _monitoringTimer;
        private readonly Process _currentProcess;
        private bool _disposed = false;
        
        // Статистика використання ресурсів
        public long PeakMemoryUsage { get; private set; }
        public double AverageCpuUsage { get; private set; }
        public long CurrentMemoryUsage => _currentProcess?.WorkingSet64 ?? 0;
        public int ThreadCount => _currentProcess?.Threads.Count ?? 0;
        public int HandleCount => _currentProcess?.HandleCount ?? 0;
        
        private double _totalCpuTime = 0;
        private int _cpuSamples = 0;
        private DateTime _lastCpuTime = DateTime.UtcNow;
        private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

        public ResourceMonitor(int monitoringIntervalMs = 5000)
        {
            _currentProcess = Process.GetCurrentProcess();
            _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
            
            _monitoringTimer = new Timer(MonitorResources, null, 0, monitoringIntervalMs);
            Log.Info($"Resource monitoring started with {monitoringIntervalMs}ms interval");
        }

        private void MonitorResources(object state)
        {
            try
            {
                if (_disposed || _currentProcess == null) return;

                // Моніторинг пам'яті
                var currentMemory = _currentProcess.WorkingSet64;
                if (currentMemory > PeakMemoryUsage)
                {
                    PeakMemoryUsage = currentMemory;
                }

                // Моніторинг CPU
                var currentTime = DateTime.UtcNow;
                var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                var totalMsPassed = (currentTime - _lastCpuTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var cpuUsagePercentage = cpuUsageTotal * 100;

                _totalCpuTime += cpuUsagePercentage;
                _cpuSamples++;
                AverageCpuUsage = _totalCpuTime / _cpuSamples;

                _lastCpuTime = currentTime;
                _lastTotalProcessorTime = currentTotalProcessorTime;

                // Логування при високому використанні ресурсів
                if (cpuUsagePercentage > 10.0) // Більше 10% CPU
                {
                    Log.Warn($"High CPU usage detected: {cpuUsagePercentage:F2}%");
                }

                if (currentMemory > 100 * 1024 * 1024) // Більше 100MB
                {
                    Log.Warn($"High memory usage detected: {currentMemory / (1024.0 * 1024.0):F2} MB");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error monitoring resources: {ex.Message}");
            }
        }

        public void LogResourceReport()
        {
            try
            {
                Log.Info("=== Resource Usage Report ===");
                Log.Info($"Current Memory Usage: {CurrentMemoryUsage / (1024.0 * 1024.0):F2} MB");
                Log.Info($"Peak Memory Usage: {PeakMemoryUsage / (1024.0 * 1024.0):F2} MB");
                Log.Info($"Average CPU Usage: {AverageCpuUsage:F2}%");
                Log.Info($"Thread Count: {ThreadCount}");
                Log.Info($"Handle Count: {HandleCount}");
                
                // Додаткова інформація про GC
                Log.Info($"GC Gen 0 Collections: {GC.CollectionCount(0)}");
                Log.Info($"GC Gen 1 Collections: {GC.CollectionCount(1)}");
                Log.Info($"GC Gen 2 Collections: {GC.CollectionCount(2)}");
                Log.Info($"Total Memory (GC): {GC.GetTotalMemory(false) / (1024.0 * 1024.0):F2} MB");
                Log.Info("=== End Resource Report ===");
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating resource report: {ex.Message}");
            }
        }

        public ResourceSnapshot TakeSnapshot()
        {
            return new ResourceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsage = CurrentMemoryUsage,
                ThreadCount = ThreadCount,
                HandleCount = HandleCount,
                GcGen0Collections = GC.CollectionCount(0),
                GcGen1Collections = GC.CollectionCount(1),
                GcGen2Collections = GC.CollectionCount(2),
                TotalManagedMemory = GC.GetTotalMemory(false)
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();
                LogResourceReport();
                _disposed = true;
            }
        }
    }

    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public long MemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int GcGen0Collections { get; set; }
        public int GcGen1Collections { get; set; }
        public int GcGen2Collections { get; set; }
        public long TotalManagedMemory { get; set; }
    }
}