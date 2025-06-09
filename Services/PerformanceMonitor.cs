using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CyclerSim.Services
{
    public class PerformanceMonitor : IDisposable
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly Timer _monitorTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private long _totalBytesTransmitted = 0;
        private long _totalDataPointsSent = 0;
        private DateTime _startTime;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
            _startTime = DateTime.Now;

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Performance counters not available");
            }

            _monitorTimer = new Timer(ReportPerformance, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public void IncrementDataPoints(int count)
        {
            Interlocked.Add(ref _totalDataPointsSent, count);
        }

        public void IncrementBytesTransmitted(long bytes)
        {
            Interlocked.Add(ref _totalBytesTransmitted, bytes);
        }

        private void ReportPerformance(object state)
        {
            try
            {
                var runtime = DateTime.Now - _startTime;
                var avgDataPointsPerSec = _totalDataPointsSent / runtime.TotalSeconds;
                var avgBytesPerSec = _totalBytesTransmitted / runtime.TotalSeconds;

                var cpuUsage = _cpuCounter?.NextValue() ?? 0;
                var availableMemory = _memoryCounter?.NextValue() ?? 0;

                _logger.LogInformation(
                    "Performance Report - Runtime: {Runtime:hh\\:mm\\:ss}, " +
                    "Avg Data Points/sec: {DataPointsPerSec:F1}, " +
                    "Avg Bytes/sec: {BytesPerSec:F0}, " +
                    "CPU Usage: {CpuUsage:F1}%, " +
                    "Available Memory: {AvailableMemory:F0} MB",
                    runtime, avgDataPointsPerSec, avgBytesPerSec, cpuUsage, availableMemory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting performance");
            }
        }

        public void Dispose()
        {
            _monitorTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
        }
    }
}