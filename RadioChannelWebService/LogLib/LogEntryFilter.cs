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

namespace BitFactory.Logging
{
	/// <summary>
	/// Concrete subclasses of this abstract class are used by loggers
	/// to "filter" each item and decide whether or not to actually log it.
	/// </summary>
	/// <remarks>
	/// Subclasses should override the canPass(LogEntry) method.
	/// </remarks>
	public abstract class LogEntryFilter
	{
		/// <summary>
		/// The lowest severity of a LogEntry that will pass this filter.
		/// Debug is the default.
		/// </summary>
		private LogSeverity _severityThreshold = LogSeverity.Debug;
		/// <summary>
		/// Gets and sets the severity threshold.
		/// </summary>
		public LogSeverity SeverityThreshold 
		{
			get { return _severityThreshold; }
			set { _severityThreshold = value; }
		}

		/// <summary>
		/// Concrete subclasses override this method and determine if aLogEntry
		/// should pass through the filter or not.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry being tested.</param>
		/// <returns>true if aLogEntry passes the filter, false otherwise.</returns>
		protected abstract bool CanPass( LogEntry aLogEntry );

		/// <summary>
		/// Test to determine if aLogEntry should be logged.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry being tested.</param>
		/// <returns>true if aLogEntry should be logged, false otherwise.</returns>
		protected internal bool ShouldLog(LogEntry aLogEntry) 
		{
			return ( aLogEntry.Severity >= SeverityThreshold ) && CanPass( aLogEntry );
		}
		/// <summary>
		/// LogEntryFilter constructor.
		/// </summary>
		protected LogEntryFilter() : base() 
		{
		}
	}

}
