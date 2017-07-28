using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using CDR;
using CDR.Ini;
using CDR.Logging;

namespace RadioChannelWebService
{
    public class CustomWebAuthenticationValidator : UserNamePasswordValidator
    {
        // Omdat het blijkbaar niet mogelijk is om custom data terug te sturen naar de client als authenticatie niet werkt
        // (kan er niks overvinden en dat wat ik vind zegt dat het niet kan)
        // Zie bijvoorbeeld http://social.msdn.microsoft.com/Forums/en/wcf/thread/dcaeaeb4-b432-4c65-a6d1-5f721be6d672 (vooral einde)
        // 
        // Oplossing die wordt genoemd is alles accepteren in Validate en
        // vervolgens in je functie het afhandelen. Niet erg mooi maar er zit blijkbaar niks
        // anders op.
        //
        // Dus elke "extern" service call moet eerst gecontroleerd worden op authorizatie voordat ie verder kan.
        //

        // voorbeeld: http://www.leastprivilege.com/FinallyUsernamesOverTransportAuthenticationInWCF.aspx
        // in combinatie met http://bytes.com/topic/net/answers/585032-wcf-username-authentication
        public override void Validate(string userName, string password)
        {
            // Dummy functie, nodig om Basic authentication te kunnen gebruiken
            // Helaas heb je hier geen context zodat je de gebruikers geen zinninge
            // foutmelding kunt geven.
            // We accepteren hier dus alles maar handelen het verder bij de geimplementeerde
            // functie zelf af. We lezen dan de header uit om username en password 
            // te kunnen authenticeren. Bij fout sturen we dat een xml antwoord met daarin
            // reden dat niet geauthenticeerd kon worden.
            
            //Console.WriteLine(string.Format("Username: {0} Password: {1}", userName, password));
        }


        // Zelf specifieke error code sturen zie http://blogs.msdn.com/drnick/archive/2010/02/02/fix-to-allow-customizing-the-status-code-when-validation-fails.aspx
        //   Exception e = new Exception();
        //   e.Data["HttpStatusCode"] = System.Net.HttpStatusCode.Unauthorized;
        //   throw e;
        //
        // Anders de default actie doen:
        //   throw new SecurityTokenException("Invalid credentials.");
    }


    public static class UserAccountManager
    {
        private static object lockObject = new object();
        private static Dictionary<string, UserAccount> userDatabase = new Dictionary<string, UserAccount>();
        private static Int64 globalNumberOfRequests = 0;

        private const int sizeHitsPerSecondLog = 1 * 60; // voor elke seconden 1 entry (kan niet zomaar veranderen, zie logica in WindowsService voor grafiek!)
        private static HitsPerSecondLog hitsPerSecond;
        private static HitsPerSecondLog? calcHitsPerSecond = null; // for thread to calculate new values for avg, min and max
        private static double avgHitsPerSecond = -1;
        private static double minHitsPerSecond = -1;
        private static double maxHitsPerSecond = -1;
        private static DateTime currentDTMinMaxAvgHits = DateTime.MinValue;
        private static MinMaxAvgHit[] currentMinMaxAvgHits = null;
        private static MinMaxAvgHit[] previousMinMaxAvgHits = null;

        public static string UserDatabaseFilename
        {
            get
            {
                return Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".UserDB.ini");
            }
        }

        /// <summary>
        /// Static constructor. Will be called automatically before first use of this class
        /// </summary>
        static UserAccountManager()
        {
            hitsPerSecond = new HitsPerSecondLog();
            hitsPerSecond.HitPerSecondArray = null;
            hitsPerSecond.StartInTotalSeconds = Convert.ToInt64((double)DateTime.MinValue.Ticks / TimeSpan.TicksPerSecond);

            // Nu even snel wat accounts toevoegen
            ReadUserDatabase();
        }

