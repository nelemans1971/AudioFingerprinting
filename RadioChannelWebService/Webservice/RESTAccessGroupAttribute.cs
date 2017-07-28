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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;

namespace RadioChannelWebService
{
    #region class RESTAccessGroupAttribute (Service Authentication Levels)
    // create custom attribute to be assigned to class members
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RESTAccessGroupAttribute : System.Attribute
    {
        // private member data 
        private AccessGroupList group;

        public RESTAccessGroupAttribute()
        {
            this.group = AccessGroupList.Nobody;
        }

        public RESTAccessGroupAttribute(AccessGroupList group)
        {
            this.group = group;
        }

        public AccessGroupList Group
        {
            get
            {
                return group;
            }
            set
            {
                group = value;
            }
        }
    }
    #endregion

    #region AccessGroupList
    // Let op defineer also ze caseinsentieve zijn!
    public enum AccessGroupList
    {
        Nobody = 0,         // default not used in definition, only to tell that "nobody" has access to this method (including administrator)
        Everybody,          // not neede to be definied, any valid user belongs tot this "group"
        Administrator,
        Maintenance
    }
    #endregion
}
