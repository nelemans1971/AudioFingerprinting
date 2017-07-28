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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace CDR.WebService
{
    public static class ServiceHelper
    {
        public static XElement CreateRoot(ResultErrorCode errorCode)
        {
            return CreateRoot(errorCode, errorCode.ToString());
        }

        public static XElement CreateRoot(ResultErrorCode errorCode, string description)
        {
            XElement xResult = new XElement("Result",
                new XAttribute("ErrorCode", Convert.ToInt32(errorCode)),
                new XAttribute("Description", description));

            return xResult;
        }

        public static Message SendErrorMessage(ResultErrorCode errorCode, string description)
        {
            return Message.CreateMessage(MessageVersion.None, "", new XElementBodyWriter(CreateRoot(errorCode, description)));
        }

        public static string CurrentClientIP
        {
            get
            {
                string ip = string.Empty;

                // zie http://nayyeri.net/detect-client-ip-in-wcf-3-5
                if (OperationContext.Current != null)
                {
                    RemoteEndpointMessageProperty client = OperationContext.Current.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;

                    if (client != null)
                    {
                        // Op onderstaande manier kun je het originele IP nummer van de request zien (die zou anders verdwijnen door
                        // de LoadBalancer)
                        ip = System.ServiceModel.Web.WebOperationContext.Current.IncomingRequest.Headers["X-Forwarded-For"];
                        if (ip == null || ip.Length <= 0)
                        {
                            ip = client.Address;
                        }
                    }
                }

                return ip;
            }
        }
    }


    /// <summary>
    /// Necessary to write out the contents as text (used with the Raw return type)
    /// </summary>
    public class TextBodyWriter : BodyWriter
    {
        byte[] messageBytes;

        public TextBodyWriter(string message)
            : base(true)
        {
            this.messageBytes = Encoding.UTF8.GetBytes(message);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("Binary");
            writer.WriteBase64(this.messageBytes, 0, this.messageBytes.Length);
            writer.WriteEndElement();
        }
    }

    public class XElementBodyWriter : BodyWriter
    {
        XElement xElement;

        public XElementBodyWriter(XElement xElement)
            : base(true)
        {
            this.xElement = xElement;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                xElement);

            doc.WriteTo(writer);
        }
    }

    /// <summary>
    /// Returned (error) codes. Ze zijn hardgecodeerd omdat we aan gebruiker een
    /// getal teruggeven en we willen dat niet zomaar laten wijzigen als we 
    /// is verwijderen of toevoegen
    /// </summary>
    public enum ResultErrorCode
    {
        OK = 0, // algemeen
        FAILED = 1, // algemeen
        // Specifieke fouten
        NoResultset = 2,
        Exception = 3,
        BadRequest = 4,
        AuthorizationError = 5,
        InvalidTOKEN = 6
    }
}
