// #define USEINTERNALMEMCACHED
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Caching;
using System.Text;

namespace CDRWebservice
{
    static class HelperCache
    {
        // This cache is for thing which are expected to be valid for about a day (24 hour) or even longer
        // There will not be many entries in this cache, they will be used many times.
        // Memory consumption should not be excessive
        private static ObjectCache DefaultCache = null;

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

        /// <summary>
        /// Initiate the default cache and the search cache with specific settings 
        /// </summary>
        public static void Init()
        {
#if USEINTERNALMEMCACHED
            NameValueCollection config = new NameValueCollection();
            config.Add("pollingInterval", "00:05:00");
            config.Add("physicalMemoryLimitPercentage", "0"); // Let memoryCache instance manage it them self
#if DEBUG
            config.Add("cacheMemoryLimitMegabytes", "256"); // 256mb aan cache
#else
            config.Add("cacheMemoryLimitMegabytes", "4096"); // 4096mb aan cache
#endif
            DefaultCache = new MemoryCache("XML Cache", config);
#endif
        }

        /// <summary>
        /// Release memory for the default and search cache. Var will be null after this call.
        /// (You can caal init again!)
        /// </summary>
        public static void Done()
        {
            if (DefaultCache != null)
            {
                ((MemoryCache)DefaultCache).Dispose();
                DefaultCache = null;
            }
        }

#if USEINTERNALMEMCACHED
        public static object Get(string key)
        {
            return DefaultCache.Get(key);
        }

        public static bool Add(string key, object value, CacheItemPolicy policy)
        {
            return DefaultCache.Add(key, value, policy);
        }

        public static object Remove(string key)
        {
            return DefaultCache.Remove(key);
        }

        public static void Invalidate_Cache()
        {
            List<KeyValuePair<String, Object>> cacheItems = (from n in DefaultCache.AsParallel() select n).ToList();  
            
            foreach (KeyValuePair<String, Object> a in cacheItems)     
            {
                DefaultCache.Remove(a.Key); 
            } //foreach
        }
#else
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

        public static void Invalidate_Cache()
        {
        }

#endif
    }

    public enum CacheType
    {
        SmallCache = 0,
        SearchCache = 1
    }
}
