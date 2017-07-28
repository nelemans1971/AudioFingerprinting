/* 
 * (C) Copyright 2002, 2005 - Lorne Brinkman - All Rights Reserved.
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
using System.Collections;

namespace BitFactory.Logging
{
	/// <summary>
	/// A CompositeLogger contains other Logger instances.
	/// </summary>
	/// <remarks>
	/// When instances of this class log LogEntries,
	/// they simply pass on the LogEntries to the Loggers contained therein.
	///
	/// Loggers are contained in a HashMap, with the keys being Strings.
	/// This provides a nice means to access a specific contained Logger, if necessary.
	///
	/// Instances of this class are likely to be used as an application's main logger,
	/// within which more specific loggers can be contained.
	/// </remarks>
	public class CompositeLogger : Logger
	{
		/// <summary>
		/// The Collection of Loggers.
		/// </summary>
		private IDictionary _loggers = new Hashtable();
		
		/// <summary>
		/// Gets and sets the Collection of Loggers.
		/// </summary>
		protected IDictionary Loggers 
		{
			get { return _loggers; }
			set { _loggers = value; }
		}

		/// <summary>
		/// Add a Logger to this CompositeLogger.
		/// </summary>
		/// <param name="aName">A meaningful name for possible access later.</param>
		/// <param name="aLogger">The Logger to add.</param>
		public void AddLogger(String aName, Logger aLogger) 
		{
			Loggers.Add( aName, aLogger);
		}

		/// <summary>
		/// Remove a Logger
		/// </summary>
		/// <param name="aName">The name of the Logger to remove</param>
		public void RemoveLogger(String aName) 
		{
			Loggers.Remove( aName);
		}

		/// <summary>
		/// Send the log message to contained Loggers.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry to log.</param>
		/// <returns>Always return true--assume success</returns>
		protected internal override bool DoLog(LogEntry aLogEntry) 
		{
            foreach (Logger logger in Loggers.Values)
                logger.Log(aLogEntry);
			return true;
		}

		/// <summary>
		/// Gets the Logger with the name corresponding to the given String.
		/// </summary>
		/// <param name="aName">The name of the desired Logger</param>
		/// <returns>The Logger corresponding with aName</returns>
		public Logger this[String aName] 
		{
			get { return (Logger) Loggers[aName]; }
		}
		
		/// <summary>
		/// Create a new instance of CompositeLogger
		/// </summary>
		public CompositeLogger() : base()
		{
		}
	}
}
