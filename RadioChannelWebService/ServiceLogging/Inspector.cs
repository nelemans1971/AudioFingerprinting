using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Dispatcher;

// zi eook http://cgeers.com/2011/05/10/wcf-message-logging/
namespace CDR.CaptureBehavior
{
    public class Inspector : IDispatchMessageInspector 
    {

        /// <summary>
        /// Triggered from AfterReceiveRequest method.
        /// </summary>
        public event EventHandler<InspectorEventArgs> RaiseRequestReceived;

        /// <summary>
        /// Triggered from BeforeSendReply method.
        /// </summary>
        public event EventHandler<InspectorEventArgs> RaiseSendingReply;

        /// <summary>
        /// Stores contents of Response messge
        /// which is sent back to the client.
        /// </summary>
        /// <value>The response XML.</value>
        public string Response { get; set; }


        #region IDispatchMessageInspector Members

        /// <summary>
        /// Called after an inbound message has been received but 
        /// before the message is dispatched to the intended operation.
        /// 
        /// This method will also raise RaiseRequestReceived event.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="channel">The incoming channel.</param>
        /// <param name="instanceContext">The current service instance.</param>
        /// <returns>
        /// The object used to correlate state. 
        /// </returns>
        public object AfterReceiveRequest(
            ref System.ServiceModel.Channels.Message request, 
            System.ServiceModel.IClientChannel channel, 
            System.ServiceModel.InstanceContext instanceContext)
        {
            // Request wordt bij sommige calls (bij afspelen van fragment de calls die info ophalen) 
            // gedisposed tussen 'AfterReceiveRequest' en 'BeforeSendReply'
            // daarom aangepast en hier al de gegevens gevuld en dat als parameter teruggegeven
            InspectorEventArgs message = new InspectorEventArgs();
            try
            {
                message.TimeStamp = DateTime.Now;
                message.Message = request.Headers.To.PathAndQuery;
                message.Request = request.Headers.To.PathAndQuery;
                message.Username = Username;
                message.ClientIP = CDR.WebService.ServiceHelper.CurrentClientIP;
                message.Servername = System.ServiceModel.OperationContext.Current.Channel.LocalAddress.Uri.Host;
                message.LocalIP = "127.0.0.1"; // weet niet hoe ik hieraan moet komen en ip opvragen via OS heeft dan ook geen zin
                message.Method = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Method;
                message.Referer = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Referer"] ?? "";
                message.Cookie = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["Cookie"] ?? "";
                message.UserAgent = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.UserAgent;

                // Zie http://archive.cnblogs.com/a/1669283/
                message.FunctionName = string.Empty;
                string key = WebHttpDispatchOperationSelector.HttpOperationSelectorUriMatchedPropertyName;
                var props = System.ServiceModel.OperationContext.Current.IncomingMessageProperties;
                UriTemplateMatch match = null;
                if (props.ContainsKey(key) && (bool)props[key])
                {
                    match = System.ServiceModel.OperationContext.Current.IncomingMessageProperties["UriTemplateMatchResults"] as UriTemplateMatch;
                    // Waarschijnlijk is na .net 2.0 dit veld in een string veranderd en makkelijk uit te lezen?!?
                    if (match.Data is string)
                    {
                        message.FunctionName = (string)match.Data;
                    }
                    else
                    {
                        message.FunctionName = getOperationName(match.Data);
                    }
                }
            }
            catch (Exception e)
            {
                message.Message = e.Message;
            }

            return message;
        }

        /// <summary>
        /// Called after the operation has returned but before the reply message is sent.
        /// 
        /// This method will also raise RaiseSendReply event.
        /// </summary>
        /// <param name="reply">The reply message. 
        /// This value is null if the operation is one way.</param>
        /// <param name="correlationState">The correlation object returned from the
        /// AfterReceiveRequest method.</param>
        public void BeforeSendReply(ref System.ServiceModel.Channels.Message reply,
            object correlationState)
        {
            InspectorEventArgs message = (InspectorEventArgs)correlationState;
            try
            {              
                // Engiste veld dat nu bas beschikbara is
                // De rest is in 'AfterReceiveRequest' al gevuld. Dit is nodig omdat
                // sommige properties bij sommige requests niet langer geldig zijn 
                // als je in dit event zit, waarom vaak wel en soms niet geen flauw idee
                // maar ik vul het message nu in 'AfterReceiveRequest' waar ze gegarandeerd 
                // wel beschikbaar zijn.
                message.ResponseInBytes = reply.ToString().Length;
            }
            catch (Exception e)
            {
                message.Message = e.Message;
            }

            OnRaiseSendingReply(message);
        }
        #endregion

        protected void OnRaiseRequestReceived(InspectorEventArgs message)
        {
            EventHandler<InspectorEventArgs> handler = RaiseRequestReceived;

            if (handler != null)
            {
                handler(this, message);
            }
        }

        protected void OnRaiseSendingReply(InspectorEventArgs message)
        {
            EventHandler<InspectorEventArgs> handler = RaiseSendingReply;

            if (handler != null)
            {
                handler(this, message);
            }
        }

        private static string DecodeBase64(string str)
        {
            byte[] decbuff = Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(decbuff);
        }

        /// <summary>
        /// Username used to use this webservice. Not necessary valid!
        /// </summary>
        private string Username
        {
            get
            {
                string username = string.Empty;
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
                            // password = userPassword[1];
                        }
                    }
                }
                return username;
            }
        }

        private static object operationNameLock = new Object();
        private static System.Reflection.PropertyInfo operationNamePropertyInfo;
        private static string getOperationName(object data)
        {
            if (operationNamePropertyInfo == null)
            {
                lock (operationNameLock)
                {
                    operationNamePropertyInfo = data.GetType().GetProperty("OperationName");
                }
            }
            try
            {
                return operationNamePropertyInfo == null ? null : operationNamePropertyInfo.GetValue(data, null) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
