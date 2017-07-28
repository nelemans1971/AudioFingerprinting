//#define SHOWTRACEINFO
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioFingerprint.Audio;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store;

namespace AudioFingerprint
{
    public class SubFingerprintQuery : IDisposable
    {
        private static ConcurrentDictionary<string, FingerprintHit> EmptyDictionary = new ConcurrentDictionary<string, FingerprintHit>();

        private IndexSearcher subFingerIndex;
        private bool useLookupHash;

        public SubFingerprintQuery(IndexSearcher subFingerIndex)
        {        
            // Kies hier de gewenste methode om de complete fingerprints op te halen
            // Er zijn 3 implementaties:
            // - GetFingerprintsLucene
            // - GetFingerprintsMSSQL
            // - GetFingerprintsMySQL
            this.getFingerprints = GetFingerprintsMySQL;

            this.subFingerIndex = subFingerIndex;
            this.useLookupHash = false;

            // Check if index hash precalculated lookup hashes (if not we init finger with different data)
            /*
            if (this.fingerIndex != null)
            {
                BooleanQuery query = new BooleanQuery();
                query.Add(new TermQuery(new Term("FINGERID", "1")), Occur.SHOULD);
                TopDocs topHits = this.fingerIndex.Search(query, 1);
                if (topHits.TotalHits > 0)
                {
                    ScoreDoc match = topHits.ScoreDocs[0];
                    Document doc = this.fingerIndex.Doc(match.Doc);
                    useLookupHash = (doc.GetField("LOOKUPHASHES") != null);
                }
            }
            */
            // Forceer dat setup table aangemaakt wordt
            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();
        }

