using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AudioFingerprint.Audio;
using RestSharp;

namespace AudioFingerprint.WebService
{
    public class WSRecognize : IDisposable
    {
        private static object staticLockVAR = new object();
        private static DateTime lastDTCall = DateTime.MinValue;
        private static int ignoreSSLCertifivcateCount = 0;

        private string userAgent = "CDRWebserviceClient 1.00";
        private int timeoutInMS = 20000; // wacht 20 seconden voordat we "failen"

        // Some default. See Fingerprint.ini for real values used
        private string wsProtocol = "http";
        private string wsDNSFinger = "127.0.0.1";
        private string wsPort = "8080";
        private string wsUser = "RadioChannel";
        private string wsPassword = "123456";

        private string wsAPICDRNL_Protocol = "https";
        private string wsAPICDRNL_DNS = "api.cdr.nl";
        private string wsAPICDRNL_Port = "";
        private string wsAPICDRNL_User = ""; 
        private string wsAPICDRNL_Password = "";


        private SynchronizationContext synchronizationContext = null;


        public WSRecognize()
        {
            this.synchronizationContext = SynchronizationContext.Current;

            int newValue = Interlocked.Increment(ref ignoreSSLCertifivcateCount);
            // eerste keer?
            if (newValue == 1)
            {
                ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
            }
            ServicePointManager.DefaultConnectionLimit = 4; // default is 4
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;


            string IniPath = CDR.DB_Helper.FingerprintIniFile;
            if (System.IO.File.Exists(IniPath))
            {
                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                wsProtocol = ini.IniReadValue("WSRecognize", "wsProtocol", wsProtocol);
                wsDNSFinger = ini.IniReadValue("WSRecognize", "wsDNSFinger", wsDNSFinger);
                wsPort = ini.IniReadValue("WSRecognize", "wsPort", wsPort);
                wsUser = ini.IniReadValue("WSRecognize", "wsUser", wsUser);
                wsPassword = ini.IniReadValue("WSRecognize", "wsPassword", wsPassword);

                wsAPICDRNL_Protocol = ini.IniReadValue("Muziekweb", "wsAPICDRNL_Protocol", wsAPICDRNL_Protocol);
                wsAPICDRNL_DNS = ini.IniReadValue("Muziekweb", "wsAPICDRNL_DNS", wsAPICDRNL_DNS);
                wsAPICDRNL_Port = ini.IniReadValue("Muziekweb", "wsAPICDRNL_Port", wsAPICDRNL_Port);
                wsAPICDRNL_User = ini.IniReadValue("Muziekweb", "wsAPICDRNL_User", wsAPICDRNL_User);
                wsAPICDRNL_Password = ini.IniReadValue("Muziekweb", "wsAPICDRNL_Password", wsAPICDRNL_Password);
            }
        }

        #region IDispose implementation

        // Track whether Dispose has been called.
        private bool disposed = false;

        /// <summary>
        /// Implement IDisposable.
        /// Do not make this method virtual.
        /// A derived class should not be able to override this method.
        /// </summary>
        void IDisposable.Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios.
        /// If disposing equals true, the method has been called directly
        /// or indirectly by a user's code. Managed and unmanaged resources
        /// can be disposed.
        /// If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference 
        /// other objects. Only unmanaged resources can be disposed.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                try
                {
                    int newValue = Interlocked.Decrement(ref ignoreSSLCertifivcateCount);
                    // eerste keer?
                    if (newValue == 0)
                    {
                        ServicePointManager.ServerCertificateValidationCallback -= ValidateServerCertificate;
                    }
                }
                catch
                {
                }
            }