        public static void ReadUserDatabase()
        {
            lock (lockObject)
            {
                // Reset alle user account tags zodat we straks  kunnen herkenen welke account "verwijderd" moeten worden
                foreach (UserAccount ua in userDatabase.Values)
                {
                    ua.Tag = 0;
                } //foreach


                // First get the name of the inifile
                string iniFilename = UserDatabaseFilename;

                // First read the ini-file settings
                if (File.Exists(iniFilename))
                {
                    IniFile ini = new IniFile(iniFilename);

                    int count = 0;
                    while (true)
                    {
                        count++;
                        string section = String.Format("User{0}", count);
                        bool addToDB = false;
                        string username = ini.IniReadValue(section, "Username", "");
                        if (username.Length <= 0)
                        {
                            // we zijn klaar!
                            break;
                        }


                        // Als account bestaat dan gegevens wijzigen anders user toevoegen
                        UserAccount ua = null;
                        if (!userDatabase.TryGetValue(username, out ua))
                        {
                            addToDB = true;
                            ua = new UserAccount();
                            ua.Username = username;
                        }
                        ua.Tag = 1; // deze account kan blijven

                        ua.Password = ini.IniReadValue(section, "Password", "_________" + count.ToString());
                        try
                        {
                            ua.NumberOfDifferentIPsPerHour = Convert.ToInt32(ini.IniReadValue(section, "NumberOfDifferentIPsPerHour", "0"));
                        }
                        catch { }
                        try
                        {
                            ua.NumberOfRequestsPerHour = Convert.ToInt32(ini.IniReadValue(section, "NumberOfRequestsPerHour", "0"));
                        }
                        catch { }


                        string groupList = ini.IniReadValue(section, "AccessGroup", "");
                        foreach (string group in groupList.Split(','))
                        {
                            // Skip over eventuele everbody groep (die is altijd al aanwezig)
                            if (group.ToUpper().Trim() != AccessGroupList.Everybody.ToString().ToUpper())
                            {
                                foreach (string s in Enum.GetNames(typeof(AccessGroupList)))
                                {
                                    if (group.ToUpper().Trim() == s.ToUpper())
                                    {
                                        ua.AccessGroupList.Add((AccessGroupList)Enum.Parse(typeof(AccessGroupList), s));
                                    }
                                } //foreach
                            }
                        } //foreach


                        // Voeg toe aan de database
                        
                        if (addToDB)
                        {
                            userDatabase.Add(ua.Username, ua);
                        }
                    } //while


                    // Nu useraccounts verwijderen die niet meer in ini file voorkomen. Tegelijk Tag resetten
                    var deleteUA = from entry in userDatabase
                                   where entry.Value.Tag == 0
                                   select entry.Key;
                    // verwijder de verwijderde useraccounts
                    foreach (string key in deleteUA)
                    {
                        userDatabase.Remove(key);
                    } //foreach


                    // Reset alle tags
                    foreach (UserAccount ua in userDatabase.Values)
                    {
                        ua.Tag = 0;
                    } //foreach
                }
                else
                {
                    userDatabase.Clear();

                    UserAccount ua;

                    ua = new UserAccount();
                    ua.Username = "RadioChannel";
                    ua.Password = "123456";
                    ua.NumberOfDifferentIPsPerHour = -1; // unlimited
                    ua.NumberOfRequestsPerHour = -1; //unlimited
                    ua.AccessGroupList.Add(AccessGroupList.Administrator);
                    userDatabase.Add(ua.Username, ua);

                    // Write it to disk
                    WriteUserDatabase();
                }
            }
        }

        private static bool writeUserDB = false;
        private static void WriteUserDatabase()
        {
            lock (lockObject)
            {
                writeUserDB = true;
                try
                {
                    // First get the name of the inifile
                    string iniFilename = UserDatabaseFilename;

                    // First read the ini-file settings
                    if (File.Exists(iniFilename))
                    {
                        File.Delete(iniFilename);
                    }

                    // Add first some informative info to the ini file
                    using (TextWriter tw = new StreamWriter(iniFilename))
                    {
                        tw.WriteLine("# Make sure when adding a new user that the section names are consecutive");
                        tw.WriteLine("# if they are not user info will not be read.");
                        tw.WriteLine("#");
                        tw.WriteLine("# The user '________' is special because this user is used by the website and a few");
                        tw.WriteLine("# maintance programs like this one it's self and XML2MySQL.");
                        tw.WriteLine("# Don't change this username and password before you make appropiate changes in those programs.");
                        tw.WriteLine("#");
                        tw.WriteLine("# The following entries must be set in a section for a [User#]if they are not user info will not be read.");
                        tw.WriteLine("# Username=username");
                        tw.WriteLine("# Password=password");
                        tw.WriteLine("# NumberOfDifferentIPsPerHour=0");
                        tw.WriteLine("# NumberOfRequestsPerHour=0");
                        tw.WriteLine("# AccessGroup=group list");
                        tw.WriteLine("#");
                        tw.WriteLine("#");
                        tw.WriteLine("# AccessGroup can have none, one or more of the following items separated by a ,");
                        tw.Write("# AccessGroup=");
                        int countGroups = 0;
                        foreach (object v in Enum.GetValues(typeof(AccessGroupList)))
                        {
                            AccessGroupList group = (AccessGroupList)v;
                            if (!(group == AccessGroupList.Everybody || group == AccessGroupList.Nobody))
                            {
                                if (countGroups > 0)
                                {
                                    tw.Write(",");
                                }
                                tw.Write(group.ToString());
                                countGroups++;
                            }
                        } //foreach
                        tw.WriteLine();

                        tw.WriteLine("#");
                        tw.WriteLine();
                    } //using

                    IniFile ini = new IniFile(iniFilename);

                    int count = 0;
                    foreach (UserAccount ua in userDatabase.Values)
                    {
                        count++;
                        string section = String.Format("User{0}", count);

                        ini.IniWriteValue(section, "Username", ua.Username);
                        ini.IniWriteValue(section, "Password", ua.Password);
                        ini.IniWriteValue(section, "NumberOfDifferentIPsPerHour", ua.NumberOfDifferentIPsPerHour.ToString());
                        ini.IniWriteValue(section, "NumberOfRequestsPerHour", ua.NumberOfRequestsPerHour.ToString());

                        string groupList = string.Empty;
                        foreach (AccessGroupList group in ua.AccessGroupList)
                        {
                            // Everbody is present by default
                            if (group != RadioChannelWebService.AccessGroupList.Everybody)
                            {
                                if (groupList.Length > 0)
                                {
                                    groupList += ",";
                                }
                                groupList += group.ToString();
                            }
                        } //foreach

                        ini.IniWriteValue(section, "AccessGroup", groupList);
                    } //foreach
                }
                finally
                {
                    writeUserDB = false;
                }
            } //lock
        }

