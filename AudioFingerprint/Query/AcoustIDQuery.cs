//#define SHOWTRACEINFO
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcoustID;
using AcoustID.Audio;
using AudioFingerprint;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using MySql.Data.MySqlClient;


namespace AudioFingerprint
{
    public class AcoustIDQuery
    {
        private IndexSearcher indexFingerLookup;

        public AcoustIDQuery(IndexSearcher fingerprintIndex)
        {
            // Chose one of the methods top find the complete fingerprint
            // (Only one method for this moment)
            // - GetFingerprintsMySQL
            getFingerprints = GetFingerprintsMySQL;

            this.indexFingerLookup = fingerprintIndex;

            // Forceer dat setup table aangemaakt wordt
            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();
        }

        public AcoustID.FingerprintAcoustID MakeAcoustIDFingerFromAudio(string filename)
        {
            // resample to 11025Hz
            IAudioDecoder decoder = new BassDecoder();
            try
            {
                decoder.Load(filename);

                ChromaContext context = new ChromaContext();

                context.Start(decoder.SampleRate, decoder.Channels);
                decoder.Decode(context.Consumer, 120);
                if (context.Finish())
                {
                    FingerprintAcoustID fingerprint = new FingerprintAcoustID();
                    fingerprint.Reference = "";
                    fingerprint.DurationInMS = (long)decoder.Duration * 1000;
                    fingerprint.SignatureInt32 = context.GetRawFingerprint();

                    return fingerprint;
                }
            }
            catch (Exception e)
            {
                // problem, propably with file
                Console.Error.WriteLine(e.ToString());
            }
            finally
            {
                decoder.Dispose();
            }

            return null;
        }


        public Resultset MatchAudioFingerprint(FingerprintAcoustID fsQuery)
        {
            DateTime startTime = DateTime.Now;
            BooleanQuery.MaxClauseCount = 600000;
            Resultset result = new Resultset();
            result.Algorithm = FingerprintAlgorithm.AcoustIDFingerprint;

            int[] query = fsQuery.AcoustID_Extract_Query;
            if (query.Length <= 0)
            {
                goto exitFunc;
            }

            // Vuurt nu query af op lucene index
            DateTime dtStart = DateTime.Now;
            TopDocs topHits = QuerySubAcoustIDFingers(query);
            int[] titelnummertrackIDs = LuceneTopDocs2TitelnummertrackIDs(topHits);
            result.FingerQueryTime = (DateTime.Now - dtStart);

            if (titelnummertrackIDs.Length == 0)
            {
                goto exitFunc;
            }

            dtStart = DateTime.Now;
            List<FingerprintAcoustID> fingerprints = GetFingerprintsMySQL(titelnummertrackIDs);
            result.FingerLoadTime = (DateTime.Now - dtStart);

            dtStart = DateTime.Now;
            ConcurrentDictionary<string, FingerprintHit> hits = FindPossibleMatch(fsQuery, fingerprints);
            result.MatchTime = (DateTime.Now - dtStart);

            // Geef resultaat terug
            List<ResultEntry> resultEntries = hits.OrderByDescending(e => e.Value.Score)
                                                  .Select(e => new ResultEntry
                                                  {
                                                      Reference = e.Value.Fingerprint.Reference,
                                                      Similarity = Convert.ToInt32(e.Value.Score * 100), // acoustic id math from 0 to 100%
                                                      // Dummy settings
                                                      TimeIndex = -1, // match is on complete track (or al least first 120 seconds!)
                                                      IndexNumberInMatchList = e.Value.IndexNumberInMatchList,
                                                      SubFingerCountHitInFingerprint = 0,
                                                      SearchStrategy = SearchStrategy.NotSet,
                                                      SearchIteration = 0
                                                  })
                                                  .ToList();
            result.ResultEntries = resultEntries;
exitFunc:
            result.QueryTime = (DateTime.Now - startTime);
            return result;
        }


        public struct DuplicateHit
        {
            public float Score;
            public string TitelnummertrackID;
        }

        const int MaxDOCSRetrieveResult = 50;
        private TopDocs QuerySubAcoustIDFingers(int[] qSubFingerIndex)
        {
            DateTime startTime = DateTime.Now;
            // Zoek naar de fingerprint in de database
            BooleanQuery query = new BooleanQuery();
            for (int j = 0; j < qSubFingerIndex.Length; j++)
            {
                Term t1 = new Term("FINGERID", qSubFingerIndex[j].ToString());
                query.Add(new TermQuery(t1), Occur.SHOULD);
            } //for j

            // Zoek naar de fingerprint in de database
            TopDocs topHits = indexFingerLookup.Search(query, MaxDOCSRetrieveResult);

            return topHits;
        }