            disposed = true;
        }

        /// <summary>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </summary>
        ~WSRecognize()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// The following method is invoked by the RemoteCertificateValidationDelegate. 
        /// needed to ignore invalid certificates (as is with the DEBUG build)
        /// </summary>
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private RestClient CreateFingerprintClient
        {
            get
            {
                string address = wsDNSFinger;
                IPAddress[] addresslist = Dns.GetHostAddresses(address);
                if (addresslist.Length > 0)
                {
                    address = addresslist[0].ToString();
                }

                RestClient client = new RestClient(string.Format("{0}://{1}:{2}", wsProtocol, address, wsPort));
                client.UserAgent = userAgent;
                client.Timeout = timeoutInMS;
                client.Authenticator = new HttpBasicAuthenticator(wsUser, wsPassword);

                lock (staticLockVAR)
                {
                    if ((DateTime.Now - lastDTCall).TotalSeconds > 100)
                    {
                        // Kill (ALL) connections
                        try
                        {
                            ServicePoint srvrPoint = ServicePointManager.FindServicePoint(new Uri(client.BaseUrl));
                            System.Reflection.MethodInfo ReleaseConns = srvrPoint.GetType().GetMethod
                                ("ReleaseAllConnectionGroups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            ReleaseConns.Invoke(srvrPoint, null);
                        }
                        catch
                        {
                        }
                    }

                    lastDTCall = DateTime.Now;
                }

                return client;
            }
        }

        /// <summary>
        /// Connect to the api.cdr.nl webservice to retrieve 
        /// </summary>
        private RestClient CreateWSAPICDRNLClient
        {
            get
            {
                string address = wsAPICDRNL_DNS;
                IPAddress[] addresslist = Dns.GetHostAddresses(address);
                if (addresslist.Length > 0)
                {
                    address = addresslist[0].ToString();
                }

                RestClient client;
                if (wsAPICDRNL_Port.Length > 0)
                {
                    client = new RestClient(string.Format("{0}://{1}:{2}", wsAPICDRNL_Protocol, address, wsAPICDRNL_Port));
                }
                else
                {
                    client = new RestClient(string.Format("{0}://{1}", wsAPICDRNL_Protocol, address));
                }
                client.UserAgent = userAgent;
                client.Timeout = timeoutInMS;
                string username = wsAPICDRNL_User;
                string password = wsAPICDRNL_Password;
                if (string.IsNullOrEmpty(username))
                {
                    username = "-";
                }
                if (string.IsNullOrEmpty(password))
                {
                    password = "-";
                }
                client.Authenticator = new HttpBasicAuthenticator(username, password);

                lock (staticLockVAR)
                {
                    if ((DateTime.Now - lastDTCall).TotalSeconds > 100)
                    {
                        // Kill (ALL) connections
                        try
                        {
                            ServicePoint srvrPoint = ServicePointManager.FindServicePoint(new Uri(client.BaseUrl));
                            System.Reflection.MethodInfo ReleaseConns = srvrPoint.GetType().GetMethod
                                ("ReleaseAllConnectionGroups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            ReleaseConns.Invoke(srvrPoint, null);
                        }
                        catch
                        {
                        }
                    }

                    lastDTCall = DateTime.Now;
                }

                return client;
            }
        }

        public CancellationTokenSource DetectAudioFragment(FingerprintSignature fingerprint, object userState = null, REST_ResultFingerDetect callback = null)
        {
            if (fingerprint == null)
            {
                return null;
            }

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

            Task task = Task.Factory.StartNew(() =>
            {
                // Were we already canceled?
                cancelTokenSource.Token.ThrowIfCancellationRequested();

                DateTime startTime = DateTime.Now;
                bool succes = true;
                RecognizeCode RecognizeCode = RecognizeCode.OK;
                string RecognizeResult = "";
                try
                {
                    RestClient client = CreateFingerprintClient;

                    // Build search request
                    RestRequest request = new RestRequest("fingerprint/Recognize", Method.POST);
                    request.AddHeader("Accept-Encoding", "gzip,deflate");
                    StringBuilder sb = new StringBuilder(200 * 1024);
                    sb.Append("Fingerprint=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.SignatureBase64));
                    sb.Append("&");
                    sb.Append("Reliabilities=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.ReliabilitiesBase64));
                    request.AddParameter("application/x-www-form-urlencoded", sb.ToString(), ParameterType.RequestBody);

                    // Run and wait for result
                    IRestResponse response = client.Execute(request);
                    cancelTokenSource.Token.ThrowIfCancellationRequested();
                    if (response.ResponseStatus != ResponseStatus.Completed)
                    {
                        // error!
                        succes = false;

                        RecognizeCode = RecognizeCode.EXCEPTION; // exception
                        RecognizeResult = response.ErrorMessage;
                        if (RecognizeResult.ToLower().Contains("timed out"))
                        {
                            RecognizeCode = RecognizeCode.TIMEOUT;
                        }
                        else if (RecognizeResult.ToLower().Contains("unable to connect"))
                        {
                            RecognizeCode = RecognizeCode.SERVERNOTFOUND;
                        }
                    }

                    // decode xml on the fly (must be done because xml can get new tags and attributes without notice
                    XmlDocument xmlDoc = null;
                    XmlElement xResult = null;
                    if (succes)
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(response.Content);

                        xResult = xmlDoc.GetElementsByTagName("Result")[0] as XmlElement;
                        if (xResult == null || xResult.Attributes["ErrorCode"].Value != "0")
                        {
                            // error
                            succes = false;
                        }
                    }

                    if (succes)
                    {
                        ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                        resultRecognitions.RecognizeCode = 0;
                        resultRecognitions.RecognizeResult = "OK";
                        succes = resultRecognitions.ParseFingerprintRecognition(xResult);

                        // do something with the result
                        cancelTokenSource.Token.ThrowIfCancellationRequested();
                        if (callback != null)
                        {
                            DoCallback(callback, this, true, resultRecognitions, userState);
                        }
                        // we're ready
                        return;
                    }
                }
                catch (Exception e)
                {
                    RecognizeCode = RecognizeCode.EXCEPTION; // exception
                    RecognizeResult = e.ToString();
                }

                // return error result when we get here
                if (callback != null)
                {
                    ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                    resultRecognitions.RecognizeCode = RecognizeCode;
                    resultRecognitions.RecognizeResult = RecognizeResult;
                    resultRecognitions.TimeStatistics.TotalQueryTime = DateTime.Now - startTime;

                    DoCallback(callback, this, false, resultRecognitions, userState);

                }
            }, cancelTokenSource.Token);

            return cancelTokenSource;
        }

        public CancellationTokenSource DetectAudioFragmentFast(FingerprintSignature fingerprint, object userState = null, REST_ResultFingerDetect callback = null)
        {
            if (fingerprint == null)
            {
                return null;
            }

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

            Task task = Task.Factory.StartNew(() =>
            {
                // Were we already canceled?
                cancelTokenSource.Token.ThrowIfCancellationRequested();

                DateTime startTime = DateTime.Now;
                bool succes = true;
                RecognizeCode RecognizeCode = RecognizeCode.OK;
                string RecognizeResult = "";
                try
                {
                    RestClient client = CreateFingerprintClient;

                    // Build search request
                    RestRequest request = new RestRequest("fingerprint/Recognize/Fast", Method.POST);
                    request.AddHeader("Accept-Encoding", "gzip,deflate");
                    StringBuilder sb = new StringBuilder(200 * 1024);
                    sb.Append("Fingerprint=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.SignatureBase64));
                    sb.Append("&");
                    sb.Append("Reliabilities=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.ReliabilitiesBase64));
                    request.AddParameter("application/x-www-form-urlencoded", sb.ToString(), ParameterType.RequestBody);

                    // Run and wait for result
                    IRestResponse response = client.Execute(request);
                    cancelTokenSource.Token.ThrowIfCancellationRequested();
                    if (response.ResponseStatus != ResponseStatus.Completed)
                    {
                        // error!
                        succes = false;

                        RecognizeCode = RecognizeCode.EXCEPTION; // exception
                        RecognizeResult = response.ErrorMessage;
                        if (RecognizeResult.ToLower().Contains("timed out"))
                        {
                            RecognizeCode = RecognizeCode.TIMEOUT;
                        }
                        else if (RecognizeResult.ToLower().Contains("unable to connect"))
                        {
                            RecognizeCode = RecognizeCode.SERVERNOTFOUND;
                        }
                    }

                    // decode xml on the fly (must be done because xml can get new tags and attributes without notice
                    XmlDocument xmlDoc = null;
                    XmlElement xResult = null;
                    if (succes)
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(response.Content);

                        xResult = xmlDoc.GetElementsByTagName("Result")[0] as XmlElement;
                        if (xResult == null || xResult.Attributes["ErrorCode"].Value != "0")
                        {
                            // error
                            succes = false;
                        }
                    }

                    if (succes)
                    {
                        ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                        resultRecognitions.RecognizeCode = 0;
                        resultRecognitions.RecognizeResult = "OK";
                        succes = resultRecognitions.ParseFingerprintRecognition(xResult);

                        // do something with the result
                        cancelTokenSource.Token.ThrowIfCancellationRequested();
                        if (callback != null)
                        {
                            DoCallback(callback, this, true, resultRecognitions, userState);
                        }
                        // we're ready
                        return;
                    }
                }
                catch (Exception e)
                {
                    RecognizeCode = RecognizeCode.EXCEPTION; // exception
                    RecognizeResult = e.ToString();
                }

                // return error result when we get here
                if (callback != null)
                {
                    ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                    resultRecognitions.RecognizeCode = RecognizeCode;
                    resultRecognitions.RecognizeResult = RecognizeResult;
                    resultRecognitions.TimeStatistics.TotalQueryTime = DateTime.Now - startTime;

                    DoCallback(callback, this, false, resultRecognitions, userState);
                }
            }, cancelTokenSource.Token);

            return cancelTokenSource;
        }

        public CancellationTokenSource DetectAudioFragmentSlow(FingerprintSignature fingerprint, object userState = null, REST_ResultFingerDetect callback = null)
        {
            if (fingerprint == null)
            {
                return null;
            }

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

            Task task = Task.Factory.StartNew(() =>
            {
                // Were we already canceled?
                cancelTokenSource.Token.ThrowIfCancellationRequested();

                DateTime startTime = DateTime.Now;
                bool succes = true;
                RecognizeCode RecognizeCode = RecognizeCode.OK;
                string RecognizeResult = "";
                try
                {
                    RestClient client = CreateFingerprintClient;

                    // Build search request
                    RestRequest request = new RestRequest("fingerprint/Recognize/Slow", Method.POST);
                    request.AddHeader("Accept-Encoding", "gzip,deflate");
                    StringBuilder sb = new StringBuilder(200 * 1024);
                    sb.Append("Fingerprint=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.SignatureBase64));
                    sb.Append("&");
                    sb.Append("Reliabilities=");
                    sb.Append(RestSharp.Contrib.HttpUtility.UrlEncode(fingerprint.ReliabilitiesBase64));
                    request.AddParameter("application/x-www-form-urlencoded", sb.ToString(), ParameterType.RequestBody);

                    // Run and wait for result
                    IRestResponse response = client.Execute(request);
                    cancelTokenSource.Token.ThrowIfCancellationRequested();
                    if (response.ResponseStatus != ResponseStatus.Completed)
                    {
                        // error!
                        succes = false;

                        RecognizeCode = RecognizeCode.EXCEPTION; // exception
                        RecognizeResult = response.ErrorMessage;
                        if (RecognizeResult.ToLower().Contains("timed out"))
                        {
                            RecognizeCode = RecognizeCode.TIMEOUT;
                        }
                        else if (RecognizeResult.ToLower().Contains("unable to connect"))
                        {
                            RecognizeCode = RecognizeCode.SERVERNOTFOUND;
                        }
                    }

                    // decode xml on the fly (must be done because xml can get new tags and attributes without notice
                    XmlDocument xmlDoc = null;
                    XmlElement xResult = null;
                    if (succes)
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(response.Content);

                        xResult = xmlDoc.GetElementsByTagName("Result")[0] as XmlElement;
                        if (xResult == null || xResult.Attributes["ErrorCode"].Value != "0")
                        {
                            // error
                            succes = false;
                        }
                    }

                    if (succes)
                    {
                        ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                        resultRecognitions.RecognizeCode = 0;
                        resultRecognitions.RecognizeResult = "OK";
                        succes = resultRecognitions.ParseFingerprintRecognition(xResult);

                        // do something with the result
                        cancelTokenSource.Token.ThrowIfCancellationRequested();
                        if (callback != null)
                        {
                            DoCallback(callback, this, true, resultRecognitions, userState);
                        }
                        // we're ready
                        return;
                    }
                }
                catch (Exception e)
                {
                    RecognizeCode = RecognizeCode.EXCEPTION; // exception
                    RecognizeResult = e.ToString();
                }

                // return error result when we get here
                if (callback != null)
                {
                    ResultFingerprintRecognition resultRecognitions = new ResultFingerprintRecognition();
                    resultRecognitions.RecognizeCode = RecognizeCode;
                    resultRecognitions.RecognizeResult = RecognizeResult;
                    resultRecognitions.TimeStatistics.TotalQueryTime = DateTime.Now - startTime;

                    DoCallback(callback, this, false, resultRecognitions, userState);
                }
            }, cancelTokenSource.Token);

            return cancelTokenSource;
        }


        public CancellationTokenSource RetrieveMetaDataMuziekweb(string FingerTrackID, object userState = null, REST_ResultMetaDataMuziekweb callback = null)
        {
            if (string.IsNullOrEmpty(FingerTrackID))
            {
                return null;
            }

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

            Task task = Task.Factory.StartNew(() =>
            {
                // Were we already canceled?
                cancelTokenSource.Token.ThrowIfCancellationRequested();

                DateTime startTime = DateTime.Now;
                bool succes = true;
                RecognizeCode RecognizeCode = RecognizeCode.OK;
                string RecognizeResult = "";
                try
                {
                    RestClient client = CreateWSAPICDRNLClient;

                    // Build search request
                    RestRequest request = new RestRequest("v1/Fingerprint/TrackInfo.xml", Method.GET);
                    request.AddHeader("Accept-Encoding", "gzip,deflate");
                    request.AddParameter("FingerTrackID", FingerTrackID);

                    // Run and wait for result
                    IRestResponse response = client.Execute(request);
                    cancelTokenSource.Token.ThrowIfCancellationRequested();
                    if (response.ResponseStatus != ResponseStatus.Completed)
                    {
                        // error!
                        succes = false;
                    }

                    // decode xml on the fly (must be done because xml can get new tags and attributes without notice
                    XmlDocument xmlDoc = null;
                    XmlElement xResult = null;
                    if (succes)
                    {
                        xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(response.Content);

                        xResult = xmlDoc.GetElementsByTagName("Result")[0] as XmlElement;
                        if (xResult == null || xResult.Attributes["ErrorCode"].Value != "0")
                        {
                            // error
                            succes = false;
                        }
                    }

                    if (succes)
                    {
                        ResultSongs resultSongs = new ResultSongs();
                        succes = resultSongs.ParseSongs(xResult);

                        // do something with the result
                        cancelTokenSource.Token.ThrowIfCancellationRequested();
                        if (callback != null)
                        {
                            DoCallback(callback, this, true, resultSongs, userState);
                        }
                        // we're ready
                        return;
                    }
                }
                catch (Exception e)
                {
                    RecognizeCode = RecognizeCode.EXCEPTION; // exception
                    RecognizeResult = e.ToString();
                }

                // return error result when we get here
                if (callback != null)
                {
                    ResultSongs resultSongs = new ResultSongs();
                    DoCallback(callback, this, false, resultSongs, userState);
                }
            }, cancelTokenSource.Token);

            return cancelTokenSource;
        }

        /// <summary>
        /// Helper function to pick the first ipv4 number
        /// </summary>
        private string GetIPNumber
        {
            get
            {
                string ipStr = "127.0.0.1";
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                    {
                        foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                ipStr = ip.Address.ToString();
                                break;
                            }
                        } //foreach
                    }
                }
                return ipStr;
            }
        }



        #region SynchronizationContext Helper

        /// <summary>
        /// If possible will call the user callback function on the 
        /// thread set by SynchronizationContextMethod. This
        /// will most of the time be the UI thread.
        /// </summary>
        private void DoCallback(params object[] objects)
        {
            SynchronizationContext sc;
            lock (staticLockVAR)
            {
                sc = synchronizationContext;
            } //lock
            if (sc != null)
            {
                sc.Post(HandleCallUserCode, objects);
            }
            else
            {
                HandleCallUserCode(objects);
            }
        }


        private void HandleCallUserCode(object state)
        {
            object[] objects = (object[])state;

            if (objects[0] is REST_ResultFingerDetect)
            {
                // REST_ResultFingerDetect(object sender, bool success, object userState = null);
                (objects[0] as REST_ResultFingerDetect)((object)objects[1], (bool)objects[2], (ResultFingerprintRecognition)objects[3], (object)objects[4]);
            }
            else if (objects[0] is REST_ResultMetaDataMuziekweb)
            {
                // REST_ResultMetaDataMuziekweb(object sender, bool success, object userState = null);
                (objects[0] as REST_ResultMetaDataMuziekweb)((object)objects[1], (bool)objects[2], (ResultSongs)objects[3], (object)objects[4]);
            }
        }

        #endregion

    }


    public delegate void REST_ResultFingerDetect(object sender, bool success, ResultFingerprintRecognition resultRecognitions, object userState = null);

    public delegate void REST_ResultMetaDataMuziekweb(object sender, bool success, ResultSongs resultSongs, object userState = null);
}
