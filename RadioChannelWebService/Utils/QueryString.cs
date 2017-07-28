using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Web;
using System.ServiceModel.Web;

namespace RadioChannelWebService
{
    // http://blogs.msdn.com/rjacobs/archive/2009/02/10/ambiguous-uritemplates-query-parameters-and-integration-testing.aspx
    // http://www.codeproject.com/KB/aspnet/SimpleQueryString.aspx

    // http://www.csharper.net/blog/querystring_class_useful_for_querystring_manipulation__appendage__etc.aspx
    public class QueryString : NameValueCollection
    {
        public QueryString()
        {
        }

        public QueryString(string queryString)
        {
            DecodeQueryString(queryString);
        }

        public QueryString(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                DecodeQueryString(reader.ReadToEnd());
            } //using
        }

        public QueryString(NameValueCollection clone)
            : base(clone)
        {
        }

        private void DecodeQueryString(string url)
        {
            if (url.Trim().Length == 0)
            {
                return;
            }

            string[] keys = url.Split("&".ToCharArray());

            foreach (string key in keys)
            {
                string[] part = key.Split("=".ToCharArray());

                if (part.Length == 1)
                {
                    Add(part[0], "");
                }

                Add(part[0], HttpUtility.UrlDecode(part[1]));
            }
        }

        public void ClearAllExcept(string except)
        {
            ClearAllExcept(new string[] { except });
        }

        public void ClearAllExcept(string[] except)
        {
            ArrayList toRemove = new ArrayList();
            foreach (string s in this.AllKeys)
            {
                foreach (string e in except)
                {
                    if (s.ToLower() == e.ToLower())
                    {
                        if (!toRemove.Contains(s))
                        {
                            toRemove.Add(s);
                        }
                    }
                } //foreach
            } //foreach

            foreach (string s in toRemove)
            {
                this.Remove(s);
            } //foreach
        }

        public override void Add(string name, string value)
        {
            if (this[name] != null)
            {
                this[name] += ',' + value;
            }
            else
            {
                base.Add(name, value);
            }
        }

        public override string ToString()
        {
            string[] parts = new string[this.Count];
            string[] keys = this.AllKeys;

            for (int i = 0; i < keys.Length; i++)
            {
                parts[i] = keys[i] + "=" + HttpUtility.UrlEncode(this[keys[i]]);
            } //for i

            string queryString = String.Join("&", parts);

            if (!string.IsNullOrEmpty(queryString) && !queryString.StartsWith("?"))
            {
                queryString = "?" + queryString;
            }

            return queryString;
        }
    }
}
