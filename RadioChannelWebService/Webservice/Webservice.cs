#region License
// Copyright (c) 2015-2017 Stichting Centrale Discotheek Rotterdam.
// 
// website: https://www.muziekweb.nl
// e-mail:  info@muziekweb.nl
//
// This code is under MIT licence, you can find the complete file here: 
// LICENSE.MIT
#endregion
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Linq;
using AudioFingerprint;
using AudioFingerprint.Audio;
using CDR;
using CDR.Logging;
using CDR.WebService;

namespace RadioChannelWebService
{
    // NOTE: If you change the class name "Service" here, you must also update the reference to "Service" in App.config and in the associated .svc file.

    // Per call the host will create an instance for your service.
    // This mode give better performance incase the client still opened and not active rather than the prev. mode.
    // [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public partial class Webservice : IWebserviceContract
    {
        #region Administation stuff

        // We starten default in offline mode!
        private static bool forceServiceOffline = true;
        public static bool ForceServiceOffline
        {
            get
            {
                return forceServiceOffline;
            }
            set
            {
                forceServiceOffline = value;
            }
        }


        /// <summary>
        /// </summary>
        /// <returns></returns>
        public Message Version()
        {
            string errorMessage;
            if (!UserAccountManager.HasAccess(UserAccountManager.CurrentAccessGroup, out errorMessage, true))
            {
                return ServiceHelper.SendErrorMessage(ResultErrorCode.AuthorizationError, errorMessage);
            }
            UserAccountManager.GlobalNumberOfRequestsInc();

            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            XElement xResult = ServiceHelper.CreateRoot(ResultErrorCode.OK);
            xResult.Add(new XElement("Version", String.Format("{0:0}.{1:00}.{2:0000}", version.Major, version.Minor, version.Build)));
            xResult.Add(new XElement("ClientIP", ServiceHelper.CurrentClientIP));
            xResult.Add(new XElement("AuthenticatedUsername", UserAccountManager.CurrentBasicAuthenticatedUsername));
            xResult.Add(new XElement("ServerName", Environment.MachineName));

            return Message.CreateMessage(MessageVersion.None, "", new XElementBodyWriter(xResult));
        }
        #endregion


        #region Fingerprinting

        public Message FingerprintRecognize(Stream stream)
        {
            string errorMessage;
            if (!UserAccountManager.HasAccess(UserAccountManager.CurrentAccessGroup, out errorMessage, true))
            {
                return ServiceHelper.SendErrorMessage(ResultErrorCode.AuthorizationError, errorMessage);
            }
            UserAccountManager.GlobalNumberOfRequestsInc();

            XElement xResult = null;
            try
            {
                QueryString qs = new QueryString(stream);
                string fingerprint = qs["Fingerprint"];
                string reliabilty = qs["Reliabilities"];

                using (FingerprintSignature fsQuery = new FingerprintSignature("UNKNOWN", 0, System.Convert.FromBase64String(fingerprint), 0, true))
                {
                    fsQuery.Reliabilities = System.Convert.FromBase64String(reliabilty);

                    LuceneIndex subFingerIndex = LuceneIndexes.IndexSubFingerprint;
                    try
                    {
                        // We moeten hier al locken aangezien de audiofingerprint library geen weet heeft van onze
                        // LuceneIndex object
                        subFingerIndex.Lock();
                        using (SubFingerprintQuery query = new SubFingerprintQuery(subFingerIndex.Index))
                        {
                            Resultset answer = query.MatchAudioFingerprint(fsQuery, 2300);

                            if (answer != null)
                            {
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.OK);

                                xResult.Add(new XElement("RecognizeResult",
                                    new XAttribute("RecognizeCode", 0),
                                    "OK"));

                                XElement xTimeStatistics = new XElement("TimeStatistics");
                                xTimeStatistics.Add(new XElement("TotalQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.QueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("SubFingerQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerQueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("FingerLoadTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerLoadTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("MatchTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.MatchTime.TotalMilliseconds));
                                xResult.Add(xTimeStatistics);

                                XElement xFingerTracks = new XElement("FingerTracks",
                                    new XAttribute("Count", 0));
                                xResult.Add(xFingerTracks);
                                if (answer.ResultEntries.Count > 0)
                                {
                                    // Gets a NumberFormatInfo associated with the en-US culture.
                                    System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
                                    nfi.CurrencySymbol = "€";
                                    nfi.CurrencyDecimalDigits = 2;
                                    nfi.CurrencyDecimalSeparator = ".";
                                    nfi.NumberGroupSeparator = "";
                                    nfi.NumberDecimalSeparator = ".";

                                    Dictionary<string, ResultEntry> dict = new Dictionary<string, ResultEntry>();
                                    int count = 0;
                                    foreach (ResultEntry item in answer.ResultEntries)
                                    {
                                        XElement xFingerTrack = new XElement("FingerTrack");
                                        xFingerTracks.Add(xFingerTrack);

                                        xFingerTrack.Add(new XAttribute("BER", item.Similarity.ToString()));
                                        xFingerTrack.Add(new XElement("DetectPosition",
                                            new XAttribute("Unit", "ms"),
                                            new XAttribute("InSec", string.Format(nfi, "{0:#0.000}", (item.Time.TotalMilliseconds / 1000))),
                                            (int)item.Time.TotalMilliseconds));

                                        XElement xSearchStrategy = new XElement("SearchStrategy");
                                        xFingerTrack.Add(xSearchStrategy);
                                        xSearchStrategy.Add(new XElement("IndexNumberInMatchList", item.IndexNumberInMatchList));
                                        xSearchStrategy.Add(new XElement("SubFingerCountHitInFingerprint", item.SubFingerCountHitInFingerprint));
                                        xSearchStrategy.Add(new XElement("SearchName", item.SearchStrategy.ToString()));
                                        xSearchStrategy.Add(new XElement("SearchIteration", item.SearchIteration));

                                        xFingerTrack.Add(new XElement("Reference", item.Reference.ToString()));
                                        xFingerTrack.Add(new XElement("FingerTrackID", item.FingerTrackID.ToString()));
                                        count++;
                                    } //foreach
                                    xFingerTracks.Attribute("Count").Value = count.ToString();
                                }

                            }
                            else
                            {
                                // Query geeft null waarde terug. Betekent dat er iets goed fout is gegaan                    
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.FAILED);
                            }

                            if (xResult != null)
                            {
                                return Message.CreateMessage(MessageVersion.None, "", new XElementBodyWriter(xResult));
                            }

                            // Als we hier komen dan hebben we geen resultaat
                            return ServiceHelper.SendErrorMessage(ResultErrorCode.NoResultset, "No resultset");
                        } //using
                    }
                    finally
                    {
                        subFingerIndex.UnLock();
                    }
                } //using
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogError(e);
                return ServiceHelper.SendErrorMessage(ResultErrorCode.Exception, e.Message);
            }
            finally
            {
                GC.Collect();
            }
        }

