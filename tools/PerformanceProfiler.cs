using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WinServiceManager.Tools
{
    /// <summary>
    /// 性能分析器
    /// </summary>
    public static class PerformanceProfiler
    {
        private static readonly Dictionary<string, Stopwatch> _timers = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// 开始计时
        /// </summary>
        public static IDisposable StartTimer(string operationName)
        {
            lock (_lock)
            {
                if (!_timers.ContainsKey(operationName))
                {
                    _timers[operationName] = new Stopwatch();
                }
                _timers[operationName].Restart();
            }

            return new TimerDisposable(operationName);
        }

        /// <summary>
        /// 记录操作完成
        /// </summary>
        public static void RecordOperation(string operationName)
        {
            lock (_lock)
            {
                if (_timers.ContainsKey(operationName))
                {
                    _timers[operationName].Stop();
                    var elapsed = _timers[operationName].ElapsedMilliseconds;
                    Debug.WriteLine($"[PERF] {operationName}: {elapsed}ms");
                }
            }
        }

        /// <summary>
        /// 获取所有操作的平均时间
        /// </summary>
        public static Dictionary<string, long> GetAverageTimes()
        {
            lock (_lock)
            {
                return _timers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ElapsedMilliseconds
                );
            }
        }

        /// <summary>
        /// 内存使用快照
        /// </summary>
        public static MemorySnapshot TakeMemorySnapshot()
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();

            return new MemorySnapshot
            {
                Timestamp = DateTime.Now,
                WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
                VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,
                GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
        }

        /// <summary>
        /// 强制垃圾回收并测量
        /// </summary>
        public static MemorySnapshot ForceGCAndMeasure()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return TakeMemorySnapshot();
        }

        private class TimerDisposable : IDisposable
        {
            private readonly string _operationName;

            public TimerDisposable(string operationName)
            {
                _operationName = operationName;
            }

            public void Dispose()
            {
                RecordOperation(_operationName);
            }
        }
    }

    /// <summary>
    /// 内存快照
    /// </summary>
    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long GCMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] WorkingSet: {WorkingSetMB}MB, " +
                   $"Private: {PrivateMemoryMB}MB, Virtual: {VirtualMemoryMB}MB, " +
                   $"GC: {GCMemoryMB}MB (G0:{Gen0Collections} G1:{Gen1Collections} G2:{Gen2Collections})";
        }
    }

    /// <summary>
    /// 性能基准测试
    /// </summary>
    public static class PerformanceBenchmark
    {
        /// <summary>
        /// 运行基准测试
        /// </summary>
        public static async Task<BenchmarkResult> RunBenchmarkAsync(
            string testName,
            Func<Task> operation,
            int iterations = 100,
            int warmupIterations = 10)
        {
            var results = new List<long>();

            // 预热
            for (int i = 0; i < warmupIterations; i++)
            {
                await operation();
            }

            // 实际测试
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                var sw = Stopwatch.StartNew();
                await operation();
                sw.Stop();

                results.Add(sw.ElapsedMilliseconds);
            }

            return new BenchmarkResult
            {
                TestName = testName,
                Iterations = iterations,
                MinTime = results.Min(),
                MaxTime = results.Max(),
                AverageTime = (long)results.Average(),
                MedianTime = (long)results.OrderBy(x => x).Skip(iterations / 2).First(),
                P95Time = (long)results.OrderBy(x => x).Skip((int)(iterations * 0.95)).First(),
                P99Time = (long)results.OrderBy(x => x).Skip((int)(iterations * 0.99)).First()
            };
        }
    }

    /// <summary>
    /// 基准测试结果
    /// </summary>
    public class BenchmarkResult
    {
        public string TestName { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public long MinTime { get; set; }
        public long MaxTime { get; set; }
        public long AverageTime { get; set; }
        public long MedianTime { get; set; }
        public long P95Time { get; set; }
        public long P99Time { get; set; }

        public override string ToString()
        {
            return $"{TestName} ({Iterations} iterations):\n" +
                   $"  Average: {AverageTime}ms\n" +
                   $"  Median:  {MedianTime}ms\n" +
                   $"  Min:     {MinTime}ms\n" +
                   $"  Max:     {MaxTime}ms\n" +
                   $"  P95:     {P95Time}ms\n" +
                   $"  P99:     {P99Time}ms";
        }
    }
}