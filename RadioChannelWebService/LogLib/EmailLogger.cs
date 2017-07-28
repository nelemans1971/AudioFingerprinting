/* 
 * (C) Copyright 2002 - Lorne Brinkman - All Rights Reserved.
 * http://www.TheObjectGuy.com
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 * 
 *  - Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 * 
 *  - Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 * 
 *  - Neither the name "Lorne Brinkman", "The Object Guy", nor the name "Bit Factory"
 *    may be used to endorse or promote products derived from this software without
 *    specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
 * INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
 * OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY
 * OF SUCH DAMAGE.
 * 
 */

using System;
using System.Net.Mail;

namespace BitFactory.Logging
{
	/// <summary>
	/// An EmailLogger sends log information via an email message.
	/// </summary>
	/// <remarks>
	/// If the subject attribute is not explicitly set, then it will automatically be filled
	/// with the logEntry's application, category, and severity.
	/// </remarks>
	public class EmailLogger : Logger
	{
		/// <summary>
		/// The "from" for the email.
		/// </summary>
		private String _from;
		/// <summary>
		/// The "to" for the email.
		/// </summary>
		private String _to;
		/// <summary>
		/// The "subject" of the email.
		/// </summary>
		private String _subject;
        /// <summary>
        /// The SMTP client
        /// </summary>
        private SmtpClient _smtpClient;

		/// <summary>
		/// Gets and sets the "from" for the email.
		/// </summary>
		public String From 
		{
			get { return _from; }
			set { _from = value; }
		}
		/// <summary>
		/// Gets and sets the "to" for the email.
		/// </summary>
		public String To 
		{
			get { return _to; }
			set { _to = value; }
		}
		/// <summary>
		/// Gets and sets the "subject" of the email.
		/// </summary>
		public String Subject 
		{
			get { return _subject; }
			set { _subject = value; }
		}
        /// <summary>
        /// Gets and sets the SmtpClient
        /// </summary>
        public SmtpClient SmtpClient
        {
            get { return _smtpClient; }
            set { _smtpClient = value; }
        }

		/// <summary>
		/// Create an instance of EmailLogger.
		/// </summary>
        /// <param name="anSmtpClient">The SmtpClient object the logger will use to send emails.</param>
		/// <param name="aFrom">The "from" for the emails that get sent.</param>
		/// <param name="aTo">The "to" for the emails that get sent.</param>
        public EmailLogger(SmtpClient anSmtpClient, String aFrom, String aTo) : this(anSmtpClient, aFrom, aTo, null)
		{			
		}
		/// <summary>
		/// Create an instance of EmailLogger.
		/// </summary>
        /// <param name="anSmtpClient">The SmtpClient object the logger will use to send emails.</param>
        /// <param name="aFrom">The "from" for the emails that get sent.</param>
		/// <param name="aTo">The "to" for the emails that get sent.</param>
		/// <param name="aSubject">The "subject" of the emails that get sent.</param>
		public EmailLogger(SmtpClient anSmtpClient, String aFrom, String aTo, String aSubject) : base()
		{
            SmtpClient = anSmtpClient;
			From = aFrom;
			To = aTo;
			Subject = aSubject;
		}

		/// <summary>
		/// Send the email representing aLogEntry.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry.</param>
		/// <returns>true upon success, false upon failure.</returns>
		protected internal override bool DoLog(LogEntry aLogEntry) 
		{
			try 
			{
				String aSubject = "Subject: ";
				if ( Subject == null) 
				{
					if ( aLogEntry.Application != null )
						aSubject += "[" + aLogEntry.Application + "] -- ";
					if ( aLogEntry.Category != null )
						aSubject += "{" + aLogEntry.Category + "} -- ";
					aSubject += "<" + aLogEntry.SeverityString + ">";
				} 
				else 
				{
					aSubject += Subject;
				}

				SmtpClient.Send( From, To, aSubject, Formatter.AsString( aLogEntry));

				return true;
			} 
			catch
			{
				return false;
			}
		}
	}
}
