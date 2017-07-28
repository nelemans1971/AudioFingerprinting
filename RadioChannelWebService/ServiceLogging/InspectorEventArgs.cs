using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDR.CaptureBehavior
{
    public class InspectorEventArgs : EventArgs
    {
        public InspectorEventArgs()
        {
            this.Message = string.Empty;
        }

        public InspectorEventArgs(string message)
        {
            this.Message = message;
        }

        public string Message { get; set; }

        public DateTime TimeStamp { get; set; }
        public string ClientIP { get; set; }
        public string Username { get; set; }
        public string Servername { get; set; }
        public string LocalIP { get; set; }
        public string Method { get; set; } // POST of GET
        public string Request { get; set; }
        public int Status { get; set; }
        public int ResponseInBytes { get; set; } // excluding headers, when 0 then '-'
        public string Referer { get; set; }
        public string Cookie { get; set; }
        public string UserAgent { get; set; }
        public string FunctionName { get; set; } // if filled method name which is called
    }
}

