using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Analyzer = Lucene.Net.Analysis.Analyzer;
using Document = Lucene.Net.Documents.Document;
using Directory = Lucene.Net.Store.Directory;

namespace Lucene.Net.Index
{
    public class ThreadedIndexWriter : IndexWriter
    {
        ThreadPoolWithBlockingQueue threadPool; // see http://www.codeproject.com/KB/threads/smartthreadpool.aspx
        private Analyzer defaultAnalyzer;

        public ThreadedIndexWriter(Directory dir, Analyzer a, bool create, IndexWriter.MaxFieldLength mfl)
            : base(dir, a, create, mfl)
        {
            defaultAnalyzer = a;

            int numThreads = 1;
            // 2 of 3 processors?
            if (Environment.ProcessorCount > 1 && Environment.ProcessorCount < 4)
            {
                // Special case
                numThreads = 2;
            }
            else if (Environment.ProcessorCount >= 4)
            {
                numThreads = Environment.ProcessorCount - 1;
            }
            int maxQueueSize = (numThreads * 2) + 2000;
            
            threadPool = new ThreadPoolWithBlockingQueue(numThreads, maxQueueSize);
        }       

        public ThreadedIndexWriter(Directory dir, Analyzer a, bool create, int numThreads, int maxQueueSize, IndexWriter.MaxFieldLength mfl)
            : base(dir, a, create, mfl)
        {
            defaultAnalyzer = a;

            threadPool = new ThreadPoolWithBlockingQueue(numThreads, maxQueueSize);
        }

        public override void Optimize()
        {
            // Wait for the completion of all work items
            threadPool.WaitForIdle();
            base.Optimize();
        }

        public override void Dispose()
        {
            Finish();

            base.Dispose();
        }

        public override void Dispose(bool waitForMerges)
        {
            Finish();
            base.Dispose(waitForMerges);
        }

        public override void Rollback()
        {
            // Wait for the completion of all work items
            threadPool.WaitForIdle();
            base.Rollback();
        }

        /// <summary>
        /// Shutdown thread pool
        /// </summary>
        private void Finish()
        {
            // Wait for the completion of all work items
            threadPool.WaitForIdle();
            threadPool.Shutdown(false, Timeout.Infinite);
        }
        
        public override void AddDocument(Document doc)
        {
            threadPool.QueueWorkItem(new Amib.Threading.Action<Document, Term, Analyzer>(RunJob), doc, null, defaultAnalyzer);
        }

        public void addDocument(Document doc, Analyzer a)
        {
            threadPool.QueueWorkItem(new Amib.Threading.Action<Document, Term, Analyzer>(RunJob), doc, null, a);
        } 
        
        public void updateDocument(Term term, Document doc)
        {
            threadPool.QueueWorkItem(new Amib.Threading.Action<Document, Term, Analyzer>(RunJob), doc, term, defaultAnalyzer);
        }
        
        public void updateDocument(Term term, Document doc, Analyzer a)
        {
            threadPool.QueueWorkItem(new Amib.Threading.Action<Document, Term, Analyzer>(RunJob), doc, term, a);
        }


        private void RunJob(Document doc, Term delTerm, Analyzer analyzer)
        {
            base.UpdateDocument(delTerm, doc, analyzer);
        }


        private class ThreadPoolWithBlockingQueue : Amib.Threading.SmartThreadPool
        {
            private int maxQueueSize;
            private ManualResetEvent emptyWorkQueue = new ManualResetEvent(true);

            public ThreadPoolWithBlockingQueue( int numThreads, int maxQueueSize): base ( 10 * 1000, numThreads)
            {
                if (maxQueueSize > numThreads)
                {
                    maxQueueSize = numThreads + 1;
                }
                this.maxQueueSize = maxQueueSize;
            }

            internal override void Enqueue(Amib.Threading.Internal.WorkItem workItem)
            {
                while ((WaitingCallbacks + InUseThreads) > maxQueueSize)
                {
                    emptyWorkQueue.Reset();
                    // We moeten wachten totdat er weer wat "vrij" komt
                    emptyWorkQueue.WaitOne(250); // wacht max 250 ms
                    emptyWorkQueue.Set();
                }//while
                
                base.Enqueue(workItem);
            }

            protected override void DecrementWorkItemsCount()
            {
                base.DecrementWorkItemsCount();
                emptyWorkQueue.Set();
            }
        }
    } // class ThreadIndexWriter
}
