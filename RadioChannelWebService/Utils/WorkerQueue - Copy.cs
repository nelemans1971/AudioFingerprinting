using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CDR.Logging;

namespace CDRWebservice
{
    /// <summary>
    /// Static class waarin we onze items kunnen dumpen om in de achtergound
    /// verwerkt te worden. 
    /// 
    /// NIET gegarandeerd dat je item ook echt aan de beurt komt (als programma wordt afgesloten)
    /// </summary>
    static class HelperThreadedWorkerQueue
    {
        private static ThreadedWorkerQueue backgroundWorker = null;
        private static List<ThreadedWorkerQueue> backgroundWorkerList = null;


        public static void Start()
        {
            // Zou dit binnen een lock moeten?
            if (backgroundWorker == null)
            {
                backgroundWorker = new ThreadedWorkerQueue();
                backgroundWorker.Start();
            }
        }

        public static void Stop()
        {
            if (backgroundWorker != null)
            {
                backgroundWorker.Stop();
                backgroundWorker = null;
            }
        }

        public static void Add(IRunnable obj)
        {
            backgroundWorker.Add(obj);
        }

        public static int QueueCount
        {
            get
            {
                return backgroundWorker.QueueCount;
            }
        }

        public static bool IsRunning
        {
            get
            {
                return backgroundWorker.IsRunning;
            }
        }
    }


    class ThreadedWorkerQueue 
    {
        // Zie ook http://www.stev.org/post/2011/10/04/C-ThreadQueue-example.aspx &
        // http://www.stev.org/post/2011/10/03/C-Improving-the-abstract-thread-class.aspx

        private Queue<IRunnable> workerQueue = new Queue<IRunnable>();
        private IRunnable currentRuningItem = null;
        private bool threadStarted = false;
        private Thread thread = null;


        public void Start()
        {
            if (thread != null)
            {
                return;
            }

            thread = new Thread(new ParameterizedThreadStart(DoRun));
            thread.IsBackground = true;
            threadStarted = true;
            thread.Start();
        }
        
        public void Stop() 
        {
            if (thread != null) 
            {
                try
                {
                    lock (workerQueue)
                    {
                        threadStarted = false;
                        Monitor.Pulse(workerQueue); // "schut" thread wakker
                    }

                    // Wacht max 1 minuut om het ding te laten stoppen
                    thread.Join(new TimeSpan(0, 1, 0));
                    if (thread.IsAlive)
                    {
                        thread.Abort();
                    }
                    thread = null;
                }
                catch { }
            } 
        } 

        private void DoRun(object args) 
        { 
            Run(args); 
        }

        /// <summary>
        /// Voeg een item toe aan de queue om te verwerken
        /// </summary>
        /// <param name="obj"></param>
        public void Add(IRunnable obj) 
        {     
            lock (workerQueue)     
            {         
                workerQueue.Enqueue(obj);         
                Monitor.Pulse(workerQueue);
            } 
        }

        /// <summary>
        /// Aantal items in de queue die verwerkt moeten worden
        /// </summary>
        public int QueueCount
        {
            get
            {
                lock (workerQueue)
                {
                    return workerQueue.Count;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                return (thread != null);
            }
        }

        protected void Run(object obj)
        {
            while (true)
            {
                // Moeten we stoppen?
                if (!threadStarted)
                {
                    break;
                }

                try
                {
                    IRunnable item = null;
                    lock (workerQueue)
                    {
                        if (workerQueue.Count == 0)
                        {
                            Monitor.Wait(workerQueue); 
                        }
                        if (workerQueue.Count > 0)
                        {
                            item = workerQueue.Dequeue();
                            currentRuningItem = item;
                        }
                    }

                    if (currentRuningItem == null)
                    {
                        continue;
                    }

                    // Start het verwerken van dit item

                    try
                    {
                        currentRuningItem.Run();
                    }
                    catch (Exception e)
                    {
                        CDRLogger.Logger.LogError(e);
                    }

                    lock (workerQueue)
                    {
                        currentRuningItem = null;
                    }
                }
                catch (Exception e)
                {
                    CDRLogger.Logger.LogError(e);
                }
            }
        }
    }

    public interface IRunnable 
    { 
        void Run(); 
    }
}
