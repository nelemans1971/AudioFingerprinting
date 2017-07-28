using System;
using System.Text;

namespace BitFactory.Logging
{
    /// <summary>
    /// This formatter is the CDR default one that formats all LogEntry information in a reasonable way.
    /// </summary>
    public class LogEntryCDRFormatter : LogEntryFormatter
    {
        /// <summary>
        /// Create a reasonably formatted String that contains all the LogEntry information.
        /// </summary>
        /// <param name="aLogEntry">The LogEntry to format.</param>
        /// <returns>A nicely formatted String.</returns>
        protected internal override String AsString(LogEntry aLogEntry)
        {
            StringBuilder logMsg = new StringBuilder(1024);

            // Applicatie als die ingesteld is
            if (aLogEntry.Application != null)
            {
                logMsg.Append("[" + aLogEntry.Application + "] -- ");
            }

            // Tijd 
            logMsg.Append(aLogEntry.Date.ToString("yyyy-MM-dd HH:mm:ss.fff "));

            // Catgorie als die aanwezig is
            if (aLogEntry.Category != null)
            {
                try
                {
                    logMsg.Append("{" + aLogEntry.Category + "} -- ");
                }
                catch{}                
            }

            // Severity
            logMsg.Append("<" + aLogEntry.SeverityString + "> -- ");

            // en als laatste het bericht zelf
            logMsg.Append(aLogEntry.Message);

            // En geef het terug
            return logMsg.ToString();
        }

        /// <summary>
        /// Create a new instance of LogEntryStandardFormatter.
        /// </summary>
        public LogEntryCDRFormatter()
            : base()
        {
        }
    }
}


