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
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace BitFactory.Logging
{
	/// <summary>
	/// Instances of SerialLogger write out serialized LogEntries to it's OutputStream
	/// </summary>
	public class SerialLogger : Logger
	{
		/// <summary>
		/// A formatter for writing to the stream.
		/// </summary>
		private IFormatter _serialFormatter = new BinaryFormatter();
		/// <summary>
		/// Gets and sets the formatter.
		/// </summary>
		private IFormatter SerialFormatter 
		{
			get { return _serialFormatter; }
			set { _serialFormatter = value; }
		}
		/// <summary>
		/// The stream to which the SerialLogger logs.
		/// </summary>
		private Stream _out;
		/// <summary>
		/// Gets and sets the stream.
		/// </summary>
		public Stream Out 
		{
			get { return _out; }
			set { _out = value; }
		}
		/// <summary>
		/// Create a new instance of SerialLogger.
		/// </summary>
		protected SerialLogger() : base()
		{
		}
		/// <summary>
		/// Create a new instance of SerialLogger.
		/// </summary>
		/// <param name="anOutStream">The Stream into which logs should be written.</param>
		public SerialLogger(Stream anOutStream) : this() 
		{
			Out = anOutStream;
		}
		/// <summary>
		/// Destructor.
		/// Close the stream.
		/// </summary>
		~SerialLogger() 
		{
			try 
			{
				Out.Close();
			}
			catch
			{
			}
		}

		/// <summary>
		/// Write the LogEntry to the stream.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry to log.</param>
		/// <returns>true upon success, false upon failure.</returns>
		protected internal override bool DoLog(LogEntry aLogEntry) 
		{
			try 
			{
				TryToLog(aLogEntry);
				return true;
			} 
			catch (Exception ex) 
			{
				return HandleLogFailure(aLogEntry, ex);
			}
		}
		/// <summary>
		/// Take action upon failure to log to the stream.
		/// </summary>
		/// <param name="aLogEntry">The LogEntry that failed to be logged.</param>
		/// <param name="ex">The exception thrown during the logging attempt.</param>
		/// <returns>Always return false.</returns>
		protected virtual bool HandleLogFailure(LogEntry aLogEntry, Exception ex) 
		{
			return false;
		}
		/// <summary>
		/// Attampt to write the LogEntry to the stream.
		/// </summary>
		/// <param name="aLogEntry">The Log Entry being logged.</param>
		protected void TryToLog(LogEntry aLogEntry)
		{
			SerialFormatter.Serialize(Out,aLogEntry);
		}

	}
}