        public Message FingerprintRecognizeFast(Stream stream)
        {
            string errorMessage;
            if (!UserAccountManager.HasAccess(UserAccountManager.CurrentAccessGroup, out errorMessage, true))
            {
                return ServiceHelper.SendErrorMessage(ResultErrorCode.AuthorizationError, errorMessage);
            }
            UserAccountManager.GlobalNumberOfRequestsInc();

            XElement xResult = null;
            try
            {
                QueryString qs = new QueryString(stream);
                string fingerprint = qs["Fingerprint"];
                string reliabilty = qs["Reliabilities"];

                using (FingerprintSignature fsQuery = new FingerprintSignature("UNKNOWN", 0, System.Convert.FromBase64String(fingerprint), 0, true))
                {
                    fsQuery.Reliabilities = System.Convert.FromBase64String(reliabilty);

                    LuceneIndex subFingerIndex = LuceneIndexes.IndexSubFingerprint;
                    try
                    {
                        subFingerIndex.Lock();
                        using (SubFingerprintQuery query = new SubFingerprintQuery(subFingerIndex.Index))
                        {
                            Resultset answer = query.MatchAudioFingerprintFast(fsQuery, 2300);

                            if (answer != null)
                            {
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.OK);

                                xResult.Add(new XElement("RecognizeResult",
                                    new XAttribute("RecognizeCode", 0),
                                    "OK"));

                                XElement xTimeStatistics = new XElement("TimeStatistics");
                                xTimeStatistics.Add(new XElement("TotalQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.QueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("SubFingerQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerQueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("FingerLoadTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerLoadTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("MatchTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.MatchTime.TotalMilliseconds));
                                xResult.Add(xTimeStatistics);

                                XElement xFingerTracks = new XElement("FingerTracks",
                                    new XAttribute("Count", 0));
                                xResult.Add(xFingerTracks);
                                if (answer.ResultEntries.Count > 0)
                                {
                                    // Gets a NumberFormatInfo associated with the en-US culture.
                                    System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
                                    nfi.CurrencySymbol = "€";
                                    nfi.CurrencyDecimalDigits = 2;
                                    nfi.CurrencyDecimalSeparator = ".";
                                    nfi.NumberGroupSeparator = "";
                                    nfi.NumberDecimalSeparator = ".";

                                    Dictionary<string, ResultEntry> dict = new Dictionary<string, ResultEntry>();
                                    int count = 0;
                                    foreach (ResultEntry item in answer.ResultEntries)
                                    {
                                        XElement xFingerTrack = new XElement("FingerTrack");
                                        xFingerTracks.Add(xFingerTrack);

                                        xFingerTrack.Add(new XAttribute("BER", item.Similarity.ToString()));
                                        xFingerTrack.Add(new XElement("DetectPosition",
                                            new XAttribute("Unit", "ms"),
                                            new XAttribute("InSec", string.Format(nfi, "{0:#0.000}", (item.Time.TotalMilliseconds / 1000))),
                                            (int)item.Time.TotalMilliseconds));

                                        XElement xSearchStrategy = new XElement("SearchStrategy");
                                        xFingerTrack.Add(xSearchStrategy);
                                        xSearchStrategy.Add(new XElement("IndexNumberInMatchList", item.IndexNumberInMatchList));
                                        xSearchStrategy.Add(new XElement("SubFingerCountHitInFingerprint", item.SubFingerCountHitInFingerprint));
                                        xSearchStrategy.Add(new XElement("SearchName", item.SearchStrategy.ToString()));
                                        xSearchStrategy.Add(new XElement("SearchIteration", item.SearchIteration));

                                        xFingerTrack.Add(new XElement("Reference", item.Reference.ToString()));
                                        xFingerTrack.Add(new XElement("FingerTrackID", item.FingerTrackID.ToString()));
                                        count++;
                                    } //foreach
                                    xFingerTracks.Attribute("Count").Value = count.ToString();
                                }

                            }
                            else
                            {
                                // Query geeft null waarde terug. Betekent dat er iets goed fout is gegaan                    
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.FAILED);
                            }

                            if (xResult != null)
                            {
                                return Message.CreateMessage(MessageVersion.None, "", new XElementBodyWriter(xResult));
                            }

                            // Als we hier komen dan hebben we geen resultaat
                            return ServiceHelper.SendErrorMessage(ResultErrorCode.NoResultset, "No resultset");
                        } //using
                    }
                    finally
                    {
                        subFingerIndex.UnLock();
                    }
                } //using
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogError(e);
                return ServiceHelper.SendErrorMessage(ResultErrorCode.Exception, e.Message);
            }
            finally
            {
                GC.Collect();
            }
        }

        public Message FingerprintRecognizeSlow(Stream stream)
        {
            string errorMessage;
            if (!UserAccountManager.HasAccess(UserAccountManager.CurrentAccessGroup, out errorMessage, true))
            {
                return ServiceHelper.SendErrorMessage(ResultErrorCode.AuthorizationError, errorMessage);
            }
            UserAccountManager.GlobalNumberOfRequestsInc();

            StringBuilder sb = new StringBuilder();
            XElement xResult = null;
            try
            {
                QueryString qs = new QueryString(stream);
                string fingerprint = qs["Fingerprint"];
                string reliabilty = qs["Reliabilities"];

                sb.Append("<form action=\"http://localhost:8080/fingerprint/Recognize/Slow\" method=\"post\">\r\n");
                sb.Append("<input type=\"hidden\" name=\"Fingerprint\" value=\"" + fingerprint + "\">\r\n");
                sb.Append("<input type=\"hidden\" name=\"Reliabilities\" value=\"" + reliabilty + "\">\r\n");
                sb.Append("<input type=\"submit\" name=\"DetectSlow\" value=\"DetectSlow\">\r\n");
                sb.Append("</form>\r\n");


                using (FingerprintSignature fsQuery = new FingerprintSignature("UNKNOWN", 0, System.Convert.FromBase64String(fingerprint), 0, true))
                {
                    fsQuery.Reliabilities = System.Convert.FromBase64String(reliabilty);

                    LuceneIndex subFingerIndex = LuceneIndexes.IndexSubFingerprint;
                    try
                    {
                        subFingerIndex.Lock();
                        using (SubFingerprintQuery query = new SubFingerprintQuery(subFingerIndex.Index))
                        {
                            Resultset answer = query.MatchAudioFingerprintSlow(fsQuery, 2300);

                            if (answer != null)
                            {
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.OK);

                                xResult.Add(new XElement("RecognizeResult",
                                    new XAttribute("RecognizeCode", 0),
                                    "OK"));

                                XElement xTimeStatistics = new XElement("TimeStatistics");
                                xTimeStatistics.Add(new XElement("TotalQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.QueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("SubFingerQueryTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerQueryTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("FingerLoadTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.FingerLoadTime.TotalMilliseconds));
                                xTimeStatistics.Add(new XElement("MatchTime",
                                    new XAttribute("Unit", "ms"),
                                    (int)answer.MatchTime.TotalMilliseconds));
                                xResult.Add(xTimeStatistics);

                                XElement xFingerTracks = new XElement("FingerTracks",
                                    new XAttribute("Count", 0));
                                xResult.Add(xFingerTracks);
                                if (answer.ResultEntries.Count > 0)
                                {
                                    // Gets a NumberFormatInfo associated with the en-US culture.
                                    System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
                                    nfi.CurrencySymbol = "€";
                                    nfi.CurrencyDecimalDigits = 2;
                                    nfi.CurrencyDecimalSeparator = ".";
                                    nfi.NumberGroupSeparator = "";
                                    nfi.NumberDecimalSeparator = ".";

                                    Dictionary<string, ResultEntry> dict = new Dictionary<string, ResultEntry>();
                                    int count = 0;
                                    foreach (ResultEntry item in answer.ResultEntries)
                                    {
                                        XElement xFingerTrack = new XElement("FingerTrack");
                                        xFingerTracks.Add(xFingerTrack);

                                        xFingerTrack.Add(new XAttribute("BER", item.Similarity.ToString()));
                                        xFingerTrack.Add(new XElement("DetectPosition",
                                            new XAttribute("Unit", "ms"),
                                            new XAttribute("InSec", string.Format(nfi, "{0:#0.000}", (item.Time.TotalMilliseconds / 1000))),
                                            (int)item.Time.TotalMilliseconds));

                                        XElement xSearchStrategy = new XElement("SearchStrategy");
                                        xFingerTrack.Add(xSearchStrategy);
                                        xSearchStrategy.Add(new XElement("IndexNumberInMatchList", item.IndexNumberInMatchList));
                                        xSearchStrategy.Add(new XElement("SubFingerCountHitInFingerprint", item.SubFingerCountHitInFingerprint));
                                        xSearchStrategy.Add(new XElement("SearchName", item.SearchStrategy.ToString()));
                                        xSearchStrategy.Add(new XElement("SearchIteration", item.SearchIteration));

                                        xFingerTrack.Add(new XElement("Reference", item.Reference.ToString()));
                                        xFingerTrack.Add(new XElement("FingerTrackID", item.FingerTrackID.ToString()));
                                        count++;
                                    } //foreach
                                    xFingerTracks.Attribute("Count").Value = count.ToString();
                                }

                            }
                            else
                            {
                                // Query geeft null waarde terug. Betekent dat er iets goed fout is gegaan                    
                                xResult = ServiceHelper.CreateRoot(ResultErrorCode.FAILED);
                            }

                            if (xResult != null)
                            {
                                return Message.CreateMessage(MessageVersion.None, "", new XElementBodyWriter(xResult));
                            }

                            // Als we hier komen dan hebben we geen resultaat
                            return ServiceHelper.SendErrorMessage(ResultErrorCode.NoResultset, "No resultset");
                        } //using
                    }
                    finally
                    {
                        subFingerIndex.UnLock();
                    }

                } //using
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogError(e);

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(string.Format("AudioDump{0:yyyy-MM-dd-HH-mm-ss-fff}.htm", DateTime.Now), true))
                {
                    file.WriteLine(sb.ToString());
                }

                return ServiceHelper.SendErrorMessage(ResultErrorCode.Exception, e.Message);
            }
            finally
            {
                GC.Collect();
            }
        }

        #endregion


        #region Private functions

        #endregion
    }

    /// <summary>
    /// Deze implementatie is er alleen voor haproxy om te checken of de webservice nog online is
    /// Het moet in een aparte class omdat we geen "authentication" kunne/mogen gebruiken
    /// bij haproxy
    /// 
    /// De standard url is hier altijd "http://localhost:8080/engine/health/"
    /// </summary>
    public class ServiceHealthCheck : IServiceContractHealthCheck
    {
        private static DateTime dtLastCheckHealth = DateTime.MinValue;
        private static object lockObject = new object();

        public Stream CheckHealth()
        {
            MemoryStream ms = new MemoryStream();
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";

            bool returnError = false;
            try
            {
                if (!Webservice.ForceServiceOffline)
                {
                    // Check if MySQL is alive
                    byte[] data = Encoding.ASCII.GetBytes("200 OK");
                    ms.Write(data, 0, data.Length);
                    WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
                }
            }
            catch
            {
                returnError = true;
            }

            // Moeten we een error teruggeven
            if (Webservice.ForceServiceOffline || returnError)
            {
                ms = new MemoryStream();
                byte[] data = Encoding.ASCII.GetBytes("500 Internal Server Error");
                ms.Write(data, 0, data.Length);
                WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            }

            ms.Position = 0;

            lock (lockObject)
            {
                dtLastCheckHealth = DateTime.Now;
            } //lock
            return ms;
        }

        public Stream CheckHealthHEAD()
        {
            return CheckHealth();
        }

        public Stream CheckHealthGET()
        {
            return CheckHealth();
        }

        /// <summary>
        /// Return last time when the service health check was called (probally by haproxy)
        /// </summary>
        public static DateTime LastCheckHealth
        {
            get
            {
                lock (lockObject)
                {
                    // maak kopie van datetime en geef die terug
                    return new DateTime(dtLastCheckHealth.Ticks);
                } //lock
            }
        }

    }
}