        public static bool WritingToUserDatabase
        {
            get
            {
                return writeUserDB;
            }
        }

        #region Request counters
        public static Int64 GlobalNumberOfRequests
        {
            get
            {
                lock (lockObject)
                {
                    return globalNumberOfRequests;
                }
            }
            set
            {
                lock (lockObject)
                {
                    globalNumberOfRequests = value;
                }
            }
        }

        /// <summary>
        /// Increase counter and return value. Thread safe
        /// </summary>
        public static Int64 GlobalNumberOfRequestsInc()
        {
            lock (lockObject)
            {
                globalNumberOfRequests++;

                try
                {
                    DateTime dt = DateTime.Now;
                    long totalSeconds = Convert.ToInt64((double)dt.Ticks / TimeSpan.TicksPerSecond);

                    // detecteren van rollover
                    if (hitsPerSecond.HitPerSecondArray == null || (totalSeconds - hitsPerSecond.StartInTotalSeconds) >= hitsPerSecond.HitPerSecondArray.Length)
                    {
                        // Hits per seconden berekenen heeft alleen zin als er metingen zijn
                        if (hitsPerSecond.HitPerSecondArray != null)
                        {
                            calcHitsPerSecond = hitsPerSecond;
                        }

                        hitsPerSecond = new HitsPerSecondLog();
                        hitsPerSecond.HitPerSecondArray = new int[sizeHitsPerSecondLog];
                        hitsPerSecond.StartInTotalSeconds = totalSeconds; // "nieuwe rollover"
                    }

                    int seconds = Convert.ToInt32(totalSeconds - hitsPerSecond.StartInTotalSeconds);
                    hitsPerSecond.HitPerSecondArray[seconds]++;                
                }
                catch (Exception e)
                {
                    CDRLogger.Logger.LogError(e);
                }

                return globalNumberOfRequests;
            }
        }

        public static double AvgHitsPerSecond
        {
            get
            {
                lock (lockObject)
                {
                    return avgHitsPerSecond;
                }
            }            
            set
            {
                lock (lockObject)
                {
                    avgHitsPerSecond = value;
                }
            }
        }

        public static double MinHitsPerSecond
        {
            get
            {
                lock (lockObject)
                {
                    return minHitsPerSecond;
                }
            }
            set
            {
                lock (lockObject)
                {
                    minHitsPerSecond = value;
                }
            }
        }

        public static double MaxHitsPerSecond
        {
            get
            {
                lock (lockObject)
                {
                    return maxHitsPerSecond;
                }
            }
            set
            {
                lock (lockObject)
                {
                    maxHitsPerSecond = value;
                }
            }
        }

        public static void ResetRequestsCounters()
        {
            lock (lockObject)
            {
                globalNumberOfRequests = 0;
                
                avgHitsPerSecond = -1;
                minHitsPerSecond = -1;
                maxHitsPerSecond = -1;
                hitsPerSecond = new HitsPerSecondLog();
                hitsPerSecond.HitPerSecondArray = new int[sizeHitsPerSecondLog];
                hitsPerSecond.StartInTotalSeconds = Convert.ToInt64((double)DateTime.Now.Ticks / TimeSpan.TicksPerSecond);
                currentMinMaxAvgHits = null;
            }
        }

