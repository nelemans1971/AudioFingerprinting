using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using CDR.Logging;

namespace CDRWebservice
{
    static class HelperCache
    {
        #region CachePolicy
        public static CacheItemPolicy ExpirePolicySliding1Hour = new CacheItemPolicy { Priority = CacheItemPriority.Default, SlidingExpiration = TimeSpan.FromHours(1) };
        public static CacheItemPolicy ExpirePolicySliding10Minutes = new CacheItemPolicy { Priority = CacheItemPriority.Default, SlidingExpiration = TimeSpan.FromMinutes(10) };

        /// <summary>
        /// After 1 hour from now the data WILL BE REMOVED!
        /// </summary>
        public static CacheItemPolicy ExpirePolicyAbsolute1Hour
        {
            get
            {
                return new CacheItemPolicy { Priority = CacheItemPriority.Default, AbsoluteExpiration = DateTimeOffset.Now.AddHours(1) };
            }
        }

        /// <summary>
        /// After 24 hours from now the data WILL BE REMOVED!
        /// </summary>
        public static CacheItemPolicy ExpirePolicyAbsolute24Hours
        {
            get
            {
                return new CacheItemPolicy { Priority = CacheItemPriority.Default, AbsoluteExpiration = DateTimeOffset.Now.AddHours(24) };
            }
        }

        /// <summary>
        /// After 5 minutesfrom now the data WILL BE REMOVED!
        /// </summary>
        public static CacheItemPolicy ExpirePolicyAbsolute5Minutes
        {
            get
            {
                return new CacheItemPolicy { Priority = CacheItemPriority.Default, AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5) };
            }
        }
        #endregion

        /// <summary>
        /// Initiate the default cache and the search cache with specific settings 
        /// </summary>
        public static void Init()
        {
        }

        /// <summary>
        /// Release memory for the default and search cache. Var will be null after this call.
        /// (You can caal init again!)
        /// </summary>
        public static void Done()
        {
        }


        public static object Get(string key)
        {
            return null;
        }

        public static bool Add(string key, object value, CacheItemPolicy policy)
        {
            return false;
        }

        public static object Remove(string key)
        {
            return null;
        }

        public static System.Xml.Linq.XElement GetXElement(string key)
        {
            return null;
        }

        public static bool Add(string key, System.Xml.Linq.XElement value, CacheItemPolicy policy)
        {
            return false;
        }

        public static void Invalidate_Cache()
        {
        }
    }

    public enum CacheType
    {
        SmallCache = 0,
        SearchCache = 1
    }
}
