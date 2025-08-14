using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace TwitchDeckOverlay.Utility
{
    public class PerformanceMonitor : IDisposable
    {
        private readonly Dictionary<string, PerformanceMetrics> _metrics = new Dictionary<string, PerformanceMetrics>();
        private readonly object _lock = new object();
        private bool _disposed = false;

        public class PerformanceMetrics
        {
            public string OperationName { get; set; }
            public long TotalExecutions { get; set; }
            public long TotalMilliseconds { get; set; }
            public long MinMilliseconds { get; set; } = long.MaxValue;
            public long MaxMilliseconds { get; set; }
            public double AverageMilliseconds => TotalExecutions > 0 ? (double)TotalMilliseconds / TotalExecutions : 0;
            public long MemoryUsedBytes { get; set; }
            public DateTime LastExecution { get; set; }
        }

        public IDisposable StartOperation(string operationName, [CallerMemberName] string callerName = "")
        {
            return new OperationTimer(this, $"{callerName}.{operationName}");
        }

        internal void RecordOperation(string operationName, long elapsedMs, long memoryUsed)
        {
            lock (_lock)
            {
                if (!_metrics.ContainsKey(operationName))
                {
                    _metrics[operationName] = new PerformanceMetrics { OperationName = operationName };
                }

                var metric = _metrics[operationName];
                metric.TotalExecutions++;
                metric.TotalMilliseconds += elapsedMs;
                metric.MinMilliseconds = Math.Min(metric.MinMilliseconds, elapsedMs);
                metric.MaxMilliseconds = Math.Max(metric.MaxMilliseconds, elapsedMs);
                metric.MemoryUsedBytes = memoryUsed;
                metric.LastExecution = DateTime.Now;
            }
        }

        public void LogPerformanceReport()
        {
            lock (_lock)
            {
                Log.Info("=== Performance Report ===");
                foreach (var kvp in _metrics)
                {
                    var metric = kvp.Value;
                    Log.Info($"Operation: {metric.OperationName}");
                    Log.Info($"  Executions: {metric.TotalExecutions}");
                    Log.Info($"  Total Time: {metric.TotalMilliseconds}ms");
                    Log.Info($"  Average Time: {metric.AverageMilliseconds:F2}ms");
                    Log.Info($"  Min Time: {metric.MinMilliseconds}ms");
                    Log.Info($"  Max Time: {metric.MaxMilliseconds}ms");
                    Log.Info($"  Memory Used: {metric.MemoryUsedBytes / 1024.0:F2} KB");
                    Log.Info($"  Last Execution: {metric.LastExecution}");
                    Log.Info("");
                }
                Log.Info("=== End Performance Report ===");
            }
        }

        public Dictionary<string, PerformanceMetrics> GetMetrics()
        {
            lock (_lock)
            {
                return new Dictionary<string, PerformanceMetrics>(_metrics);
            }
        }

        public void ClearMetrics()
        {
            lock (_lock)
            {
                _metrics.Clear();
            }
        }

        private class OperationTimer : IDisposable
        {
            private readonly PerformanceMonitor _monitor;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private readonly long _startMemory;

            public OperationTimer(PerformanceMonitor monitor, string operationName)
            {
                _monitor = monitor;
                _operationName = operationName;
                _startMemory = GC.GetTotalMemory(false);
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                var endMemory = GC.GetTotalMemory(false);
                var memoryUsed = Math.Max(0, endMemory - _startMemory);
                _monitor.RecordOperation(_operationName, _stopwatch.ElapsedMilliseconds, memoryUsed);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                LogPerformanceReport();
                _disposed = true;
            }
        }
    }
}