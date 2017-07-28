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
    // NOTE: If you change the interface name "IServiceContract" here, you must also update the reference to "IServiceContract" in App.config.
    [ServiceContract]
    public interface IWebserviceContract
    {
        #region Administation stuff
        // http://localhost:8088/engine/version
        [OperationContract]
        [WebGet(UriTemplate = "engine/version")]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Administrator)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Everybody)]
        Message Version();
        #endregion


        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "fingerprint/Recognize", BodyStyle = WebMessageBodyStyle.Bare)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Administrator)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Maintenance)]
        Message FingerprintRecognize(Stream stream);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "fingerprint/Recognize/Fast", BodyStyle = WebMessageBodyStyle.Bare)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Administrator)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Maintenance)]
        Message FingerprintRecognizeFast(Stream stream);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "fingerprint/Recognize/Slow", BodyStyle = WebMessageBodyStyle.Bare)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Administrator)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Maintenance)]
        Message FingerprintRecognizeSlow(Stream stream);

    }


    /// <summary>
    /// Deze implementatie is er alleen voor haproxy om te checken of de webservice nog online is
    /// Het moet in een aparte class omdat we geen "authentication" kunnen/mogen gebruiken
    /// bij haproxy
    /// </summary>
    [ServiceContract]
    public interface IServiceContractHealthCheck
    {
        [OperationContract]
        [WebInvoke(Method = "HEAD", UriTemplate = "checkHealth.htm", BodyStyle = WebMessageBodyStyle.Bare)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Everybody)]
        Stream CheckHealthHEAD();

        // http://localhost:8080/engine/health/checkHealth.htm
        [OperationContract]
        [WebGet(UriTemplate = "checkHealth.htm", BodyStyle = WebMessageBodyStyle.Bare)]
        [RESTAccessGroupAttribute(Group = AccessGroupList.Everybody)]
        Stream CheckHealthGET();
    }
}