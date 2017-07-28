/* 
 * (C) Copyright 2005 - Lorne Brinkman - All Rights Reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace BitFactory.Logging
{
	/// <summary>
	/// RollingFileLogger can be used to automatically roll-over files by any determination.
    /// It does not delete old log files, it just creates new log files as required.
    /// </summary>
    /// <remarks>
    /// A log file name can change based on the current date, such as MyLogFile_20050615.log, MyLogFile_20050616.log.
	/// Or at a predetermined size, the log file name can automatically increment, such as MyLogFile_1.log, MyLogFile_2.log.
	/// There are a couple included inner 'Strategy' classes defined for rolling-over files
	/// based on the date or the size of the log. Others can easily be added.
    /// </remarks>
    public class RollingFileLogger : FileLogger
    {
        #region Attributes

        private readonly RollOverStrategy FileRollOverStrategy;

        #endregion

        #region logging

		/// <summary>
		/// Do Log aLogEntry
		/// </summary>
		/// <param name="aLogEntry">A LogEntry</param>
		/// <returns>true if successfully logged, otherwise false</returns>
        protected internal override bool DoLog(LogEntry aLogEntry)
        {
            string formattedLogString = Formatter.AsString( aLogEntry );
            FileName = FileRollOverStrategy.GetFileName(aLogEntry, formattedLogString);
            return WriteToLog(formattedLogString);
        }

        #endregion

        #region Initialization

		/// <summary>
		/// Instantiate a new RollingFileLogger with the given RollOverStrategy
		/// </summary>
		/// <param name="aRollOverStrategy">A RollOverStrategy the logger will use to determine when it should roll-over</param>
        public RollingFileLogger(RollOverStrategy aRollOverStrategy) : base()
        {
            FileRollOverStrategy = aRollOverStrategy;
        }

		/// <summary>
		/// Create and return a new RollingFileLogger that rolls over according to the current date
		/// </summary>
		/// <param name="aFullPathFormatString">The Full path of the log file with a parameter ({0}) for the variable date, for example: "\mylogs\logfile{0}.log"</param>
		/// <returns>A new RollingFileLogger</returns>
        public static RollingFileLogger NewRollingDateFileLogger(string aFullPathFormatString)
        {
            return new RollingFileLogger(new RollOverDateStrategy(aFullPathFormatString));
        }

		/// <summary>
		/// Create and return a new RollingFileLogger that rolls over according to the size of the log file
		/// </summary>
		/// <param name="aFullPathFormatString">The Full path of the log file with a parameter ({0}) for a variable number (1, 2, 3, etc.), for example: "\mylogs\logfile{0}.log"</param>
		/// <param name="aMaxSize">The maximum size the file should be before it rolls over to the next file</param>
		/// <returns>A new RollingFileLogger</returns>
        public static RollingFileLogger NewRollingSizeFileLogger(string aFullPathFormatString, long aMaxSize)
        {
            return new RollingFileLogger(new RollOverSizeStrategy(aFullPathFormatString, aMaxSize));
        }

        #endregion

        #region Roll-over strategy classes

		/// <summary>
		/// RollOverStrategy is an abstract class that defines the basic functionality required by a RollingFileLogger to roll-over.
		/// </summary>
        public abstract class RollOverStrategy
        {
            #region Attributes

			/// <summary>
			/// The format string used to generate the log file name
			/// </summary>
            protected readonly string FileNameFormatString;

            #endregion

            #region Initialization

			/// <summary>
			/// Instantiate a RollOverStrategy providing a string looking like "c:\SomeDirectoryPath\SomeFileName{0}.log"
			/// </summary>
			/// <remarks>
			/// A FormatException will be thrown if the provided string does not include the format item "{0}"
			/// </remarks>
			/// <param name="aFullPathFormatString">A string describing the full path of the log file with a format item (e.g. "c:\SomeDirectoryPath\SomeFileName{0}.log")</param>
            public RollOverStrategy(string aFullPathFormatString)
            {
				if (aFullPathFormatString.IndexOf("{0}") == -1)
					throw new FormatException("A RollOverStrategy FileNameFormatString must contain a format item \"{0}\"");

                FileNameFormatString = aFullPathFormatString;
            }

            #endregion

            internal virtual string GetFileName(LogEntry aLogEntry, string aFormattedLogString)
            {
                return string.Format(FileNameFormatString, GetIncrementalName(aLogEntry, aFormattedLogString));
            }

            internal abstract string GetIncrementalName(LogEntry aLogEntry, string aFormattedLogString);
        }

		/// <summary>
		/// RollOverDateStrategy provides date-based roll-over functionality
		/// </summary>
        public class RollOverDateStrategy : RollOverStrategy
        {
            private readonly string DateFormatString = "yyyyMMdd";

            internal override string GetIncrementalName(LogEntry aLogEntry, string aFormattedLogString)
            {
                return aLogEntry.Date.ToString(DateFormatString);
            }

            #region Initialization

			/// <summary>
			/// Create a new RollOverDateStrategy
			/// </summary>
			/// <remarks>
			/// A FormatException will be thrown if the provided string does not include the format item "{0}"
			/// </remarks>
			/// <param name="aFullPathFormatString">The Full path of the log file with a format item ({0}) for the variable date, for example: "\mylogs\logfile{0}.log"</param>
            public RollOverDateStrategy(string aFullPathFormatString) : base(aFullPathFormatString)
            {
            }

			/// <summary>
			/// Create a new RollOverDateStrategy
			/// </summary>
			/// <remarks>
			/// A FormatException will be thrown if the provided string does not include the format item "{0}"
			/// </remarks>
			/// <param name="aFullPathFormatString">The Full path of the log file with a format item ({0}) for the variable date, for example: "\mylogs\logfile{0}.log"</param>
			/// <param name="aDateFormatString">A format string that will be used to format the date portion of the log file name (e.g. "yyyyMMdd")</param>
            public RollOverDateStrategy(string aFullPathFormatString, string aDateFormatString) : this(aFullPathFormatString)
            {
                DateFormatString = aDateFormatString;
            }

            #endregion
        }

		/// <summary>
		/// RollOverSizeStrategy provides log file size-based roll-over functionality
		/// </summary>
        public class RollOverSizeStrategy : RollOverStrategy
        {
            private readonly long MaxSize;
            private int fileNumber = 0;

            #region properties

            private int FileNumber
            {
                get
                {
                    return fileNumber != 0
                        ? fileNumber
                        : fileNumber = GetFileNumber();
                }
                set { fileNumber = value; }
            }

            #endregion

            #region Initialization

			/// <summary>
			/// Create a new RollOverSizeStrategy
			/// </summary>
			/// <remarks>
			/// A FormatException will be thrown if the provided string does not include the format item "{0}"
			/// </remarks>
			/// <param name="aFullPathFormatString">The Full path of the log file with a format item ({0}) for a variable number (1, 2, 3, etc.), for example: "\mylogs\logfile{0}.log"</param>
			/// <param name="aMaxSize">The maximum size the file should be before it rolls over to the next file</param>
            public RollOverSizeStrategy(string aFullPathFormatString, long aMaxSize) : base(aFullPathFormatString)
            {
                MaxSize = aMaxSize;
            }

            #endregion

			#region Utility

			/// <summary>
			/// Determine the file number to use--essentially find the highest existing file number, otherwise use 1
			/// </summary>
			/// <returns>An int representing the file number to use</returns>
            protected virtual int GetFileNumber() 
			{
				string fileFormat = Path.GetFileName(FileNameFormatString);
				string directory = Path.GetDirectoryName(FileNameFormatString);
				DirectoryInfo di = new DirectoryInfo(directory);
				
				int number = 1;

				if (di.Exists) 
				{
					FileInfo[] fileInfos = di.GetFiles(string.Format(fileFormat, "*"));

					string regPattern = "^" + fileFormat.Replace("{0}", @"([\d]+)") + "$";

					Regex regex = new Regex(regPattern, RegexOptions.IgnoreCase);

					foreach (FileInfo fileInfo in fileInfos)
					{
						Match m = regex.Match(fileInfo.Name);
						if (m.Success)
                            number = Math.Max(number, int.Parse(m.Groups[1].Value));
					}
				}

                return number;
			}

			#endregion

            internal override string GetIncrementalName(LogEntry aLogEntry, string aFormattedLogString)
            {
                FileInfo fileInfo = new FileInfo(string.Format(FileNameFormatString, FileNumber));
                if ((!fileInfo.Exists) && (FileNumber != 1))
                {
                    FileNumber = GetFileNumber(); // the log files may have been deleted, so reset file number
                    fileInfo = new FileInfo(string.Format(FileNameFormatString, FileNumber));
                }

                // if the file is too big, increment the file number
                if ((fileInfo.Exists) && (fileInfo.Length > MaxSize - aFormattedLogString.Length))
                    FileNumber++;

                return FileNumber.ToString();
            }
        }

		#endregion
    }

}
