using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioFingerprint.Audio;

namespace AudioFingerprint
{
    public enum FingerprintAlgorithm
    {
        Unknown = 0,
        SubFingerprint,
        AcoustIDFingerprint
    }

    public class Resultset
    {
        private object lockObject = new object();

        private FingerprintAlgorithm fingerprintAlgoritm;
        private TimeSpan queryTime;

        private TimeSpan subFingerQueryTime;
        private TimeSpan fingerLoadTime;
        private TimeSpan matchTime;

        private List<ResultEntry> resultEntries;

        public Resultset()
        {
            fingerprintAlgoritm = FingerprintAlgorithm.Unknown;
            queryTime = new TimeSpan(0);
            subFingerQueryTime = new TimeSpan(0);
            fingerLoadTime = new TimeSpan(0);
            matchTime = new TimeSpan(0);
        
            resultEntries = new List<ResultEntry>();
        }
        
        public Resultset(TimeSpan queryTime, List<ResultEntry> resultEntries)
        {
            this.queryTime = queryTime;
            this.resultEntries = resultEntries;
        }

        public void Add(ResultEntry qe)
        {
            resultEntries.Add(qe);
        }

        public FingerprintAlgorithm Algorithm
        {
            get
            {
                return fingerprintAlgoritm;
            }
            set
            {
                fingerprintAlgoritm = value;
            }
        }

        /// <summary>
        /// total time spent on query
        /// </summary>
        public TimeSpan QueryTime
        {
            get
            {
                return queryTime;
            }
            set
            {
                queryTime = value;
            }
        }

        public TimeSpan FingerQueryTime
        {
            get
            {
                lock (lockObject)
                {
                    return subFingerQueryTime;
                }
            }
            set
            {
                lock (lockObject)
                {
                    subFingerQueryTime = value;
                }
            }
        }

        public TimeSpan FingerLoadTime
        {
            get
            {
                lock (lockObject)
                {
                    return fingerLoadTime;
                }
            }
            set
            {
                lock (lockObject)
                {
                    fingerLoadTime = value;
                }
            }
        }

        public TimeSpan MatchTime
        {
            get
            {
                lock (lockObject)
                {
                    return matchTime;
                }
            }
            set
            {
                lock (lockObject)
                {
                    matchTime = value;
                }
            }
        }

        /// <summary>
        /// Order list of Result entries
        /// </summary>
        public List<ResultEntry> ResultEntries
        {
            get
            {
                return resultEntries;
            }
            set
            {
                resultEntries = value;
            }
        }
    }

    public class ResultEntry
    {
        public ResultEntry()
        {
        }

        public ResultEntry(object reference, long fingerTrackID, int similarity, int timeIndex, int indexNumberInMatchList, int subFingerCountHitInFingerprint, SearchStrategy searchStrategy, int searchIteration)
        {
            this.Reference = reference;
            this.FingerTrackID = fingerTrackID;
            this.Similarity = similarity;
            this.TimeIndex = timeIndex;
            this.IndexNumberInMatchList = indexNumberInMatchList;
            this.SubFingerCountHitInFingerprint = subFingerCountHitInFingerprint;
            this.SearchStrategy = searchStrategy;
            this.SearchIteration = searchIteration;
        }

        public object Reference
        {
            get;
            set;
        }
        public long FingerTrackID
        {
            get;
            set;
        }

        public int Similarity
        {
            get;
            set;
        }

        /// <summary>
        /// Index in SubFingerprint, every index represents a 11.6ms step
        /// </summary>
        public int TimeIndex
        {
            get;
            set;
        }

        public TimeSpan Time
        {
            get
            {
                // every subfingerprint is seperated by 11.6 milliseconds
                return new TimeSpan((long)(11.6f * TimeIndex * 10000)); // convert milliseconds to nanoseconds (
            }
        }

        public int IndexNumberInMatchList
        {
            get;
            set;
        }

        public int SubFingerCountHitInFingerprint 
        {
            get;
            set;
        }
                
        public SearchStrategy SearchStrategy
        {
            get;
            set;
        }

        public int SearchIteration
        {
            get;
            set;
        }
    }

    public enum SearchStrategy
    {
        NotSet = 0,
        PlanFast,
        PlanSlow,
        Plan0,
        Plan1,
        Plan2,
        Plan3,
        Plan4
    }

}
