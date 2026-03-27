using System.Collections.Concurrent;
using System.Diagnostics;
using SiteLink.API.Threading;
using SiteLink.API.Metrics;

namespace SiteLink.Services
{
    public class SessionService : BackgroundService
    {
        public static SessionService Instance { get; private set; }

        public static int ThreadId { get; private set; }

        public ServiceStats Stats { get; } = new ServiceStats("SessionService");

        public SessionManager Manager { get; } = new SessionManager();

        private readonly ConcurrentQueue<Action> _workQueue = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Instance = this;
            ThreadId = Thread.CurrentThread.ManagedThreadId;

            Scheduler.RegisterThread(ThreadId, "SessionService", EnqueueWork);

            while (!stoppingToken.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();

                await Task.Delay(100);

                try
                {
                    Manager?.Update();
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error(ex, "SessionService");
                }

                ProcessWorkQueue();

                stopwatch.Stop();
                Stats.UpdateQueueDepth(_workQueue.Count);
                Stats.RecordIteration(stopwatch.Elapsed);
            }
        }

        private void EnqueueWork(Action callback)
        {
            _workQueue.Enqueue(callback);
        }

        private void ProcessWorkQueue()
        {
            int processed = 0;
            while (_workQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                    processed++;
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error($"Error processing work queue for SessionService: {ex}", "SessionService");
                }
            }
            if (processed > 0)
                Stats.RecordProcessedItems(processed);
        }
    }
}
