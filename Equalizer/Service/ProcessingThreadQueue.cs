using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Equalizer.Service
{
    internal class ProcessingThreadQueue
    {
        /// <summary>
        /// Очередь делегатов на исполнение потоком
        /// </summary>
        private readonly ConcurrentQueue<Action> _Queue = new();
        /// <summary>
        /// Сигнал для возобновления работы потока
        /// </summary>
        private readonly AutoResetEvent _Signal = new(false);
        /// <summary>
        /// Рабочий поток
        /// </summary>
        private readonly Thread _Worker;
        /// <summary>
        /// Булево работает ли поток
        /// </summary>
        private volatile bool _Running = true;
        public ProcessingThreadQueue()
        {
            _Worker = new Thread(WorkLoop) { IsBackground = true, Name = "личный раб" };
            _Worker.Start();
        }
        /// <summary>
        /// Ставит в очередь делегат на выполнение рабочим потоком
        /// </summary>
        public void Enqueue<T>(Action<T> action, T arg)
        {
            _Queue.Enqueue(() => action(arg));
            _Signal.Set();
        }
        /// <summary>
        /// луп работы потока
        /// </summary>
        private void WorkLoop()
        {
            while (_Running)
            {
                if (_Queue.TryDequeue(out var task))
                {
                    try
                    {
                        task();
                    }
                    catch { }
                }
                else
                {
                    _Signal.WaitOne();
                }
            }
        }

        public void Dispose()
        {
            _Running = false;
            _Worker.Join();
            _Signal.Dispose();
        }

    }
}
