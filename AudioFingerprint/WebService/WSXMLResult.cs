using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AudioFingerprint.WebService
{
    public enum RecognizeCode : int
    {
        OK = 0,
        EXCEPTION,
        TIMEOUT,
        SERVERNOTFOUND
    }

    public class ResultFingerprintRecognition
    {
        public RecognizeCode RecognizeCode = RecognizeCode.OK; // 0=OK, 1=EXCEPTION, 2=TIMEOUT, 3=servernotfound
        public string RecognizeResult = string.Empty;
        public TimeStatistics TimeStatistics = new TimeStatistics();
        public List<FingerTrack> FingerTracks = new List<FingerTrack>();

        public bool ParseFingerprintRecognition(XmlElement xResult)
        {
            try
            {
                RecognizeCode = (RecognizeCode)Convert.ToInt32(xResult.GetElementsByTagName("RecognizeResult")[0].Attributes["RecognizeCode"].Value);
                RecognizeResult = xResult.GetElementsByTagName("RecognizeResult")[0].InnerText;

                if (!TimeStatistics.ParseTimeStatistics((XmlElement)xResult.GetElementsByTagName("TimeStatistics")[0]))
                {
                    return false;
                }
                
                foreach (XmlElement xFingerTrack in xResult.GetElementsByTagName("FingerTrack"))
                {
                    FingerTrack FingerTrack = new FingerTrack();
                    if (!FingerTrack.ParseFingerTrack(xFingerTrack))
                    {
                        return false;
                    }
                    FingerTracks.Add(FingerTrack);
                } //foreach
                
                return true;
            }
            catch
            {
            }

            return false;
        }
    }

    public class TimeStatistics
    {
        public TimeSpan TotalQueryTime = TimeSpan.Zero;
        public TimeSpan SubFingerQueryTime = TimeSpan.Zero;
        public TimeSpan FingerLoadTime = TimeSpan.Zero;
        public TimeSpan MatchTime = TimeSpan.Zero;

        public bool ParseTimeStatistics(XmlElement xTimeStatistics)
        {
            try
            {
                TotalQueryTime = TimeSpan.FromMilliseconds(Convert.ToDouble(xTimeStatistics["TotalQueryTime"].InnerText));
                SubFingerQueryTime = TimeSpan.FromMilliseconds(Convert.ToDouble(xTimeStatistics["SubFingerQueryTime"].InnerText));
                FingerLoadTime = TimeSpan.FromMilliseconds(Convert.ToDouble(xTimeStatistics["FingerLoadTime"].InnerText));
                MatchTime = TimeSpan.FromMilliseconds(Convert.ToDouble(xTimeStatistics["MatchTime"].InnerText));

                return true;
            }
            catch
            {
            }

            return false;
        }
    }

    public class FingerTrack
    {
        public float Score = 0.0f;
        public int BER = int.MaxValue;

        public long DetectPositionInMS = 0;
        public float DetectPositionInSec = 0.0f;

        public SearchStrategy SearchStrategy = new SearchStrategy();

        public string FingerTrackReference = "";
        public long FingerTrackID = 0;

        public bool ParseFingerTrack(XmlElement xFingerTrack)
        {
            try
            {
                BER = Convert.ToInt32(xFingerTrack.Attributes["BER"].Value, ResultSetHelper.NumberFormatInfo);

                DetectPositionInSec = Convert.ToSingle(xFingerTrack["DetectPosition"].Attributes["InSec"].Value, ResultSetHelper.NumberFormatInfo);
                DetectPositionInMS = Convert.ToInt64(xFingerTrack["DetectPosition"].InnerText, ResultSetHelper.NumberFormatInfo);
                SearchStrategy.ParseSearchStrategy((XmlElement)xFingerTrack.GetElementsByTagName("SearchStrategy")[0]);

                FingerTrackReference = xFingerTrack["Reference"].InnerText;
                FingerTrackID = Convert.ToInt64(xFingerTrack["FingerTrackID"].InnerText);

                return true;
            }
            catch
            {
            }

            return false;
        }
    }


    public class ResultSongs
    {
        public List<Song> Songs = new List<Song>();

        public bool ParseSongs(XmlElement xResult)
        {
            try
            {
                foreach (XmlElement xSong in xResult.GetElementsByTagName("Song"))
                {
                    Song Song = new Song();
                    if (!Song.ParseSong(xSong))
                    {
                        return false;
                    }
                    Songs.Add(Song);
                } //foreach

                return true;
            }
            catch
            {
            }

            return false;
        }
    }

    public class Performer : ICloneable
    {
        public double Score = 0.0;                                      // Not always available
        public string Link = "";
        public string PresentationName = "";
        public string SortedName = "";
        public string Years = "";
        public string Annotation = "";
        public string PrimaryCatalogueCode = "";
        public string PrimaryRoleCode = "";
        public string Role = "";
        public string Role_Code = "";
        public bool IsImportantPerformer = false;                        // Not always available (eg AlbumInformation)
        public string Article = "";                                     // Not always available (eg album/song)
        public string ArticleLanguage = "";                             // Not always available
        public List<string> AvailableMedia = new List<string>();        // Not always available (eg song)
        public string Catalogue = "";                                   // Not always available (eg song)
        public List<string> PerformerCatalogue = new List<string>();    // Not always available (eg song)
        public string PrimaryCatalogue = "";                            // Not always available

        public bool ParsePerformer(XmlElement xPerformer)
        {
            try
            {
                Score = 0.0;
                if (xPerformer.Attributes["Score"] != null)
                {
                    Score = Convert.ToDouble(xPerformer.Attributes["Score"].Value, ResultSetHelper.NumberFormatInfo);
                }
                Link = xPerformer.Attributes["Link"].Value;
                PresentationName = xPerformer["PresentationName"].InnerText;
                SortedName = xPerformer["SortedName"].InnerText;
                Years = xPerformer["Years"].InnerText;
                Annotation = xPerformer["Annotation"].InnerText;
                PrimaryCatalogueCode = xPerformer["PrimaryCatalogueCode"].InnerText;
                PrimaryRoleCode = xPerformer["PrimaryRoleCode"].InnerText;
                IsImportantPerformer = false;
                if (xPerformer.Attributes["IsImportantPerformer"] != null)
                {
                    IsImportantPerformer = (xPerformer["IsImportantPerformer"].InnerText != "0");
                }
                if (xPerformer["Role"] != null)
                {
                    Role = xPerformer["Role"].InnerText;
                    Role_Code = xPerformer["Role"].Attributes["Code"].Value;
                }

                Article = "";
                ArticleLanguage = "";
                if (xPerformer["Article"] != null)
                {
                    Article = xPerformer["Article"].InnerText;
                    ArticleLanguage = xPerformer["PresentationName"].InnerText;
                }

                AvailableMedia.Clear();
                if (xPerformer["AvailableMedia"] != null)
                {
                    foreach (string media in xPerformer["AvailableMedia"].InnerText.Split(' '))
                    {
                        AvailableMedia.Add(media);
                    } //foreach
                }

                Catalogue = "";
                if (xPerformer["Catalogue"] != null)
                {
                    Catalogue = xPerformer["Catalogue"].InnerText;
                }
                PerformerCatalogue.Clear();
                if (xPerformer["PerformerCatalogue"] != null)
                {
                    foreach (string catalogue in xPerformer["PerformerCatalogue"].InnerText.Split(' '))
                    {
                        PerformerCatalogue.Add(catalogue);
                    } //foreach
                }
                if (xPerformer["PrimaryCatalogue"] != null)
                {
                    PrimaryCatalogue = xPerformer["PrimaryCatalogue"].InnerText;
                }

                return true;
            }
            catch
            {
            }

            return false;
        }

        #region ICloneable interface implementation

        /// <summary>
        /// Deep clone
        /// </summary>
        public object Clone()
        {
            Performer clone = new Performer();
            clone.Score = Score;
            clone.Link = Link;
            clone.PresentationName = PresentationName;
            clone.SortedName = SortedName;
            clone.Years = Years;
            clone.Annotation = Annotation;
            clone.PrimaryCatalogueCode = PrimaryCatalogueCode;
            clone.PrimaryRoleCode = PrimaryRoleCode;
            clone.Role = Role;
            clone.Role_Code = Role_Code;
            clone.IsImportantPerformer = IsImportantPerformer;
            clone.Article = Article;
            clone.ArticleLanguage = ArticleLanguage;
            foreach (string s in AvailableMedia)
            {
                clone.AvailableMedia.Add(s);
            } //foreach
            clone.Catalogue = Catalogue;
            foreach (string s in PerformerCatalogue)
            {
                clone.PerformerCatalogue.Add(s);
            }
            clone.PrimaryCatalogue = PrimaryCatalogue;

            return clone;
        }

        #endregion
    }

    /// <summary>
    /// Location of digital files
    /// </summary>
    public class Location : ICloneable
    {
        public string Location_Name = "";
        public string Location_IFL_Number = "";
        public string LocationValue = "";

        public bool ParseLocation(XmlElement xLocation)
        {
            try
            {
                Location_Name = xLocation.Attributes["Name"].Value;
                Location_IFL_Number = xLocation.Attributes["IFL_Number"].Value;
                LocationValue = xLocation.InnerText;

                return true;
            }
            catch
            {
            }

            return false;
        }

        #region ICloneable interface implementation

        /// <summary>
        /// Deep clone
        /// </summary>
        public object Clone()
        {
            Location clone = new Location();

            clone.Location_Name = Location_Name;
            clone.Location_IFL_Number = Location_IFL_Number;
            clone.LocationValue = LocationValue;

            return clone;
        }

        #endregion
    }

    public class Media : ICloneable
    {
        public string MediaType = "";
        public string MediaCode = "";
        public string MediaDescription = "";
        public int NumberOfDiscs = 0;

        public bool ParseMedia(XmlElement xMedia)
        {
            try
            {
                MediaType = xMedia["MediaType"].InnerText;
                MediaCode = xMedia["MediaCode"].InnerText;
                MediaDescription = xMedia["MediaDescription"].InnerText;
                NumberOfDiscs = Convert.ToInt32(xMedia["NumberOfDiscs"].InnerText);

                return true;
            }
            catch
            {
            }

            return false;
        }

        #region ICloneable interface implementation

        /// <summary>
        /// Deep clone
        /// </summary>
        public object Clone()
        {
            Media clone = new Media();

            clone.MediaType = MediaType;
            clone.MediaCode = MediaCode;
            clone.MediaDescription = MediaDescription;
            clone.NumberOfDiscs = NumberOfDiscs;

            return clone;
        }

        #endregion
    }

    public class OrderInfo : ICloneable
    {
        public int SortOrder = 0;
        public string LabelName = "";
        public string LabelName_Link = "";
        public bool LabelName_Checked = false;
        public string LabelNumber = "";
        public string Distributor = "";
        public double Price = 0.0;
        public string Price_Currency = "";
        public string EAN = "";

        public bool ParseOrderInfo(XmlElement xOrderInfo)
        {
            try
            {
                SortOrder = Convert.ToInt32(xOrderInfo.Attributes["SortOrder"].Value);
                LabelName = xOrderInfo["LabelName"].InnerText;
                LabelName_Link = xOrderInfo["LabelName"].Attributes["Link"].Value;
                LabelName_Checked = (xOrderInfo["LabelName"].Attributes["Checked"].Value == "1");
                LabelNumber = xOrderInfo["LabelNumber"].InnerText;
                Distributor = xOrderInfo["Distributor"].InnerText;
                Price = Convert.ToDouble(xOrderInfo["Price"].InnerText, ResultSetHelper.NumberFormatInfo);
                Price_Currency = xOrderInfo["Price"].Attributes["Currency"].Value;
                EAN = xOrderInfo["EAN"].InnerText;

                return true;
            }
            catch
            {
            }

            return false;
        }

        #region ICloneable interface implementation

        /// <summary>
        /// Deep clone
        /// </summary>
        public object Clone()
        {
            OrderInfo clone = new OrderInfo();

            clone.SortOrder = SortOrder;
            clone.LabelName = LabelName;
            clone.LabelName_Link = LabelName_Link;
            clone.LabelName_Checked = LabelName_Checked;
            clone.LabelNumber = LabelNumber;
            clone.Distributor = Distributor;
            clone.Price = Price;
            clone.Price_Currency = Price_Currency;
            clone.EAN = EAN;

            return clone;
        }

        #endregion
    }
    
    public class AlbumMusicStyle
    {
        public int SortOrder = 1;
        public string MainCategory = "";
        public string MainCategory_Link = "";
        public string Category = "";
        public string Category_Link = "";
        public string Style = "";
        public string Style_Link = "";

        public bool ParseMusicStyle(XmlElement xMusicStyle)
        {
            try
            {
                SortOrder = Convert.ToInt32(xMusicStyle.Attributes["SortOrder"].Value);
                MainCategory = xMusicStyle["MainCategory"].InnerText;
                MainCategory_Link = xMusicStyle["MainCategory"].Attributes["Link"].Value;
                Category = xMusicStyle["Category"].InnerText;
                Category_Link = xMusicStyle["Category"].Attributes["Link"].Value;
                Style = xMusicStyle["Style"].InnerText;
                Style_Link = xMusicStyle["Style"].Attributes["Link"].Value;

                return true;
            }
            catch
            {
            }

            return false;
        }
    }

    public class Album : ICloneable
    {
        public string ContextID = "";                                                                   // Not always available (eg Song)
        public string AlbumID = "";
        public string CatalogueID = "";
        public string AlbumURL = "";                                                                    // Not always available (eg Song)
        public bool Cover_HasCover = false;                                                             // Not always available (eg Song)
        public string Cover = "";                                                                       // Not always available (eg Song)
        public List<string> AvailableMedia = new List<string>();
        public string Catalogue = "";
        public DateTime ReleaseDate = DateTime.MinValue;
        public string AlbumTitle = "";
        public List<Performer> Performers = new List<Performer>();
        public bool LendingGlobal = false; // IFL or not                                                
        public int LendingGlobal_Code = 0; // IFL_CODE (0=IFL, 1=NIETIFL, 2=DEFNIETIFL)                 
        public Media Media = new Media();
        public string AlbumArticle = "";
        public string AlbumArticle_Language = "";
        public double UserRatingAlbumAVG = 0.0;
        public int UserRatingAlbumAVG_RatingAlbumCount = 0;
        public List<string> MusicStyles = null;                                                         // Only available with songs (Not supported at the moment!)

        // Special for App support not in XML result sets!!!
        public long TotalPlayTimeInSec = -1;
        public int TrackNumberCount = -1;
        public List<OrderInfo> OrderInfo = new List<OrderInfo>();                                      // NOT FILLED, just here for specific call to transform AlibumInfo to Album class

        public bool ParseAlbum(XmlElement xAlbum)
        {
            try
            {
                ContextID = "";
                if (xAlbum.Attributes["ContextID"] != null)
                {
                    ContextID = xAlbum.Attributes["ContextID"].Value;
                }
                AlbumID = xAlbum["AlbumID"].InnerText;
                CatalogueID = xAlbum["CatalogueID"].InnerText;

                AlbumURL = "";
                if (xAlbum["AlbumURL"] != null)
                {
                    AlbumURL = xAlbum["AlbumURL"].InnerText;
                }

                Cover = "";
                Cover_HasCover = false;
                if (xAlbum["Cover"] != null)
                {
                    Cover = xAlbum["Cover"].InnerText;
                    Cover_HasCover = (xAlbum["Cover"].Attributes["HasCover"].Value == "1");
                }

                foreach (string media in xAlbum["AvailableMedia"].InnerText.Split(' '))
                {
                    AvailableMedia.Add(media);
                } //foreach        
                Catalogue = xAlbum["Catalogue"].InnerText;
                ReleaseDate = Convert.ToDateTime(xAlbum["ReleaseDate"].InnerText, ResultSetHelper.CultureInfo);
                AlbumTitle = xAlbum["AlbumTitle"].InnerText;

                Performers.Clear();
                if (xAlbum.GetElementsByTagName("Performers")[0] != null)
                {
                    XmlElement xPerformers = (XmlElement)xAlbum.GetElementsByTagName("Performers")[0];
                    foreach (XmlElement xPerformer in xPerformers.GetElementsByTagName("Performer"))
                    {
                        Performer performer = new Performer();
                        if (!performer.ParsePerformer(xPerformer))
                        {
                            return false;
                        }

                        Performers.Add(performer);
                    } //foreach
                }

                LendingGlobal = (xAlbum["LendingGlobal"].InnerText == "1");
                LendingGlobal_Code = Convert.ToInt32(xAlbum["LendingGlobal"].Attributes["Code"].Value);

                if (!Media.ParseMedia((XmlElement)xAlbum.GetElementsByTagName("Media")[0]))
                {
                    return false;
                }

                AlbumArticle = xAlbum["AlbumArticle"].InnerText;
                AlbumArticle_Language = xAlbum["AlbumArticle"].Attributes["Language"].Value;
                UserRatingAlbumAVG = Convert.ToDouble(xAlbum["UserRatingAlbumAVG"].InnerText, ResultSetHelper.NumberFormatInfo);
                UserRatingAlbumAVG_RatingAlbumCount = Convert.ToInt32(xAlbum["UserRatingAlbumAVG"].Attributes["RatingAlbumCount"].Value, ResultSetHelper.NumberFormatInfo);

                return true;
            }
            catch
            {
            }

            return false;
        }

        public string CoverPICO
        {
            get
            {
                return Cover;
            }
        }

        public string CoverSMALL
        {
            get
            {
                return Cover.Replace("PICO", "SMALL");
            }
        }

        public string CoverMEDIUM
        {
            get
            {
                return Cover.Replace("PICO", "MEDIUM");
            }
        }

        /// <summary>
        /// Only available inside Muziekwebplein
        /// </summary>
        public string CoverLARGE
        {
            get
            {
                return Cover.Replace("PICO", "LARGE");
            }
        }

        /// <summary>
        /// Only available inside Muziekwebplein
        /// </summary>
        public string CoverSUPERLARGE
        {
            get
            {
                return Cover.Replace("PICO", "SUPERLARGE");
            }
        }

        /// <summary>
        /// Only available inside Muziekwebplein
        /// </summary>
        public string CoverORG
        {
            get
            {
                return Cover.Replace("PICO", "ORG");
            }
        }

        #region ICloneable interface implementation

        /// <summary>
        /// Deep clone
        /// </summary>
        public object Clone()
        {
            Album clone = new Album();

            clone.ContextID = this.ContextID;
            clone.AlbumID = this.AlbumID;
            clone.CatalogueID = this.CatalogueID;
            clone.AlbumURL = this.AlbumURL;
            clone.Cover_HasCover = this.Cover_HasCover;
            clone.Cover = this.Cover;
            this.AvailableMedia.ForEach((item) =>
            {
                clone.AvailableMedia.Add(item);
            });
            clone.Catalogue = this.Catalogue;
            clone.ReleaseDate = this.ReleaseDate;
            clone.AlbumTitle = this.AlbumTitle;
            this.Performers.ForEach((item) =>
            {
                clone.Performers.Add((Performer)item.Clone());
            });
            clone.LendingGlobal = this.LendingGlobal;
            clone.LendingGlobal_Code = this.LendingGlobal_Code;
            clone.Media = (Media)this.Media.Clone();
            clone.AlbumArticle = this.AlbumArticle;
            clone.AlbumArticle_Language = this.AlbumArticle_Language;
            clone.UserRatingAlbumAVG = this.UserRatingAlbumAVG;
            clone.UserRatingAlbumAVG_RatingAlbumCount = this.UserRatingAlbumAVG_RatingAlbumCount;
            if (this.MusicStyles != null)
            {
                this.MusicStyles.ForEach((item) =>
                {
                    clone.MusicStyles.Add(item);
                });
            }
            // Special for App support not in XML result sets!!!
            clone.TotalPlayTimeInSec = this.TotalPlayTimeInSec;
            clone.TrackNumberCount = this.TrackNumberCount;
            this.OrderInfo.ForEach((item) =>
            {
                clone.OrderInfo.Add((OrderInfo)item.Clone());
            });

            return clone;
        }

        #endregion
    }
    
    public class SearchStrategy
    {
        public int IndexNumberInMatchList = -1;       
        public int SubFingerCountHitInFingerprint = -1;
        public string SearchName = string.Empty;
        public int SearchIteration = 0;

        public bool ParseSearchStrategy(XmlElement xSearchStrategy)
        {
            try
            {
                IndexNumberInMatchList = Convert.ToInt32(xSearchStrategy["IndexNumberInMatchList"].InnerText);
                SubFingerCountHitInFingerprint = Convert.ToInt32(xSearchStrategy["SubFingerCountHitInFingerprint"].InnerText);
                SearchName = xSearchStrategy["SearchName"].InnerText;
                SearchIteration = Convert.ToInt32(xSearchStrategy["SearchIteration"].InnerText);

                return true;
            }
            catch
            {
            }

            return false;
        }

    }
    
    public class Song
    {
        public string ContextID = "";

        public float Score = 0.0f;
        public int TrackNumber = 0;

        public Album Album = new Album();
        public string AlbumTrackID = "";
        public string SongTitle = "";
        public string SongTitle_Link = "";
        public string UniformTitle = "";
        public string UniformTitle_Link = "";
        public int PlayTimeInSec = 0;
        public List<Performer> Performers = new List<Performer>();

        public bool ParseSong(XmlElement xSong)
        {
            try
            {
                ContextID = xSong.Attributes["ContextID"].Value;
                Score = Convert.ToSingle(xSong.Attributes["Score"].Value, ResultSetHelper.NumberFormatInfo);
                TrackNumber = Convert.ToInt32(xSong.Attributes["TrackNumber"].Value);
                
                if (!Album.ParseAlbum((XmlElement)xSong.GetElementsByTagName("Album")[0]))
                {
                    return false;
                }

                AlbumTrackID = xSong["AlbumTrackID"].InnerText;
                SongTitle = xSong["SongTitle"].InnerText;
                SongTitle_Link = xSong["SongTitle"].Attributes["Link"].Value;
                UniformTitle = xSong["UniformTitle"].InnerText;
                UniformTitle_Link = xSong["UniformTitle"].Attributes["Link"].Value;
                PlayTimeInSec = Convert.ToInt32(xSong["PlayTimeInSec"].InnerText);

                Performers.Clear();
                // Find Tag Performers under Song tag!! (not performers under the Song/Album tag)
                XmlElement xPerformers = null;
                foreach (XmlNode node in xSong.ChildNodes)
                {
                    if (node.Name == "Performers")
                    {
                        xPerformers = (XmlElement)node;
                        break;
                    }
                } //foreach

                if (xPerformers != null)
                {
                    foreach (XmlElement xPerformer in xPerformers.GetElementsByTagName("Performer"))
                    {
                        Performer performer = new Performer();
                        if (!performer.ParsePerformer(xPerformer))
                        {
                            return false;
                        }

                        Performers.Add(performer);
                    } //foreach
                }

                return true;
            }
            catch
            {
            }

            return false;
        }
    }

    /// <summary>
    /// Helper class for Number and date conversion
    /// </summary>
    public static class ResultSetHelper
    {
        private static System.Globalization.NumberFormatInfo nfi = null;
        private static System.Globalization.CultureInfo culture = null;

        public static System.Globalization.NumberFormatInfo NumberFormatInfo
        {
            get
            {
                if (nfi == null)
                {
                    nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
                    nfi.CurrencySymbol = "â‚¬";
                    nfi.CurrencyDecimalDigits = 2;
                    nfi.CurrencyDecimalSeparator = ".";
                    nfi.NumberGroupSeparator = "";
                    nfi.NumberDecimalSeparator = ".";
                }

                return nfi;
            }
        }

        public static System.Globalization.CultureInfo CultureInfo
        {
            get
            {
                if (culture == null)
                {
                    culture = new System.Globalization.CultureInfo("en-US");
                }

                return culture;
            }
        }

        /// <summary>
        /// Get the absolute XPath to a given XElement, including the namespace.
        /// (e.g. "/a:people/b:person[6]/c:name[1]/d:last[1]").
        /// </summary>
        public static string GetAbsoluteXPath(this XElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            Func<XElement, string> relativeXPath = e =>
            {
                int index = e.IndexPosition();

                var currentNamespace = e.Name.Namespace;

                string name;
                if (currentNamespace == null)
                {
                    name = e.Name.LocalName;
                }
                else
                {
                    string namespacePrefix = e.GetPrefixOfNamespace(currentNamespace);
                    name = namespacePrefix + ":" + e.Name.LocalName;
                }

                // If the element is the root, no index is required
                return (index == -1) ? "/" + name : string.Format
                (
                    "/{0}[{1}]",
                    name,
                    index.ToString()
                );
            };

            var ancestors = from e in element.Ancestors()
                            select relativeXPath(e);

            return string.Concat(ancestors.Reverse().ToArray()) +
                   relativeXPath(element);
        }

        /// <summary>
        /// Get the index of the given XElement relative to its
        /// siblings with identical names. If the given element is
        /// the root, -1 is returned.
        /// </summary>
        /// <param name="element">
        /// The element to get the index of.
        /// </param>
        public static int IndexPosition(this XElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            if (element.Parent == null)
            {
                return -1;
            }

            int i = 1; // Indexes for nodes start at 1, not 0

            foreach (var sibling in element.Parent.Elements(element.Name))
            {
                if (sibling == element)
                {
                    return i;
                }

                i++;
            }

            throw new InvalidOperationException("element has been removed from its parent.");
        }
    }
}