        private int[] LuceneTopDocs2TitelnummertrackIDs(TopDocs topHits)
        {
            DateTime startTime = DateTime.Now;
            int[] titelnummertrackIDs = new int[System.Math.Min(topHits.TotalHits, MaxDOCSRetrieveResult)];

            for (int j = 0; (j < topHits.TotalHits && j < MaxDOCSRetrieveResult); j++)
            {
                ScoreDoc match = topHits.ScoreDocs[j];
                Document doc = indexFingerLookup.Doc(match.Doc);

                // voor scannen kunnen we meer hits krijgen
                int tid = Convert.ToInt32(doc.Get("TITELNUMMERTRACK_ID"));
                if (j < MaxDOCSRetrieveResult)
                {
                    titelnummertrackIDs[j] = tid;
                }
            } //for j

            return titelnummertrackIDs;
        }

        private ConcurrentDictionary<string, FingerprintHit> FindPossibleMatch(FingerprintAcoustID fsQuery, List<FingerprintAcoustID> fingerprints)
        {
            DateTime starttime = DateTime.Now;
            ConcurrentDictionary<string, FingerprintHit> possibleHits = new ConcurrentDictionary<string, FingerprintHit>();

            int indexNumberInMatchList = 0;
            int[] fsQueryInts = fsQuery.SignatureInt32;
            foreach (FingerprintAcoustID fsMatch in fingerprints)
            {
                indexNumberInMatchList++;

                // ---------------------------------------------------------------
                // Tel aantal hits dat we vonden
                int fingerCountHitInFingerprint = 0;
                foreach (int hash in fsQueryInts)
                {
                    if (fsMatch.IndexOf(hash).Length > 0)
                    {
                        fingerCountHitInFingerprint++;
                    }
                } //foreach
                // ---------------------------------------------------------------

                float score = FingerprintAcoustID.MatchFingerprint(fsMatch, fsQuery, 0);
                if (score >= 0.60f) // 0.60 zoals ik terugvond levert te slechte resultaten op (engelse en nederlandse versie zijn dan hetzelfde)
                {
                    if (System.Math.Abs(100 - ((fsMatch.DurationInMS * 100) / fsQuery.DurationInMS)) < 15)
                    {
                        possibleHits.TryAdd(fsMatch.Reference.ToString(), new FingerprintHit(fsMatch, score, indexNumberInMatchList, fingerCountHitInFingerprint));
                    }
                }
            }

            return possibleHits;
        }

        #region Retrieve complete fingerprint (Lucene/MySQL code)

        private delegate List<FingerprintAcoustID> GetFingerprints(int[] fingerIDList);
        private GetFingerprints getFingerprints;

        private List<FingerprintAcoustID> GetFingerprintsMySQL(int[] fingerIDList)
        {
            DateTime startTime = DateTime.Now;
            System.Collections.Concurrent.ConcurrentBag<FingerprintAcoustID> fingerBag = new System.Collections.Concurrent.ConcurrentBag<FingerprintAcoustID>();

            using (MySql.Data.MySqlClient.MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
            {
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("SELECT *\r\n");
                sb.Append("FROM   FINGERID AS T1,\r\n");
                sb.Append("       TITELNUMMERTRACK_ID AS T2\r\n");
                sb.Append("WHERE  T1.TITELNUMMERTRACK_ID = T2.TITELNUMMERTRACK_ID\r\n");
                sb.Append("AND    T2.TITELNUMMERTRACK_ID IN (\r\n");

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
                        FingerprintAcoustID fs = new FingerprintAcoustID();
                        fs.Reference = row["TITELNUMMERTRACK"].ToString();
                        fs.Signature = (byte[])row["SIGNATURE"];
                        fs.DurationInMS = Convert.ToInt64(row["DURATIONINMS"]);

                        int titelnummertrackID = Convert.ToInt32(row["TITELNUMMERTRACK_ID"]);
                        fs.Tag = hTable[titelnummertrackID];

                        fingerBag.Add(fs);
                    }
                }
            }

            List<FingerprintAcoustID> result = fingerBag.OrderBy(e => (int)e.Tag)
                                                        .ToList();

            return result;
        }
        
        #endregion

        private struct FingerprintHit
        {
            public FingerprintAcoustID Fingerprint;
            public float Score;
            public int IndexNumberInMatchList;
            public int FingerCountHitInFingerprint;


            public FingerprintHit(FingerprintAcoustID fingerprint, float score, int indexNumberInMatchList, int fingerCountHitInFingerprint)
            {
                this.Fingerprint = fingerprint;
                this.Score = score;
                this.IndexNumberInMatchList = indexNumberInMatchList;
                this.FingerCountHitInFingerprint = fingerCountHitInFingerprint;
            }
        }

    }
}