        /// <summary>
        /// Tells if there is a new hit per second available to be used for calculation.
        /// Will also check to make it available it there were no requests in the timeframe
        /// </summary>
        public static bool NewHitsPerSecondLog
        {
            get
            {
                bool result = false;
                long tmpStartInTotalSeconds = 0;
                // niet te lang de data locken
                lock (lockObject)
                {
                    result = calcHitsPerSecond != null && ((HitsPerSecondLog)calcHitsPerSecond).HitPerSecondArray != null;
                    if (!result)
                    {
                        tmpStartInTotalSeconds = hitsPerSecond.StartInTotalSeconds;
                    }
                }

                if (!result)
                {
                    DateTime dt = DateTime.Now;
                    long totalSeconds = Convert.ToInt64((double)dt.Ticks / TimeSpan.TicksPerSecond);

                    // detecteren van rollover
                    if (hitsPerSecond.HitPerSecondArray == null || (totalSeconds - hitsPerSecond.StartInTotalSeconds) >= hitsPerSecond.HitPerSecondArray.Length)
                    {
                        lock (lockObject)
                        {
                            // Hits per seconden berekenen heeft alleen zin als er metingen zijn
                            if (hitsPerSecond.HitPerSecondArray != null)
                            {
                                calcHitsPerSecond = hitsPerSecond;
                                result = true;
                            }

                            hitsPerSecond = new HitsPerSecondLog();
                            hitsPerSecond.HitPerSecondArray = new int[sizeHitsPerSecondLog];
                            hitsPerSecond.StartInTotalSeconds = totalSeconds; // "nieuwe rollover"
                        }
                    }
                }

                return result;
            }
        }

        public static MinMaxAvgHit[] NewHitsPerDayArray()
        {
            MinMaxAvgHit[] result = new MinMaxAvgHit[24 * 60];

            for (int i = 0; i < result.Length; i++)
            {
                result[i].avgHitsPerSecond = -1;
                result[i].minHitsPerSecond = -1;
                result[i].maxHitsPerSecond = -1;
            } //foreach

            return result;
        }

