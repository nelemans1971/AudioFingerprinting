using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace GZipEncoder
{
    public static class GZipEncoderHelper
    {
        // Settings to turn GZIP encoding on of off globally
        public const bool GZIPEncodingActive = true;
        public const int MinimumSizeForGZIPEncoding = 1024;
        public const float MaxCPUUtilization = 70.0F; // stop using GZIP when 70% CPU utilization has ben reached
    }


    class ContentEncodingBehavior : IEndpointBehavior, IDispatchMessageInspector
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(this);
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            // Geef http request headers door naar "BeforeSendReply" dit is nodig omdat 
            // System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest een System.ObjectDisposedException exception
            // geeft in "BeforeSendReply"

            // Making sure we have a HttpRequestMessageProperty    
            HttpRequestMessageProperty httpRequestMessageProperty = null;
            if (request.Properties.ContainsKey(HttpRequestMessageProperty.Name))    
            {     
                httpRequestMessageProperty = request.Properties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;
                return httpRequestMessageProperty.Headers;
            }

            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            bool acceptGZIP = false;
            if (correlationState != null)
            {
                System.Net.WebHeaderCollection headers = correlationState as System.Net.WebHeaderCollection;
                string acceptedEncoding = headers["Accept-Encoding"];
                acceptGZIP = (!string.IsNullOrEmpty(acceptedEncoding) && Array.IndexOf(acceptedEncoding.Split(','), "gzip") >= 0);
            }

            if (GZipEncoderHelper.GZIPEncodingActive && acceptGZIP && ProcessorUsage.GetCurrentValue < 80.0F && reply.ToString().Length > GZipEncoderHelper.MinimumSizeForGZIPEncoding)
            {
                HttpResponseMessageProperty prop;
                if (reply.Properties.ContainsKey(HttpResponseMessageProperty.Name))
                {
                    prop = (HttpResponseMessageProperty)reply.Properties[HttpResponseMessageProperty.Name];
                }
                else
                {
                    prop = new HttpResponseMessageProperty();
                    reply.Properties.Add(HttpResponseMessageProperty.Name, prop);
                }

                // YN: Sinds 2014-04-10 (waarschijnlijk eerder maar toen hadden we geen nieuwe build)
                //     Is het verplicht deze code altijd op te nemen! Voorheen ALLEEN als httpresponse properties niet bestond
                System.ServiceModel.Web.WebOperationContext.Current.OutgoingResponse.Headers["Content-Encoding"] = "gzip";

                // When adding this header the body content will be encoded with gzip in the GZipMessageEncoderFactory class
                prop.Headers[System.Net.HttpResponseHeader.ContentEncoding] = "gzip";
            }
        }
    }


    public static class ProcessorUsage
    {
        private const float sampleFrequencyMillis = 1000;

        private static object syncLock = new object();
        private static PerformanceCounter counter = null;
        private static float lastSample = 0.0F;
        private static DateTime lastSampleTime = DateTime.MinValue;

        /// <summary>
        /// 
        /// </summary>
        static ProcessorUsage()
        {
            try
            {
                counter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static public float GetCurrentValue
        {
            get
            {
                if (counter != null)
                {
                    if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > sampleFrequencyMillis)
                    {
                        lock (syncLock)
                        {
                            if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > sampleFrequencyMillis)
                            {
                                lastSample = counter.NextValue();
                                lastSampleTime = DateTime.UtcNow;
                            }
                        }
                    }
                }

                return lastSample;
            }
        }
    }
}