        ~SubFingerprintQuery()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes all used resources and deletes the temporary file.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (subFingerIndex != null)
                {
                    subFingerIndex = null;
                }
            }
        }

        /// <summary>
        /// Beste zoek strategy is.
        /// Plan 1. Zoek eerst met zoveel mogelijk subfinger naar hits.
        ///          Kijk dan bij de eerste 25 hits of er een BER kleiner dan 2000 bij zit
        /// Plan 2. Niks gevonden.
        ///         Ga opnieuw 1ste 256 finger blok en doe nu stappen van 512 subfingers
        ///         Neem nu ook "varianten" mee
        /// </summary>
        public Resultset MatchAudioFingerprint(FingerprintSignature fsQuery, int berMatch = -1)
        {
            DateTime startTime = DateTime.Now;
            BooleanQuery.MaxClauseCount = 600000;

            ConcurrentDictionary<string, FingerprintHit> hits = new ConcurrentDictionary<string, FingerprintHit>();

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.CancellationToken = tokenSource.Token;

            Resultset result = new Resultset();
            {
                QueryPlanConfig config = new QueryPlanConfig();
                config.SearchStrategy = SearchStrategy.Plan0;
                config.fsQuery = fsQuery;
                config.MaxDOCSRetrieveResult = 10;
                config.MaxTopDOCS = config.MaxDOCSRetrieveResult;
                config.MinimalBERMatch = 2000; // default is te hoog en geeft false positieves
                config.Token = tokenSource;

                DoPlan0(config);
                if (config.LowestBER < 2000)
                {
                    // Cancel the rest of the search options
                    tokenSource.Cancel();
                }
                // copy data 
                foreach (KeyValuePair<string, FingerprintHit> item in config.Hits)
                {
                    hits.TryAdd(item.Key, item.Value);
                } //foreach

                result.FingerQueryTime = result.FingerQueryTime.Add(config.SubFingerQueryTime);
                result.FingerLoadTime = result.FingerLoadTime.Add(config.FingerLoadTime);
                result.MatchTime = result.MatchTime.Add(config.MatchTime);
            }

            if (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    Parallel.Invoke(pOptions,
                    () =>
                    {
                        QueryPlanConfig config = new QueryPlanConfig();
                        config.SearchStrategy = SearchStrategy.Plan1;
                        config.fsQuery = fsQuery;
                        config.MaxDOCSRetrieveResult = 10;
                        config.MaxTopDOCS = config.MaxDOCSRetrieveResult;
                        config.MinimalBERMatch = 2300; // default is te hoog en geeft false positieves
                        config.Token = tokenSource;

                        DoPlan1(config);
                        if (!tokenSource.IsCancellationRequested && config.Hits.Count > 0)
                        {
                            if (config.LowestBER < 2000)
                            {
                                // Cancel the rest of the search options
                                tokenSource.Cancel();
                            }
                            // copy data 
                            foreach (KeyValuePair<string, FingerprintHit> item in config.Hits)
                            {
                                hits.TryAdd(item.Key, item.Value);
                            } //foreach
                        }

                        lock (result)
                        {
                            result.FingerQueryTime = result.FingerQueryTime.Add(config.SubFingerQueryTime);
                            result.FingerLoadTime = result.FingerLoadTime.Add(config.FingerLoadTime);
                            result.MatchTime = result.MatchTime.Add(config.MatchTime);
                        }
                    },
                    () =>
                    {
                        QueryPlanConfig config = new QueryPlanConfig();
                        config.SearchStrategy = SearchStrategy.Plan2;
                        config.fsQuery = fsQuery;
                        config.MaxDOCSRetrieveResult = 25;
                        config.MaxTopDOCS = config.MaxDOCSRetrieveResult;
                        config.MinimalBERMatch = 2300; // vanwege extra subfinger varianten kan makkelijker een verkeerde match gevonden worden, dus strenger zijn!
                        config.Token = tokenSource;

                        DoPlan2(config);
                        if (!tokenSource.IsCancellationRequested && config.Hits.Count > 0)
                        {
                            if (config.LowestBER < 2000)
                            {
                                // Cancel the rest of the search options
                                tokenSource.Cancel();
                            }
                            // copy data 
                            foreach (KeyValuePair<string, FingerprintHit> item in config.Hits)
                            {
                                hits.TryAdd(item.Key, item.Value);
                            } //foreach
                        }

                        lock (result)
                        {
                            result.FingerQueryTime = result.FingerQueryTime.Add(config.SubFingerQueryTime);
                            result.FingerLoadTime = result.FingerLoadTime.Add(config.FingerLoadTime);
                            result.MatchTime = result.MatchTime.Add(config.MatchTime);
                        }
                    });
                }
                catch
                {
                }
            }

            // Geef resultaat terug
            List<ResultEntry> resultEntries = hits.OrderBy(e => e.Value.BER)
                                                  .Select(e => new ResultEntry
                                                  {
                                                      Reference = e.Value.Fingerprint.Reference,
                                                      FingerTrackID = e.Value.Fingerprint.FingerTrackID,
                                                      Similarity = e.Value.BER,
                                                      TimeIndex = e.Value.TimeIndex,
                                                      IndexNumberInMatchList = e.Value.IndexNumberInMatchList,
                                                      SubFingerCountHitInFingerprint = e.Value.SubFingerCountHitInFingerprint,
                                                      SearchStrategy = e.Value.SearchStrategy,
                                                      SearchIteration = e.Value.SearchIteration
                                                  })
                                                  .ToList();

            result.QueryTime = (DateTime.Now - startTime);
            result.ResultEntries = resultEntries;
            result.Algorithm = FingerprintAlgorithm.SubFingerprint;
            return result;
        }

        public Resultset MatchAudioFingerprintFast(FingerprintSignature fsQuery, int berMatch = -1)
        {
            DateTime startTime = DateTime.Now;
            BooleanQuery.MaxClauseCount = 600000;

            ConcurrentDictionary<string, FingerprintHit> hits = new ConcurrentDictionary<string, FingerprintHit>();

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.CancellationToken = tokenSource.Token;

            Resultset result = new Resultset();
            QueryPlanConfig config = new QueryPlanConfig();
            config.SearchStrategy = SearchStrategy.PlanFast;
            config.fsQuery = fsQuery;
            config.MaxDOCSRetrieveResult = 10;
            config.MaxTopDOCS = config.MaxDOCSRetrieveResult;
            config.MinimalBERMatch = 2000; // default is te hoog en geeft false positieves
            config.Token = tokenSource;

            List<HashIndex> matchSubFingerIndex = new List<HashIndex>();
            SubFingerIndexPlanNormal(config, 4, out matchSubFingerIndex);
            TopDocs topHits = QuerySubFingers(config, matchSubFingerIndex);
            int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
            if (fingerIDs.Length > 0)
            {
                List<FingerprintSignature> fingerprints = getFingerprints(config, fingerIDs);
                FindPossibleMatch(config, fingerprints, matchSubFingerIndex);
            }

            // copy data 
            foreach (KeyValuePair<string, FingerprintHit> item in config.Hits)
            {
                hits.TryAdd(item.Key, item.Value);
            } //foreach

            result.FingerQueryTime = result.FingerQueryTime.Add(config.SubFingerQueryTime);
            result.FingerLoadTime = result.FingerLoadTime.Add(config.FingerLoadTime);
            result.MatchTime = result.MatchTime.Add(config.MatchTime);

            // Geef resultaat terug
            List<ResultEntry> resultEntries = hits.OrderBy(e => e.Value.BER)
                                                  .Select(e => new ResultEntry
                                                  {
                                                      Reference = e.Value.Fingerprint.Reference,
                                                      FingerTrackID = e.Value.Fingerprint.FingerTrackID,
                                                      Similarity = e.Value.BER,
                                                      TimeIndex = e.Value.TimeIndex,
                                                      IndexNumberInMatchList = e.Value.IndexNumberInMatchList,
                                                      SubFingerCountHitInFingerprint = e.Value.SubFingerCountHitInFingerprint,
                                                      SearchStrategy = e.Value.SearchStrategy,
                                                      SearchIteration = e.Value.SearchIteration
                                                  })
                                                  .ToList();

            result.QueryTime = (DateTime.Now - startTime);
            result.ResultEntries = resultEntries;
            return result;
        }

        public Resultset MatchAudioFingerprintSlow(FingerprintSignature fsQuery, int berMatch = -1)
        {
            DateTime startTime = DateTime.Now;
            BooleanQuery.MaxClauseCount = 600000;

            ConcurrentDictionary<string, FingerprintHit> hits = new ConcurrentDictionary<string, FingerprintHit>();

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.CancellationToken = tokenSource.Token;

            Resultset result = new Resultset();
            QueryPlanConfig config = new QueryPlanConfig();
            config.SearchStrategy = SearchStrategy.PlanSlow;
            config.fsQuery = fsQuery;
            config.MaxDOCSRetrieveResult = 10;
            config.MaxTopDOCS = config.MaxDOCSRetrieveResult;
            config.MinimalBERMatch = 2000; // default is te hoog en geeft false positieves
            config.Token = tokenSource;

            List<HashIndex> subFingerIndexWithVariants = new List<HashIndex>();
            // Haal lijst op over max 12 seconden data (geeft bij goede kwaliteit geluid snel een goede hit)
            SubFingerIndexPlanWithVariants(config, 256, 0, 4, 3, out subFingerIndexWithVariants);
            TopDocs topHits = QuerySubFingers(config, subFingerIndexWithVariants);
            int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
            if (fingerIDs.Length > 0)
            {
                List<FingerprintSignature> fingerprints = getFingerprints(config, fingerIDs);
                FindPossibleMatch(config, fingerprints, subFingerIndexWithVariants);
            }

            // copy data 
            foreach (KeyValuePair<string, FingerprintHit> item in config.Hits)
            {
                hits.TryAdd(item.Key, item.Value);
            } //foreach

            result.FingerQueryTime = result.FingerQueryTime.Add(config.SubFingerQueryTime);
            result.FingerLoadTime = result.FingerLoadTime.Add(config.FingerLoadTime);
            result.MatchTime = result.MatchTime.Add(config.MatchTime);

            // Geef resultaat terug
            List<ResultEntry> resultEntries = hits.OrderBy(e => e.Value.BER)
                                                  .Select(e => new ResultEntry
                                                  {
                                                      Reference = e.Value.Fingerprint.Reference,
                                                      FingerTrackID = e.Value.Fingerprint.FingerTrackID,
                                                      Similarity = e.Value.BER,
                                                      TimeIndex = e.Value.TimeIndex,
                                                      IndexNumberInMatchList = e.Value.IndexNumberInMatchList,
                                                      SubFingerCountHitInFingerprint = e.Value.SubFingerCountHitInFingerprint,
                                                      SearchStrategy = e.Value.SearchStrategy,
                                                      SearchIteration = e.Value.SearchIteration
                                                  })
                                                  .ToList();

            result.QueryTime = (DateTime.Now - startTime);
            result.ResultEntries = resultEntries;
            return result;
        }
        
        private void DoPlan0(QueryPlanConfig config)
        {
            // =============================================================================================================================
            // Plan 0
            // =============================================================================================================================
#if SHOWTRACEINFO
            Console.WriteLine("PLAN 0");
#endif
            List<HashIndex> subFingerIndexWithVariants = new List<HashIndex>();
            SubFingerIndexPlanWithVariants(config, 64, 0, 1, 3, out subFingerIndexWithVariants);
            List<HashIndex> subFingerIndexWithVariantsForMatch = new List<HashIndex>();
            SubFingerIndexPlanWithVariants(config, 256, 0, 1, 3, out subFingerIndexWithVariantsForMatch);

            TopDocs topHits = QuerySubFingers(config, subFingerIndexWithVariants);
            int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
            List<FingerprintSignature> fingerprints = getFingerprints(config, fingerIDs);
            if (config.Token != null && config.Token.IsCancellationRequested)
            {
                return;
            }

            FindPossibleMatch(config, fingerprints, subFingerIndexWithVariantsForMatch);
        }

        private void DoPlan1(QueryPlanConfig config)
        {
            // =============================================================================================================================
            // Plan 1
            // =============================================================================================================================
#if SHOWTRACEINFO
            Console.WriteLine("PLAN 1");
#endif
            List<HashIndex> matchSubFingerIndex = new List<HashIndex>();

            // Haal lijst op over max 12 seconden data (geeft bij goede kwaliteit geluid snel een goede hit)
            SubFingerIndexPlanNormal(config, 4, out matchSubFingerIndex);
            if (config.Token.IsCancellationRequested)
            {
                return;
            }
            TopDocs topHits = QuerySubFingers(config, matchSubFingerIndex);
            if (config.Token.IsCancellationRequested)
            {
                return;
            }
            int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
            List<FingerprintSignature> fingerprints = getFingerprints(config, fingerIDs);
            if (config.Token.IsCancellationRequested)
            {
                return;
            }
                        
            
            // Hint added 
            /*
            BooleanQuery query = new BooleanQuery();
            query.Add(new TermQuery(new Term("TITELNUMMERTRACK", "JK140158-0004")), Occur.SHOULD);
            query.Add(new TermQuery(new Term("FINGERID", "9169")), Occur.SHOULD);
            TopDocs tmpTopHits = fingerIndex.Search(query, 1);
            if (tmpTopHits.TotalHits > 0)
            {
                ScoreDoc match = tmpTopHits.ScoreDocs[0];
                Document doc = fingerIndex.Doc(match.Doc);
                FingerprintSignature fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                    doc.GetBinaryValue("LOOKUPHASHES"), Convert.ToInt64(doc.Get("DURATIONINMS")));
                fingerprints.Add(fs);
            }
            */

            FindPossibleMatch(config, fingerprints, matchSubFingerIndex);
        }

        private void DoPlan2(QueryPlanConfig config)
        {
            // =============================================================================================================================
            // Plan 2
            // =============================================================================================================================
#if SHOWTRACEINFO
            Console.WriteLine("PLAN 2");
#endif
            List<HashIndex> subFingerIndexWithVariants = new List<HashIndex>();

            int maxIndexSteps = Convert.ToInt32((config.fsQuery.SubFingerprintCount - (config.fsQuery.SubFingerprintCount % 256)) / 256);
            if (maxIndexSteps > 4)
            {
                maxIndexSteps = 4;
            }

            for (int indexStep = 0; indexStep < maxIndexSteps; indexStep++)
            {
                subFingerIndexWithVariants.Clear();
                config.SearchIteration = indexStep;

                SubFingerIndexPlanWithVariants(config, 256, indexStep, 1, 5, out subFingerIndexWithVariants);
                TopDocs topHits = QuerySubFingers(config, subFingerIndexWithVariants);
                int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
                List<FingerprintSignature> fingerprints = getFingerprints(config, fingerIDs);
                if (config.Token != null && config.Token.IsCancellationRequested)
                {
                    return;
                }
                
                
                // hint
                /*
                BooleanQuery query = new BooleanQuery();
                //query.Add(new TermQuery(new Term("TITELNUMMERTRACK", "JK140158-0004")), Occur.SHOULD);
                query.Add(new TermQuery(new Term("FINGERID", "1159967")), Occur.SHOULD); //JK190376-0006 / Photograph - Ed Sheeran
                TopDocs tmpTopHits = fingerIndex.Search(query, 1);
                if (tmpTopHits.TotalHits > 0)
                {
                    ScoreDoc match = tmpTopHits.ScoreDocs[0];
                    Document doc = fingerIndex.Doc(match.Doc);

                    FingerprintSignature fs;
                    if (useLookupHash)
                    {
                        fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                            doc.GetBinaryValue("LOOKUPHASHES"), Convert.ToInt64(doc.Get("DURATIONINMS")));
                    }
                    else
                    {
                        fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                            Convert.ToInt64(doc.Get("DURATIONINMS")), true);
                    }
                    fingerprints.Add(fs);
                }
                */
                
                FindPossibleMatch(config, fingerprints, subFingerIndexWithVariants);
                if (config.Token != null && config.Token.IsCancellationRequested)
                {
                    config.LowestBER = int.MaxValue;
                    return;
                }

                // When we find a "GOOD" hit we stop searching
                if (config.Hits.Count > 0 && config.LowestBER < 2000)
                {
                    // token.cancel will be called from calling fnction!
                    return;
                }
            } //for indexstep (with max 4)
        }


        /// <summary>
        /// Plan 1. Zoek eerst met zoveel mogelijk subfinger naar hits.
        ///         Kijk dan bij de eerste 25 hits of er een BER kleiner dan 2000 bij zit
        /// </summary>
        private void SubFingerIndexPlanNormal(QueryPlanConfig config,  int fingerBlockSize, out List<HashIndex> subFingerList)
        {
            DateTime startTime = DateTime.Now;
            subFingerList = new List<HashIndex>();
            int maxFingerIndex = -1;
            int savedindex = 0;
            for (int index = 0; (index < (config.fsQuery.SubFingerprintCount - 256) && index <= (fingerBlockSize * 256)); index++)
            {
                savedindex = index;
                uint h = config.fsQuery.SubFingerprint(index);
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(h, 0);
                if (bits < 13 || bits > 19) // 5 27  (10 22)
                {
                    // try further in the fingerprint
                    continue;
                }
                if ((int)index / 256 > maxFingerIndex)
                {
                    maxFingerIndex = (int)index / 256;
                }
                subFingerList.Add(new HashIndex(maxFingerIndex, index, h));
            } // for j

            config.SubFingerCreateQueryTime = config.SubFingerCreateQueryTime.Add(DateTime.Now - startTime);
        }

        private void SubFingerIndexPlanWithVariants(QueryPlanConfig config, int blockSize, int indexStep, int fingerBlockSize, byte maxVariantBit, out List<HashIndex> subFingerListWithVariants)
        {
            DateTime startTime = DateTime.Now;
            subFingerListWithVariants = new List<HashIndex>();

            int maxFingerIndex = -1;
            int savedindex = 0;
            for (int index = (indexStep * blockSize); (index < (config.fsQuery.SubFingerprintCount - blockSize) && index <= ((indexStep * blockSize) + (fingerBlockSize * blockSize))); index++)
            {
                savedindex = index;
                uint h = config.fsQuery.SubFingerprint(index);
                if ((int)index / 256 > maxFingerIndex)
                {
                    maxFingerIndex = (int)index / 256;
                }
                // this is used for hamming difference
                uint[] hashes = SubFingerVariants(maxVariantBit, h, config.fsQuery.Reliability(index));
                int count = 0;
                foreach (uint h2 in hashes)
                {
                    subFingerListWithVariants.Add(new HashIndex(maxFingerIndex, index, h2, count != 0));
                    count++;
                }
            } // for j

            // Make sure we don't return duplicates
            subFingerListWithVariants = subFingerListWithVariants.Distinct().ToList();
            config.SubFingerCreateQueryTime = config.SubFingerCreateQueryTime.Add(DateTime.Now - startTime);
        }

        /// <summary>
        /// Voeg eventuele variant hashes (naast de orginele hash) toe op basis van reliabilty info
        /// De return hashes is een "unieke" lijst
        /// 
        /// maxBitFlips=3 (dan max 8 verschillende waardes)
        /// maxBitFlips=10 dan (max 1024 waardes, dit is tevens het maximum)
        /// </summary>
        private uint[] SubFingerVariants(byte maxBitFlips, uint hash, byte[] r)
        {
            // doe maximaal 2^10 variant meenemen
            if (maxBitFlips > 10)
            {
                maxBitFlips = 10;
            }
            int countFlip = 0;
            List<SimpleBitVector32> bitVectors = new List<SimpleBitVector32>();
            bitVectors.Add(new SimpleBitVector32(hash));
            for (byte k = 0; k < maxBitFlips; k++)
            {
                for (byte i = 0; i < r.Length; i++)
                {
                    if (r[i] == k)
                    {
                        // flip bit
                        int len = bitVectors.Count;
                        for (int j = 0; j < len; j++)
                        {
                            SimpleBitVector32 bv = new SimpleBitVector32(bitVectors[j].UInt32Value);
                            bv.Toggle(i);
                            bitVectors.Add(bv);
                        }
                        countFlip++;
                        // Exit i loop want we zijn klaar met deze bit
                        break;
                    }
                } //for i
            } //for k

            // voeg orgineel en varianten toe, filter op minimaal aantal bits die zijn veranderd
            System.Collections.Hashtable table = new System.Collections.Hashtable(bitVectors.Count);
            foreach (SimpleBitVector32 bv in bitVectors)
            {
                uint h = bv.UInt32Value;
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(h, 0);
                if (bits <= 13 || bits >= 19) // 5 27  (10 22)
                {
                    // try further in the fingerprint
                    continue;
                }
                if (!table.ContainsKey(h))
                {
                    table.Add(h, h);
                }
            } //foreach

            return table.Keys.Cast<uint>().ToArray();
        }

        private TopDocs QuerySubFingers(QueryPlanConfig config, List<HashIndex> qSubFingerIndex)
        {
            DateTime startTime = DateTime.Now;
            // Zoek naar de fingerprint in de database
            BooleanQuery query = new BooleanQuery();
            for (int j = 0; j < qSubFingerIndex.Count - 1; j++)
            {
                Term t1 = new Term("SUBFINGER", qSubFingerIndex[j].Hash.ToString());
                query.Add(new TermQuery(t1), Occur.SHOULD);
            } //for j

            // Zoek naar de fingerprint in de database
            TopDocs topHits = subFingerIndex.Search(query, config.MaxTopDOCS);
            config.SubFingerQueryTime = config.SubFingerQueryTime.Add(DateTime.Now - startTime);

#if SHOWTRACEINFO
            Console.WriteLine(config.SearchStrategy.ToString() + " TotalHits=" + topHits.TotalHits.ToString());
#endif
            return topHits;
        }

        private int[] LuceneTopDocs2FingerIDs(QueryPlanConfig config, TopDocs topHits)
        {
            DateTime startTime = DateTime.Now;
            List<int> fingerIDs = new List<int>(System.Math.Min(topHits.TotalHits, config.MaxDOCSRetrieveResult));

            for (int j = 0; (j < topHits.TotalHits && j < config.MaxTopDOCS); j++)
            {
                ScoreDoc match = topHits.ScoreDocs[j];
                Document doc = subFingerIndex.Doc(match.Doc);

                // voor scannen kunnen we meer hits krijgen
                int fid = Convert.ToInt32(doc.Get("FINGERID"));
                if (j < config.MaxDOCSRetrieveResult)
                {
                    if (!fingerIDs.Contains(fid))
                    {
                        fingerIDs.Add(fid);
                    }
                }

#if SHOWTRACEINFO
                if (TestTrackID(fid))
                {
                    Console.WriteLine("HIT (Index=" + j.ToString() + ") [FINGERID=" + fid.ToString() + "] ");
                }
#endif
            } //for j
            config.SubFingerQueryTime = config.SubFingerQueryTime.Add(DateTime.Now - startTime);

            return fingerIDs.ToArray();
        }

        private void FindPossibleMatch(QueryPlanConfig config, List<FingerprintSignature> fingerprints, List<HashIndex> qSubFingerIndex)
        {
            DateTime starttime = DateTime.Now;
            ConcurrentDictionary<string, FingerprintHit> possibleHits = new ConcurrentDictionary<string, FingerprintHit>();
            config.LowestBER = int.MaxValue;
            int sLowestBER = config.LowestBER;
            // Nu gaan we op basis van fingerprints een match proberen te vinden
            int BERMatch = config.MinimalBERMatch;
            if (BERMatch < 0) // get default?
            {
                BERMatch = FingerprintSignature.BER(256);
            }

            int indexNumberInMatchList = 0;
            foreach (FingerprintSignature fsMatch in fingerprints)
            {
                indexNumberInMatchList++;
                string key = (string)fsMatch.Reference;

                // ---------------------------------------------------------------
                // Tel aantal hits dat we vonden in deze 256 subfingers.
                // handiog om te beoordelen hoe we komen tot een hit
                int subFingerCountHitInFingerprint = 0;
                foreach (HashIndex hi in qSubFingerIndex)
                {
                    if (fsMatch.IndexOf(hi.Hash).Length > 0)
                    {
                        subFingerCountHitInFingerprint++;
                    }
                } //foreach
                // ---------------------------------------------------------------

                bool foundMatch = false; // to exit loop from an innerloop
                int checkCount = 0;
                foreach (HashIndex hi in qSubFingerIndex)
                {
                    // Na 2 BER controles stoppen, is het toch niet
                    if (checkCount >= 2)
                    {
                        break;
                    }
                    int[] indexOfList = fsMatch.IndexOf(hi.Hash);
                    if (indexOfList.Length != 0)
                    {
                        uint[] fsQuery256 = config.fsQuery.Fingerprint(hi.Index);
                        checkCount++;

                        foreach (int indexOf in indexOfList)
                        {
                            uint[] fsMatch256 = fsMatch.Fingerprint(indexOf);
                            if (fsMatch256 == null)
                            {
                                // we zijn klaar
                                break;
                            }

                            int BER = FingerprintSignature.HammingDistance(fsMatch256, fsQuery256);
#if SHOWTRACEINFO
                            //System.Diagnostics.Trace.WriteLine(key + " BER=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec");
                            if (TestTrackID(key))
                            {
                                Console.WriteLine(key + " BER=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec");
                            }
#endif

                            if (BER < BERMatch)
                            {
                                // een mogelijk hit!
                                //System.Diagnostics.Trace.WriteLine(key + " BER(" + BERMatch.ToString() + ")=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec
                                if (config.Hits.ContainsKey(key))
                                {
                                    if (BER < config.Hits[key].BER)
                                    {
                                        config.Hits[key] = new FingerprintHit(fsMatch, indexOf, BER, indexNumberInMatchList, subFingerCountHitInFingerprint, config.SearchStrategy, config.SearchIteration);
                                    }
                                }
                                else
                                {
                                    config.Hits.TryAdd(key, new FingerprintHit(fsMatch, indexOf, BER, indexNumberInMatchList, subFingerCountHitInFingerprint, config.SearchStrategy, config.SearchIteration));
                                }
                                if (sLowestBER > BER)
                                {
                                    sLowestBER = BER;
                                }

                                foundMatch = true;
                                break;
                            }
                        } //foreach

                        // Als we hit gevonden hebben stop met verder zoeken
                        if (foundMatch)
                        {
                            break;
                        }
                    }
                } // foreach

                // Good enough BER
                if (sLowestBER < 1800)
                {
                    break;
                }
            }
            config.LowestBER = sLowestBER;
            config.MatchTime = config.MatchTime.Add(DateTime.Now - starttime);
        }

        private void FindPossibleMatchParallel(QueryPlanConfig config, List<FingerprintSignature> fingerprints, List<HashIndex> qSubFingerIndex)
        {
            DateTime starttime = DateTime.Now;
            ConcurrentDictionary<string, FingerprintHit> possibleHits = new ConcurrentDictionary<string, FingerprintHit>();
            config.LowestBER = int.MaxValue;
            int sLowestBER = config.LowestBER;
            // Nu gaan we op basis van fingerprints een match proberen te vinden
            int BERMatch = config.MinimalBERMatch;
            if (BERMatch < 0) // get default?
            {
                BERMatch = FingerprintSignature.BER(256);
            }

            Parallel.ForEach(fingerprints, (FingerprintSignature fsMatch, ParallelLoopState foreachFingerprintState) =>
            {
                string key = (string)fsMatch.Reference;
#if SHOWTRACEINFO
                if (TestTrackID(key))
                {
                    int hitCount = 0;
                    foreach (HashIndex hi in qSubFingerIndex)
                    {
                        if (fsMatch.IndexOf(hi.Hash).Length > 0)
                        {
                            hitCount++;
                        }
                    } //foreach

                    Console.WriteLine("Probing " + key +  " (" + hitCount.ToString() + ")");
                }
#endif

                bool foundMatch = false; // to exit loop from an innerloop
                int checkCount = 0;
                //foreach (HashIndex hi in qSubFingerIndex)
                Parallel.ForEach(qSubFingerIndex, (HashIndex hi, ParallelLoopState foreachSubFingerState) =>
                {
                    // Na 2 BER controles stoppen, is het toch niet
                    if (checkCount >= 2)
                    {
                        foreachSubFingerState.Stop();
                        return;
                    }
                    int[] indexOfList = fsMatch.IndexOf(hi.Hash);
                    if (indexOfList.Length != 0)
                    {
                        uint[] fsQuery256 = config.fsQuery.Fingerprint(hi.Index);
                        checkCount++;

                        foreach (int indexOf in indexOfList)
                        {
                            uint[] fsMatch256 = fsMatch.Fingerprint(indexOf);
                            if (fsMatch256 == null)
                            {
                                // we zijn klaar
                                break;
                            }

                            int BER = FingerprintSignature.HammingDistance(fsMatch256, fsQuery256);
#if SHOWTRACEINFO
                            //System.Diagnostics.Trace.WriteLine(key + " BER=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec");
                            if (TestTrackID(key))
                            {
                                Console.WriteLine(key + " BER=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec");
                            }
#endif

                            if (BER < BERMatch)
                            {
                                // een mogelijk hit!
                                //System.Diagnostics.Trace.WriteLine(key + " BER(" + BERMatch.ToString() + ")=" + BER.ToString() + " Timeindex=" + ((indexOf * 11.6) / 1000).ToString("#0.000") + "sec
                                if (config.Hits.ContainsKey(key))
                                {
                                    if (BER < config.Hits[key].BER)
                                    {
                                        config.Hits[key] = new FingerprintHit(fsMatch, indexOf, BER, -1, -1, config.SearchStrategy, config.SearchIteration);
                                    }
                                }
                                else
                                {
                                    config.Hits.TryAdd(key, new FingerprintHit(fsMatch, indexOf, BER, -1, -1, config.SearchStrategy, config.SearchIteration));
                                }
                                if (sLowestBER > BER)
                                {
                                    sLowestBER = BER;
                                }

                                foundMatch = true;
                                break;
                            }
                        } //foreach

                        // Als we hit gevonden hebben stop met verder zoeken
                        if (foundMatch)
                        {
                            foreachSubFingerState.Stop();
                            return;
                        }
                    }
                }); //Parallel.Foreach

                // Good enough BER
                if (sLowestBER < 1800)
                {
                    foreachFingerprintState.Stop();
                    return;
                }
            }); //Parallel.Foreach
            config.LowestBER = sLowestBER;
            config.MatchTime = config.MatchTime.Add(DateTime.Now - starttime);
        }

        private void AddProbeEntry(Dictionary<int, int> probeHit, int docID, int score)
        {
            if (probeHit.ContainsKey(docID))
            {
                probeHit[docID] += -1;
            }
            else
            {
                probeHit.Add(docID, score);
            }
        }

        #region Retrieve complete fingerprint (Lucene/MySQL code)
        private delegate List<FingerprintSignature> GetFingerprints(QueryPlanConfig config, int[] fingerIDList);
        private GetFingerprints getFingerprints;

        /*
        private List<FingerprintSignature> GetFingerprintsLucene(QueryPlanConfig config, int[] fingerIDList)
        {
            DateTime startTime = DateTime.Now;
            System.Collections.Concurrent.ConcurrentBag<FingerprintSignature> fingerBag = new System.Collections.Concurrent.ConcurrentBag<FingerprintSignature>();

            if (fingerIDList.Length > 0)
            {
                // Nu de geselecteerde fingeprints "laden" uit de database
                BooleanQuery query = new BooleanQuery();
                System.Collections.Hashtable hTable = new System.Collections.Hashtable(fingerIDList.Length);
                int count = 0;
                foreach (int id in fingerIDList)
                {
                    query.Add(new TermQuery(new Term("FINGERID", id.ToString())), Occur.SHOULD);
                    hTable.Add(id, count);
                    count++;
                } //foreach

                TopDocs topHits = fingerIndex.Search(query, fingerIDList.Length);
                Parallel.For(0, System.Math.Min(fingerIDList.Length, topHits.ScoreDocs.Length), index =>
                {
                    ScoreDoc match = topHits.ScoreDocs[index];
                    Document doc = fingerIndex.Doc(match.Doc);

                    FingerprintSignature fs;
                    if (useLookupHash)
                    {
                        fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                            doc.GetBinaryValue("LOOKUPHASHES"), Convert.ToInt64(doc.Get("DURATIONINMS")));
                    }
                    else
                    {
                        fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                            Convert.ToInt64(doc.Get("DURATIONINMS")), true);
                    }

                    int fingerID = Convert.ToInt32(doc.Get("FINGERID"));
                    fs.Tag = hTable[fingerID];

                    fingerBag.Add(fs);
                });
            }

            List<FingerprintSignature> result = fingerBag.OrderBy(e => (int)e.Tag)
                                                         .ToList();
            config.FingerLoadTime = DateTime.Now - startTime;

            return result;
        }
        */

        private List<FingerprintSignature> GetFingerprintsMySQL(QueryPlanConfig config, int[] fingerIDList)
        {
            if (fingerIDList.Length == 0)
            {
                return new List<FingerprintSignature>();
            }

            DateTime startTime = DateTime.Now;
            System.Collections.Concurrent.ConcurrentBag<FingerprintSignature> fingerBag = new System.Collections.Concurrent.ConcurrentBag<FingerprintSignature>();

            using (MySql.Data.MySqlClient.MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
            {
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("SELECT *\r\n");
                sb.Append("FROM   TITELNUMMERTRACK_ID AS T1,\r\n");
                sb.Append("       SUBFINGERID AS T2\r\n");
                sb.Append("WHERE  T1.TITELNUMMERTRACK_ID = T2.TITELNUMMERTRACK_ID\r\n");
                sb.Append("AND    T1.TITELNUMMERTRACK_ID IN (\r\n");
                int count = 0;
                System.Collections.Hashtable hTable = new System.Collections.Hashtable(fingerIDList.Length);
                foreach (int id in fingerIDList)
                {
                    if (count > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(id.ToString());

                    hTable.Add(id, count);
                    count++;
                }
                sb.Append(')');

                MySql.Data.MySqlClient.MySqlCommand command = new MySql.Data.MySqlClient.MySqlCommand(sb.ToString(), conn);
                command.CommandTimeout = 60;

                MySql.Data.MySqlClient.MySqlDataAdapter adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(command);
                System.Data.DataSet ds = new System.Data.DataSet();
                adapter.Fill(ds);
                if (ds.Tables.Count > 0)
                {
                    foreach (System.Data.DataRow row in ds.Tables[0].Rows)
                    {
                        FingerprintSignature fs;
                        if (useLookupHash)
                        {
                            fs = new FingerprintSignature(row["TITELNUMMERTRACK"].ToString(), Convert.ToInt64(row["TITELNUMMERTRACK_ID"]),
                                (byte[])row["SIGNATURE"], (byte[])row["LOOKUPHASHES"], Convert.ToInt64(row["DURATIONINMS"]));
                        }
                        else
                        {
                            fs = new FingerprintSignature(row["TITELNUMMERTRACK"].ToString(), Convert.ToInt64(row["TITELNUMMERTRACK_ID"]), 
                                (byte[])row["SIGNATURE"], Convert.ToInt64(row["DURATIONINMS"]), true);
                        }

                        int fingerID = Convert.ToInt32(row["TITELNUMMERTRACK_ID"]);
                        fs.Tag = hTable[fingerID];

                        fingerBag.Add(fs);
                    }
                }
            }

            List<FingerprintSignature> result = fingerBag.OrderBy(e => (int)e.Tag)
                                                         .ToList();
            config.FingerLoadTime = DateTime.Now - startTime;

            return result;
        }
        #endregion

        private bool TestTrackID(int key)
        {
            List<int> test = new List<int>();
            /*
            test.Add("JK198404-0006"); //Juanes - A dios le pido
            test.Add("JK175683-0004"); //Juanes - A dios le pido

            test.Add("JK173821-0003"); //Michael Jackson - Billie Jean
            test.Add("JK172328-0004"); //Michael Jackson - Billie Jean
            test.Add("JK193547-0041"); //Michael Jackson - Billie Jean

            test.Add("JK193551-0029"); //Clown - Emeli Sandé
            test.Add("JK195747-0025"); //Clown - Emeli Sandé
            test.Add("JK185567-0005"); //Clown - Emeli Sandé
            test.Add("JK179330-0011"); //Clown - Emeli Sandé
            test.Add("JK173346-0005"); //Clown - Emeli Sandé

            test.Add("JK185766-0003"); //Ed Sheeran - Give me love
            test.Add("JK172068-0012"); //Ed Sheeran - Give me love
            
            test.Add("JK189782-0003"); //Sam Smith - Stay with me
            test.Add("JK194105-0038"); //Sam Smith - Stay with me
            test.Add("JK193550-0032"); //Sam Smith - Stay with me
            test.Add("JK194591-0027"); //Sam Smith - Stay with me
            test.Add("JK196924-0008"); //Sam Smith - Stay with me
            test.Add("JK192524-0005"); //Sam Smith - Stay with me

            test.Add("JK191592-0047"); //Basto! - Gregory's theme
            test.Add("JK140158-0023"); //Normaal - Top of the bult
            test.Add("JK140158-0004"); //Normaal - Moar as 't mot
            
            test.Add("JK178131-0010"); //??? - Holes
            test.Add("JK180877-0005"); //??? - Holes
            test.Add("JK181109-0016"); //??? - Holes

            test.Add("JK90000-0003"); //Madonna - Ray of light

            test.Add("JK186824-0003"); //Dotan - Hungry
*/
            test.Add(1159967); //JK190376-0006 / Ed Sheeran - Photograph
            test.Add(47806); //JK198379-0001 / 548 Pi - Adam Lambert - Ghost Town
            test.Add(48388); //JK199128-0034 / Walk on water - Cau.wav
            return test.Contains(key);
        }

        private bool TestTrackID(string key)
        {
            switch (key.ToUpper())
            {
                case "JK190376-0006":
                    return TestTrackID(1159967);
                case "JK198379-0001":
                    return TestTrackID(47806);
                case "JK199128-0034":
                    return TestTrackID(48388);
            }

            return false;
        }

        #region Helper functions
        private static byte[] GetBytes(string str)
        {
            if (str == null)
            {
                return new byte[0];
            }

            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static string GetString(byte[] bytes)
        {
            if (bytes == null)
            {
                return string.Empty;
            }

            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }
        #endregion


        private struct HashIndex
        {
            public int FingerIndex;
            public int Index;
            public uint Hash;
            public bool Variant;

            public HashIndex(int fingerIndex, int index, uint hash)
            {
                this.FingerIndex = fingerIndex;
                this.Index = index;
                this.Hash = hash;
                this.Variant = false;
            }

            public HashIndex(int fingerIndex, int index, uint hash, bool variant)
            {
                this.FingerIndex = fingerIndex;
                this.Index = index;
                this.Hash = hash;
                this.Variant = variant;
            }

            public override bool Equals(object obj)
            {
                return ((HashIndex)obj).Hash == Hash;
            }

            public override int GetHashCode()
            {
                return unchecked((int)Hash);
            }
        }

        private struct FingerprintHit
        {
            public FingerprintSignature Fingerprint;
            public int BER;
            public int TimeIndex;
            public int IndexNumberInMatchList;
            public int SubFingerCountHitInFingerprint;
            public SearchStrategy SearchStrategy;
            public int SearchIteration;


            public FingerprintHit(FingerprintSignature fingerprint, int timeIndex, int ber, int indexNumberInMatchList, int subFingerCountHitInFingerprint, SearchStrategy searchStrategy, int searchIteration)
            {
                this.Fingerprint = fingerprint;
                this.TimeIndex = timeIndex;
                this.BER = ber;
                this.IndexNumberInMatchList = indexNumberInMatchList;
                this.SubFingerCountHitInFingerprint = subFingerCountHitInFingerprint;
                this.SearchStrategy = searchStrategy;
                this.SearchIteration = searchIteration;
            }
        }

        private class QueryPlanConfig
        {
            public SearchStrategy SearchStrategy = SearchStrategy.NotSet;

            public int MaxTopDOCS = 10; // how many documents need to be return
            public int MaxDOCSRetrieveResult = 10;
            public int LowestBER = int.MaxValue;
            public int MinimalBERMatch = -1; // 
            public CancellationTokenSource Token;
            public FingerprintSignature fsQuery = null;

            public TimeSpan SubFingerCreateQueryTime = TimeSpan.FromTicks(0);
            public TimeSpan SubFingerQueryTime = TimeSpan.FromTicks(0);
            public TimeSpan FingerLoadTime = TimeSpan.FromTicks(0);
            public TimeSpan MatchTime = TimeSpan.FromTicks(0);

            public int SearchIteration = 0;

            public ConcurrentDictionary<string, FingerprintHit> Hits = new ConcurrentDictionary<string, FingerprintHit>();
        }       
    }


/*

            /// <summary>
            /// Plan 2. Niks gevonden.
            ///         Ga opnieuw 1ste 256 finger blok en doe nu stappen van 512 subfingers
            ///         Neem nu ook "varianten" mee
            ///         
            ///         fingerBlock = 2 (bij plan 1 is het 4)
            /// </summary>
            private void SubFingerIndexPlan2(FingerprintSignature fsQuery, int indexStep, int fingerBlock, out List<HashIndex> subFingerList, out List<HashIndex> subFingerListWithVariants)
            {
                subFingerList = new List<HashIndex>();
                subFingerListWithVariants = new List<HashIndex>();

                int maxFingerIndex = -1;
                for (int index = (indexStep * 256); (index < (fsQuery.SubFingerprintCount - 256) && index <= ((indexStep * 256) + (fingerBlock * 256))); index++)
                {
                    uint h = fsQuery.SubFingerprint(index);
                    int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(h, 0);
                    if (bits < 10 || bits > 22) // 5 27  (10 22)
                    {
                        // try further in the fingerprint
                        continue;
                    }
                    if ((int)index / 256 > maxFingerIndex)
                    {
                        maxFingerIndex = (int)index / 256;
                    }

                    // this is used for hamming difference
                    subFingerList.Add(new HashIndex(maxFingerIndex, index, h));
                    uint[] hashes = SubFingerVariants(3, h, fsQuery.Reliability(index));
                    int count = 0;
                    foreach (uint h2 in hashes)
                    {
                        subFingerListWithVariants.Add(new HashIndex(maxFingerIndex, index, h2, count != 0));
                        count++;
                    }
                } // for j
            }

            private void DoPlan2(QueryPlanConfig config)
            {
                // =============================================================================================================================
                // Plan 2
                // =============================================================================================================================
    #if SHOWTRACEINFO
                Console.WriteLine("PLAN 2");
    #endif

                List<HashIndex> querySubFingerIndex = new List<HashIndex>();
                List<HashIndex> matchSubFingerIndex = new List<HashIndex>();

                int maxIndexSteps = Convert.ToInt32((config.fsQuery.SubFingerprintCount - (config.fsQuery.SubFingerprintCount % 256)) / 256);
                if (maxIndexSteps > 4)
                {
                    maxIndexSteps = 4;
                }
                // als plan1 niet slaagt heeft het geen zin om indexstep 0 en 1 te doen
                for (int indexStep = 2; indexStep < maxIndexSteps; indexStep++)
                {
                    matchSubFingerIndex.Clear();
                    querySubFingerIndex.Clear();

                    SubFingerIndexPlan2(config.fsQuery, indexStep, 2, out matchSubFingerIndex, out querySubFingerIndex);
                    TopDocs topHits = QuerySubFingers(config, matchSubFingerIndex);
                    int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
                    List<FingerprintSignature> fingerprints = GetFingerprints(config, fingerIDs);
                    if (config.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    FindPossibleMatch(config, fingerprints, matchSubFingerIndex);
                    if (config.Token.IsCancellationRequested)
                    {
                        config.LowestBER = int.MaxValue;
                        return;
                    }

                    // When we find a "GOOD" hit we stop searching
                    if (config.Hits.Count > 0 && config.LowestBER < 2000)
                    {
                        // token.cancel will be called from calling fnction!
                        return;
                    }
                } //for indexstep (with max 4)
            }

            private void DoPlan3(QueryPlanConfig config)
            {
                // =============================================================================================================================
                // Plan 3
                // =============================================================================================================================
    #if SHOWTRACEINFO
                Console.WriteLine("PLAN 3");
    #endif
                List<HashIndex> querySubFingerIndex = new List<HashIndex>();
                List<HashIndex> matchSubFingerIndex = new List<HashIndex>();

                SubFingerIndexPlan2(config.fsQuery, 0, 6, out matchSubFingerIndex, out querySubFingerIndex);
                if (config.Token.IsCancellationRequested)
                {
                    return;
                }
                TopDocs topHits = QuerySubFingers(config, querySubFingerIndex);
                if (!config.Token.IsCancellationRequested && topHits.TotalHits > 30000)
                {
                    int[] fingerIDs = LuceneTopDocs2FingerIDs(config, topHits);
                    if (config.Token.IsCancellationRequested)
                    {
                        return;
                    }
                
                    List<FingerprintSignature> fingerprints = GetFingerprints(config, fingerIDs);
                    if (config.Token.IsCancellationRequested)
                    {
                        return;
                    }
                    FindPossibleMatch(config, fingerprints, querySubFingerIndex);
                    if (config.Token.IsCancellationRequested)
                    {
                        return;
                    }
                
                
                    for (int i = 0; i < (int)(fingerIDs.Length / 25); i++)
                    {
                        int startID = i * 25;
                        int endID = ((i + 1) * 25) - 1;
                        if (endID >= fingerIDs.Length)
                        {
                            endID = fingerIDs.Length - 1;
                        }
                        int[] fingerID25 = new int[(endID - startID) + 1];
                        Buffer.BlockCopy(fingerIDs, startID * 4, fingerID25, 0, fingerID25.Length * 4);

                        List<FingerprintSignature> fingerprints = GetFingerprints(config, fingerID25);
                    
                        BooleanQuery query = new BooleanQuery();
                        query.Add(new TermQuery(new Term("TITELNUMMERTRACK", "JK140158-0004")), Occur.SHOULD);
                        query.Add(new TermQuery(new Term("FINGERID", "9169")), Occur.SHOULD);
                        TopDocs tmpTopHits = fingerIndex.Search(query, 1);
                        if (tmpTopHits.TotalHits > 0)
                        {
                            ScoreDoc match = tmpTopHits.ScoreDocs[0];
                            Document doc = fingerIndex.Doc(match.Doc);
                            FingerprintSignature fs = new FingerprintSignature(doc.Get("TITELNUMMERTRACK"), doc.GetBinaryValue("FINGERPRINT"),
                                doc.GetBinaryValue("LOOKUPHASHES"), Convert.ToInt64(doc.Get("DURATIONINMS")));
                            fingerprints.Add(fs);
                        }
                    
                        if (config.Token.IsCancellationRequested)
                        {
                            return;
                        }
                        FindPossibleMatch(config, fingerprints, querySubFingerIndex);
                        if (config.Token.IsCancellationRequested)
                        {
                            return;
                        }
                    } //for  
                }
            }

    */
}