        public static MinMaxAvgHit[] CurrentHitsPerDay
        {
            get
            {
                MinMaxAvgHit[] result = NewHitsPerDayArray();
                lock (lockObject)
                {
                    // Maak een kopie zodat we geen concurrent problemen kunnen krijgen
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (currentMinMaxAvgHits == null)
                        {
                            result[i].avgHitsPerSecond = -1;
                            result[i].minHitsPerSecond = -1;
                            result[i].maxHitsPerSecond = -1;
                        }
                        else
                        {
                            result[i].avgHitsPerSecond = currentMinMaxAvgHits[i].avgHitsPerSecond;
                            result[i].minHitsPerSecond = currentMinMaxAvgHits[i].minHitsPerSecond;
                            result[i].maxHitsPerSecond = currentMinMaxAvgHits[i].maxHitsPerSecond;
                        }
                    } //foreach
                } //lock

                return result;
            }
            set
            {
                lock (lockObject)
                {
                    currentMinMaxAvgHits = value;
                    // Belangrijk, default staat deze op op MinValue van date en dat betekent dus dat
                    // de gegevens bij de eerts eupdate calc worden overschreven
                    currentDTMinMaxAvgHits = DateTime.Now.Date;
                }
            }
        }

        /// <summary>
        /// De gegevens blijven bewaard tot de volgende dag! of totdat de webservice wordt gestopt
        /// </summary>
        public static MinMaxAvgHit[] PreviousHitsPerDay
        {
            get
            {
                MinMaxAvgHit[] result;
                lock (lockObject)
                {
                    result = previousMinMaxAvgHits;
                } //lock

                return result;
            }
        }

        public static bool PreviousHitsPerDayIsAvailable
        {
            get
            {
                lock (lockObject)
                {
                    return previousMinMaxAvgHits != null;
                } //lock
            }
        }
        #endregion

        /// <summary>
        /// Vult minuut gemiddelde & minuut per dag gemiddelde
        /// </summary>
        public static void UpdateHitsPerSecond()
        {
            HitsPerSecondLog hitsPerSecondlog;
            lock (lockObject)
            {
                if (calcHitsPerSecond == null)
                {
                    return;
                }
                hitsPerSecondlog = (HitsPerSecondLog)calcHitsPerSecond;
                calcHitsPerSecond = null;
            }


            // Hier berekenen we de avg, min en max request per seconds waarde
            long totalHits = 0;
            for (int i = 0; i < hitsPerSecondlog.HitPerSecondArray.Length; i++)
            {
                totalHits += ((HitsPerSecondLog)hitsPerSecondlog).HitPerSecondArray[i];
            } //for
            // Nu min, max en average berekenen over deze periode (10 minuten)                            
            double hitPerSecond = Convert.ToDouble(totalHits) / hitsPerSecondlog.HitPerSecondArray.Length; // nu in hits per seconds

            // Hebben we een overrun/overroll
            if ((DateTime.Now.Date - currentDTMinMaxAvgHits.Date).TotalDays != 0)
            {
                lock (lockObject)
                {
                    previousMinMaxAvgHits = currentMinMaxAvgHits;

                    currentDTMinMaxAvgHits = DateTime.Now.Date;
                    currentMinMaxAvgHits = null;
                }
            }

            lock (lockObject)
            {
                if (currentMinMaxAvgHits == null)
                {
                    currentMinMaxAvgHits = NewHitsPerDayArray(); // array voor de hele dag
                }

                if (hitPerSecond > maxHitsPerSecond)
                {
                    maxHitsPerSecond = hitPerSecond;
                }
                if (minHitsPerSecond < 0 || hitPerSecond < minHitsPerSecond)
                {
                    minHitsPerSecond = hitPerSecond;
                }

                // Store hits account for the whole day
                int minutes = Convert.ToInt32((DateTime.Now - currentDTMinMaxAvgHits.Date).TotalMinutes);
                if (currentMinMaxAvgHits.Length > minutes)
                {
                    currentMinMaxAvgHits[minutes].avgHitsPerSecond = hitPerSecond; // GEEN gemiddeld avergae gebruiken hier
                    currentMinMaxAvgHits[minutes].minHitsPerSecond = minHitsPerSecond;
                    currentMinMaxAvgHits[minutes].maxHitsPerSecond = maxHitsPerSecond;
                }

                // Was er al een meting?
                if (avgHitsPerSecond < 0)
                {
                    avgHitsPerSecond = hitPerSecond;
                }
                else
                {
                    avgHitsPerSecond = (avgHitsPerSecond + hitPerSecond) / 2;
                }
            }
        }

        public static UserAccount CurrentUser
        {
            get
            {
                string auth = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Authorization"];
                string username = string.Empty;
                string password = null;
                if (!string.IsNullOrEmpty(auth))
                {
                    if (auth.StartsWith("Basic "))
                    {
                        auth = DecodeBase64(auth.Substring(6));
                        string[] userPassword = auth.Split(new char[] { ':' }, 2);
                        if (userPassword.Length == 2)
                        {
                            username = userPassword[0];
                            password = userPassword[1];
                        }
                    }
                }

                UserAccount ua;
                if (userDatabase.TryGetValue(username, out ua))
                {
                    if (ua.Password != password)
                    {
                        ua = null;
                    }
                }

                return ua;
            }
        }
        /// <summary>
        /// Controleert of de gebruiker gegevens kloppen. Daarna wordt gecontroleerd
        /// of NumberOfDifferentIPsPerHour en NumberOfRequestsPerHour niet wordt overtreden.
        /// 
        /// Als alles klopt true anders false.
        /// </summary>
        public static bool HasAccess(string username, string password, AccessGroupList[] methodAccessGroup, out string errorMessage, bool updateAccountInfo)
        {
            errorMessage = string.Empty;

            UserAccount ua;
            if (userDatabase.TryGetValue(username, out ua))
            {
                if (ua.Password != password)
                {
                    errorMessage = "Invalid user credentials.";
                }
                else
                {
                    // User is authenticated. nu kijken of andere dingen nog wel kloppen
                    if (updateAccountInfo)
                    {
                        string ip = "0.0.0.0";
                        if (OperationContext.Current != null)
                        {
                            ip = CDR.WebService.ServiceHelper.CurrentClientIP; 
                        }

                        // Werk statistieken bij
                        ua.UpdateAccouting(ip);
                    }


                    // Nu testen of account niet over zijn ingestelde "limiet" gaat
                    if (ua.OverLimitOfDifferentIPPerHour)
                    {
                        errorMessage = "Too many different ip using the same account (max=" + ua.NumberOfDifferentIPsPerHour.ToString() + "/hour).";
                    }
                    else if (ua.OverLimitOfRequestPerHour)
                    {
                        errorMessage = string.Format("Too many requests per hour for this account (max={0}/hour).", ua.NumberOfRequestsPerHour);
                    }
                        // Nu controleren of user toegang heeft tot de methode
                    else if (!ua.HasAccessTo(methodAccessGroup))
                    {
                        errorMessage = "User has no access to use this REST method call. If you think this is incorrect please contact automatisering(at)cdr.nl";
                    }
                }
            }
            else
            {
                errorMessage = "Invalid user credentials.";
            }


            // Als er geen errormessage is dan is alles goed gegaan
            return (errorMessage.Length == 0);
        }

        /// <summary>
        /// Same but doesn't update the account info
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static bool HasAccess(string username, string password, AccessGroupList[] methodAccessGroup, out string errorMessage)
        {
            return HasAccess(username, password, methodAccessGroup, out errorMessage, false);
        }

        public static bool HasAccess(AccessGroupList[] methodAccessGroup, out string errorMessage, bool updateAccountInfo)
        {
            string username = string.Empty;
            string password = string.Empty;

            // get authorization header
            if (System.ServiceModel.Web.WebOperationContext.Current != null)
            {
                string auth = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Authorization"];
                if (!string.IsNullOrEmpty(auth))
                {
                    if (auth.StartsWith("Basic "))
                    {
                        auth = DecodeBase64(auth.Substring(6));
                        string[] userPassword = auth.Split(new char[] { ':' }, 2);
                        if (userPassword.Length == 2)
                        {
                            username = userPassword[0];
                            password = userPassword[1];
                        }
                    }
                }
            }

            return HasAccess(username, password, methodAccessGroup, out errorMessage, updateAccountInfo);
        }

        /// <summary>
        /// Zoekt op welke RESTAccessGroupAttribute de calling method heeft. Als er geen wordt gevonden
        /// dan wordt AccessGroupList.Nobody teruggegeven
        /// </summary>
        /// <returns></returns>
        public static AccessGroupList[] CurrentAccessGroup
        {
            get
            {
                MethodBase methodBase = new System.Diagnostics.StackFrame(1).GetMethod();
                Type classType = methodBase.DeclaringType;

                foreach(Type t in classType.GetInterfaces()) // dit levert de interfaces op (alle 3 op dit moment, omdat we ze in 1 class vinden)
                {
                    foreach (Attribute a in Attribute.GetCustomAttributes(t))
                    {
                        if (a is ServiceContractAttribute)
                        {
                            System.Reflection.MemberInfo mi = t.GetMethod(methodBase.Name);
                            if (mi != null)
                            {
                                RESTAccessGroupAttribute[] attributes = (RESTAccessGroupAttribute[])mi.GetCustomAttributes(typeof(RESTAccessGroupAttribute), false);

                                // Geef alleen de "eerste" group terug
                                if (attributes.Length > 0)
                                {
                                    AccessGroupList[] list = new AccessGroupList[attributes.Length];
                                    for (int i = 0; i < attributes.Length; i++)
                                    {
                                        list[i] = attributes[i].Group;
                                    } //for

                                    return list;
                                }
                            }
                        } //if ServiceContractAttribute
                    } //foreach
                } //foreach

                return new AccessGroupList[] { AccessGroupList.Nobody };
            }
        }


        private static string DecodeBase64(string str)
        {
            byte[] decbuff = Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(decbuff);
        }

        public static string CurrentBasicAuthenticatedUsername
        {
            get
            {
                string username = string.Empty;

                if (System.ServiceModel.Web.WebOperationContext.Current != null)
                {
                    // get authorization header
                    string auth = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Authorization"];
                    if (!string.IsNullOrEmpty(auth))
                    {
                        if (auth.StartsWith("Basic "))
                        {
                            auth = DecodeBase64(auth.Substring(6));
                            string[] userPassword = auth.Split(new char[] { ':' }, 2);
                            if (userPassword.Length == 2)
                            {
                                username = userPassword[0];
                            }
                        }
                    }
                }

                return username;
            }
        }

        public static string CurrentBasicAuthenticatedPassword
        {
            get
            {
                string password = string.Empty;

                if (System.ServiceModel.Web.WebOperationContext.Current != null)
                {
                    // get authorization header
                    string auth = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Authorization"];
                    if (!string.IsNullOrEmpty(auth))
                    {
                        if (auth.StartsWith("Basic "))
                        {
                            auth = DecodeBase64(auth.Substring(6));
                            string[] userPassword = auth.Split(new char[] { ':' }, 2);
                            if (userPassword.Length == 2)
                            {
                                password = userPassword[1];
                            }
                        }
                    }
                }

                return password;
            }
        }

        /// <summary>
        /// Geeft clone van "dictornary" + clones van de UserAccounts
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, UserAccount> CloneUserDatabase()
        {
            Dictionary<string, UserAccount> cloneDB = new Dictionary<string, UserAccount>();
            lock (lockObject)
            {
                foreach (UserAccount ua in userDatabase.Values)
                {
                    cloneDB.Add(ua.Username, ua.Clone());
                } //foreach                    
            }

            return cloneDB;
        }

        /// <summary>
        /// Geeft "echte" userAccount object terug
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static UserAccount Account(string username)
        {
            UserAccount ua = null;
            lock (lockObject)
            {
                userDatabase.TryGetValue(username, out ua);   
            }

            return ua;
        }

        public static void ResetGlobalUserRequests(string username)
        {
            UserAccount ua;
            if (userDatabase.TryGetValue(username, out ua))
            {
                ua.NumberOfRequests = 0;
            }
        }
    }

    /// <summary>
    /// Deze class is "thread"-safe
    /// </summary>
    public class UserAccount
    {
        private object lockAccount = new object();
        private int tag = 0;
        private string username = null;
        private string password = null;
        private List<AccessGroupList> accessGroupList = new List<AccessGroupList>();
        private int numberOfDifferentIPsPerHour = 1; // -1 = unlimited
        private int numberOfRequestsPerHour = 1; // -1 = unlimited
        private Int64 numberOfRequests = 0; // Total number of request since last "reset"

        private List<string> ipList = new List<string>();
        private Dictionary<string, IPRequestInfo> ipDictionary = new Dictionary<string, IPRequestInfo>();
        private List<int> requestList = new List<int>();

        public UserAccount()
        {
            accessGroupList.Clear();
            // Every user belongs to this groep
            accessGroupList.Add(RadioChannelWebService.AccessGroupList.Everybody);

            requestList.Clear();
            // Nu voor elk uur in de dag lijst vullen (met waarde 0)
            for (int i = 0; i <= 23; i++)
            {
                requestList.Add(0);
            } //for
        }

        /// <summary>
        /// Maakt een kopie van de huidige gegevens die in deze class zitten
        /// </summary>
        /// <returns></returns>
        public UserAccount Clone()
        {
            UserAccount ua = new UserAccount();
            lock (lockAccount)
            {
                ua.tag = this.tag;
                ua.username = this.username;
                ua.password = this.password;
                ua.numberOfDifferentIPsPerHour = this.numberOfDifferentIPsPerHour;
                ua.numberOfRequestsPerHour = this.numberOfRequestsPerHour;
                ua.numberOfRequests = this.numberOfRequests;
                foreach (RadioChannelWebService.AccessGroupList group in this.accessGroupList)
                {
                    // Everbody is allready present
                    if (group != RadioChannelWebService.AccessGroupList.Everybody)
                    {
                        ua.accessGroupList.Add(group);
                    }
                } //foreach
            }

            return ua;
        }

        public int Tag
        {
            get
            {
                lock (lockAccount)
                {
                    return tag;
                }
            }
            set
            {
                lock (lockAccount)
                {
                    tag = value;
                }
            }
        }

        public string Username
        {
            get
            {
                lock (lockAccount)
                {
                    return username;
                }
            }
            set
            {
                lock (lockAccount)
                {
                    username = value;
                }
            }
        }

        public string Password
        {
            get
            {
                lock (lockAccount)
                {
                    return password;
                }
            }
            set
            {
                lock (lockAccount)
                {
                    password = value;
                }
            }
        }

        public List<AccessGroupList> AccessGroupList
        {
            get
            {
                lock (lockAccount)
                {
                    return accessGroupList;
                }
            }
        }
        

        public int NumberOfDifferentIPsPerHour
        {
            get
            {
                lock (lockAccount)
                {
                    return numberOfDifferentIPsPerHour;
                }
            }
            set
            {
                if (value > 1000)
                {
                    value = 1000;
                }

                lock (lockAccount)
                {
                    numberOfDifferentIPsPerHour = value;

                    // tets op >= 0 nodige vanwege unlimited die -1 is
                    if (numberOfDifferentIPsPerHour >= 0)
                    {
                        while (ipList.Count > numberOfDifferentIPsPerHour)
                        {
                            // remove oldest entries
                            ipDictionary.Remove(ipList[0]);
                            ipList.RemoveAt(0);
                        } //while
                    }
                    ipList.TrimExcess();
                } //lock
            }
        }


        public int NumberOfRequestsPerHour
        {
            get
            {
                lock (lockAccount)
                {
                    return numberOfRequestsPerHour;
                }
            }
            set
            {
                lock (lockAccount)
                {
                    numberOfRequestsPerHour = value;
                }
            }
        }

        public Int64 NumberOfRequests
        {
            get
            {
                lock (lockAccount)
                {
                    return numberOfRequests;
                }
            }
            set
            {
                lock (lockAccount)
                {
                    numberOfRequests = value;
                }
            }
        }

        public bool HasAccessTo(AccessGroupList[] methodAccessGroup)
        {
            lock (lockAccount)
            {
                foreach (AccessGroupList group in methodAccessGroup)
                {
                    // Everybody zoeken we niet op, iedereen heeft toegang hiertoe
                    if (group == RadioChannelWebService.AccessGroupList.Everybody)
                    {
                        return true;
                    }

                    if (accessGroupList.Contains(group))
                    {
                        return true;
                    }
                } // foreach
            } //lock

            return false;
        }

        /// <summary>
        /// Werkt statistieken bij. Zorg dat dit maar 1x wordt aangeroepen over de gehele 
        /// REST call!!!
        /// </summary>
        /// <param name="ip"></param>
        public void UpdateAccouting(string ip)
        {
            lock (lockAccount)
            {
                // Update global user count request
                numberOfRequests++;

                IPRequestInfo ipInfo;

                // Eerst verlopen entries verwijderen!
                while (ipList.Count > 0)
                {
                    if (ipDictionary.TryGetValue(ipList[0], out ipInfo))
                    {
                        if (Convert.ToInt32((System.DateTime.Now - ipInfo.lastAccessed).TotalSeconds) > 3600)
                        {
                            // Entry is verlopen, dus verwijderen
                            ipDictionary.Remove(ipList[0]);
                            ipList.RemoveAt(0);
                        }
                        else
                        {
                            // We zijn klaar met verlopen entries verwijderen
                            break;
                        }
                    }
                } //while

                // Als we (NumberOfDifferentIPsPerHour+1) hebben hier dan 
                // De allertaatste verwijderen (die krijgt toch geen toegang)
                // zodat in de logica in ipList de laatste entry altijd deze is,


                if (NumberOfDifferentIPsPerHour >= 0)
                {
                    if (ipList.Count > NumberOfDifferentIPsPerHour)
                    {
                        ipDictionary.Remove(ipList[ipList.Count - 1]);
                        ipList.RemoveAt(ipList.Count - 1);
                    }
                }

                // Nu entry toevoegen of bijwerken
                if (ipDictionary.TryGetValue(ip, out ipInfo))
                {
                    // De entry bestaat al
                    ipList.Remove(ip);
                    ipInfo.lastAccessed = DateTime.Now;
                }
                else
                {
                    // Het is een nieuwe entry
                    ipInfo = new IPRequestInfo();
                    ipInfo.IPNumber = ip;
                    ipInfo.lastAccessed = DateTime.Now;

                    ipDictionary.Add(ip, ipInfo);
                }

                // Voeg ip toe aan het einde van de lijst
                ipList.Add(ip);



                // Nu voor berekening aantal requests per uur
                // We zetten (toekomstige uur op 0) en tellen bij huidige uur 1 op
                int hour = DateTime.Now.Hour;
                if (hour >= 23)
                {
                    requestList[0] = 0;
                }
                else
                {
                    requestList[hour + 1] = 0;
                }

                requestList[hour] += 1;
            }
        }

        /// <summary>
        /// Zijn we over limiet van teveel verschillende ip nummer dat deze REST service wordt benaderd
        /// </summary>
        public bool OverLimitOfDifferentIPPerHour
        {
            get
            {
                // Als we teveel entries hebben dan zijn we over de limiet (anders hebben we namelijk niet teveel entries!,
                // daar zorgt logica in "UpdateAccouting" voor.)

                if (NumberOfDifferentIPsPerHour < 0)
                {
                    // Unlimited
                    return false;
                }
                else if (NumberOfDifferentIPsPerHour == 0)
                {
                    // disabled
                    return true;
                }
                else
                {
                    return (ipList.Count > NumberOfDifferentIPsPerHour);
                }
            }
        }


        /// <summary>
        /// Zijn er teveel verzoeken per uur?
        /// Let op de berekening is over een uur. Dus na een uur begint het opnieuw.
        /// </summary>
        public bool OverLimitOfRequestPerHour
        {
            get
            {
                if (NumberOfRequestsPerHour < 0)
                {
                    // Unlimited
                    return false;
                }
                else if (NumberOfRequestsPerHour == 0)
                {
                    // disabled
                    return true;
                }
                else
                {
                    return (requestList[DateTime.Now.Hour] > NumberOfRequestsPerHour);
                }
            }
        }



        private struct IPRequestInfo
        {
            public string IPNumber;
            public DateTime lastAccessed;
        }
    }

    public struct HitsPerSecondLog
    {
        public int[] HitPerSecondArray;
        public long StartInTotalSeconds;
    }

    public struct MinMaxAvgHit
    {
        public double avgHitsPerSecond;
        public double minHitsPerSecond;
        public double maxHitsPerSecond;
    }
}

