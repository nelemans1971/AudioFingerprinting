using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using CDR.IniFiles2;

namespace CDR.IniFiles2.Light
{
    /// <summary>Provides a simpliest parser/writer of INI files. This parser will not preserve formatting, blank lines, and multi-line commentaries.</summary>
    public class IniFileLight
    {
        /// <summary>Character used as an equality sign.</summary>
        public static char EQUALITY_SIGN = '=';
        /// <summary>Character used as an opening bracket of sections' definitions.</summary>
        public static char SECTION_OPEN_BRACKET = '[';
        /// <summary>Character used as an closing bracket of sections' definitions.</summary>
        public static char SECTION_CLOSE_BRACKET = ']';
        /// <summary>Character used as a commentary start.</summary>
        public static char COMMENT_START_1 = ';';
        /// <summary>Character used as an alternative commentary start.</summary>
        public static char COMMENT_START_2 = '#';

        Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, string> comments = new Dictionary<string, string>();

        /// <summary>Creates a new instance of IniFileLight, loading an INI file from specified path.</summary>
        /// <param name="path">Path to an existing INI file.</param>
        public IniFileLight(string path)
        {
            StreamReader reader = File.OpenText(path);
            load(reader);
            reader.Close();
        }
        /// <summary>Creates a new instance of IniFileLight, loading an INI file from specified stream.</summary>
        /// <param name="stream">A stream of INI content.</param>
        public IniFileLight(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            load(reader);
        }
        /// <summary>Creates a new, empty instance of IniFileLight.</summary>
        public IniFileLight()
        {
        }
        void load(StreamReader reader)
        {
            string str, key, value, currSect = null, currCom = null, comPath;
            int index;
            while (reader.Peek() != -1)
            {
                str = reader.ReadLine().Trim();
                if (str.Length > 0)
                {
                    if (str[0] == COMMENT_START_1 || str[0] == COMMENT_START_2)
                    {
                        if (str.Length > 1)
                            currCom = str.Substring(1);
                    }
                    else if (currSect != null && (index = str.IndexOf(EQUALITY_SIGN)) > 0 && index < str.Length - 1)
                    {
                        key = str.Substring(0, index).TrimEnd();
                        value = str.Substring(index + 1, str.Length - index - 1).TrimStart();
                        if (currCom != null)
                        {
                            comPath = currSect + "." + key;
                            if (comments.ContainsKey(comPath))
                                comments[comPath] = currCom;
                            else
                                comments.Add(comPath, currCom);
                            currCom = null;
                        }
                        if (sections[currSect].ContainsKey(key))
                            sections[currSect][key] = value;
                        else
                            sections[currSect].Add(key, value);
                    }
                    else if (str.Length > 2 && str[0] == SECTION_OPEN_BRACKET && str[str.Length - 1] == SECTION_CLOSE_BRACKET)
                    {
                        currSect = str.Substring(1, str.Length - 2).Trim();
                        if (currCom != null)
                        {
                            comPath = currSect;
                            if (comments.ContainsKey(comPath))
                                comments[comPath] = currCom;
                            else
                                comments.Add(comPath, currCom);
                            currCom = null;
                        }
                        if (!sections.ContainsKey(currSect))
                            sections.Add(currSect, new Dictionary<string, string>());
                    }
                }
            }
            if (currCom != null && !comments.ContainsKey(""))
                comments.Add("", currCom);
        }
        void save(StreamWriter writer)
        {
            lock (sections)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> sect in sections)
                {
                    if (comments.ContainsKey(sect.Key))
                        writer.WriteLine(COMMENT_START_1 + comments[sect.Key]);
                    writer.WriteLine("[" + sect.Key + "]");
                    foreach (KeyValuePair<string, string> value in sect.Value)
                    {
                        if (comments.ContainsKey(sect.Key + "." + value.Key))
                            writer.WriteLine(COMMENT_START_1 + comments[sect.Key + "." + value.Key]);
                        writer.WriteLine(value.Key + "=" + value.Value);
                    }
                }
                if (comments.ContainsKey(""))
                    writer.WriteLine(COMMENT_START_1 + comments[""]);
            }
        }
        /// <summary>Gets a dictionary of sections, in which keys are names of sections and values are
        /// dictionaries in which keys are names of values and values are actual values.</summary>
        public Dictionary<string, Dictionary<string, string>> Sections
        {
            get
            {
                return sections;
            }
        }
        /// <summary>Gets a dictionary of comments, in which keys are paths to the entries in the format:
        /// "SectionName.Key", "SectionName" or "" for the footer.</summary>
        public Dictionary<string, string> Comments
        {
            get
            {
                return comments;
            }
        }
        /// <summary>Saves the current state of sections and values to a file.</summary>
        /// <param name="path">Path of a file where to save an INI.</param>
        public void Save(string path)
        {
            StreamWriter writer = File.CreateText(path);
            save(writer);
            writer.Close();
        }
        /// <summary>Saves the current state of sections and values to a file.</summary>
        /// <param name="stream">Stream where to send INI's content.</param>
        public void Save(Stream stream)
        {
            StreamWriter writer = new StreamWriter(stream);
            save(writer);
        }
    }
}
