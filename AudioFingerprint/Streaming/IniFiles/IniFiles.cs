using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace CDR.IniFiles2
{
    /// <summary>StreamReader implementation, which read from an INI file.
    /// IniFileReader DOES NOT override any StreamReader methods. New ones are added.</summary>
    public class IniFileReader : StreamReader
    {
        /// <summary>Initializes a new instance of IniFileReader from specified stream.</summary>
        public IniFileReader(Stream str)
            : base(str)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified stream and encoding.</summary>
        public IniFileReader(Stream str, Encoding enc)
            : base(str, enc)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified path.</summary>
        public IniFileReader(string path)
            : base(path)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified path and encoding.</summary>
        public IniFileReader(string path, Encoding enc)
            : base(path, enc)
        {
        }
        IniFileElement current = null;

        /// <summary>Parses a single line.</summary>
        /// <param name="line">Text to parse.</param>
        public static IniFileElement ParseLine(string line)
        {
            if (line == null)
                return null;
            if (line.Contains("\n"))
                throw new ArgumentException("String passed to the ParseLine method cannot contain more than one line.");
            string trim = line.Trim();
            IniFileElement elem = null;
            if (IniFileBlankLine.IsLineValid(trim))
                elem = new IniFileBlankLine(1);
            else if (IniFileCommentary.IsLineValid(line))
                elem = new IniFileCommentary(line);
            else if (IniFileSectionStart.IsLineValid(trim))
                elem = new IniFileSectionStart(line);
            else if (IniFileValue.IsLineValid(trim))
                elem = new IniFileValue(line);
            return elem ?? new IniFileElement(line);
        }
        /// <summary>Parses given text.</summary>
        /// <param name="text">Text to parse.</param>
        public static List<IniFileElement> ParseText(string text)
        {
            if (text == null)
                return null;
            List<IniFileElement> ret = new List<IniFileElement>();
            IniFileElement currEl, lastEl = null;
            string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                currEl = ParseLine(lines[i]);
                if (IniFileSettings.GroupElements)
                {
                    if (lastEl != null)
                    {
                        if (currEl is IniFileBlankLine && lastEl is IniFileBlankLine)
                        {
                            ((IniFileBlankLine)lastEl).Amount++;
                            continue;
                        }
                        else if (currEl is IniFileCommentary && lastEl is IniFileCommentary)
                        {
                            ((IniFileCommentary)lastEl).Comment += Environment.NewLine + ((IniFileCommentary)currEl).Comment;
                            continue;
                        }
                    }
                    else
                        lastEl = currEl;
                }
                lastEl = currEl;
                ret.Add(currEl);
            }
            return ret;
        }
        /// <summary>Reads and parses next line from the config file.</summary>
        /// <returns>Created ConfigFileElement.</returns>
        public IniFileElement ReadElement()
        {
            current = ParseLine(base.ReadLine());
            return current;
        }
        /// <summary>Reads all files</summary>
        /// <returns>All new elements which was added.</returns>
        public List<IniFileElement> ReadElementsToEnd()
        {
            List<IniFileElement> ret = ParseText(base.ReadToEnd());
            return ret;
        }
        /// <summary>Seeks to the section of specified name. If such section is not found,
        /// the function returns NULL and leaves the stream at the end of file.</summary>
        /// <param name="sectionName">Name of section to find.</param>
        public IniFileSectionStart GotoSection(string sectionName)
        {
            IniFileSectionStart sect = null;
            string str;
            while (true)
            {
                str = ReadLine();
                if (str == null)
                {
                    current = null;
                    return null;
                }
                if (IniFileSectionStart.IsLineValid(str))
                {
                    sect = ParseLine(str) as IniFileSectionStart;
                    if (sect != null && (sect.SectionName == sectionName || (!IniFileSettings.CaseSensitive && sect.SectionName.ToLowerInvariant() == sectionName)))
                    {
                        current = sect;
                        return sect;
                    }
                }
            }
        }
        /// <summary>Returns a list of IniFileElement object in the currect section. The first element of
        /// returned collection will be a IniFileSectionStart.</summary>
        /// <exception cref="System.InvalidOperationException">A stream is not currently at the IniFileSectionStart.</exception>
        public List<IniFileElement> ReadSection()
        {
            if (current == null || !(current is IniFileSectionStart))
                throw new InvalidOperationException("The current position of the reader must be at IniFileSectionStart. Use GotoSection method");
            List<IniFileElement> ret = new List<IniFileElement>();
            IniFileElement theCurrent = current;
            ret.Add(theCurrent);
            string text = "", temp;
            while ((temp = base.ReadLine()) != null)
            {
                if (IniFileSectionStart.IsLineValid(temp.Trim()))
                {
                    current = new IniFileSectionStart(temp);
                    break;
                }
                text += temp + Environment.NewLine;
            }
            if (text.EndsWith(Environment.NewLine) && text != Environment.NewLine)
                text = text.Substring(0, text.Length - Environment.NewLine.Length);
            ret.AddRange(ParseText(text));
            return ret;
        }
        /// <summary>Gets a recently parsed IniFileElement.</summary>
        public IniFileElement Current
        {
            get
            {
                return current;
            }
        }
        /// <summary>Gets values of the current section.</summary>
        /// <exception cref="System.InvalidOperationException">A stream is not currently at the IniFileSectionStart.</exception>
        public List<IniFileValue> ReadSectionValues()
        {
            List<IniFileElement> elements = ReadSection();
            List<IniFileValue> ret = new List<IniFileValue>();
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] is IniFileValue)
                    ret.Add((IniFileValue)elements[i]);
            return ret;
        }
        /// <summary>Searches the current section for a value of specified key. If such key is not found,
        /// the function returns NULL and leaves the stream at next section.</summary>
        /// <param name="key">Key to find.</param>
        public IniFileValue GotoValue(string key)
        {
            return GotoValue(key, false);
        }
        /// <summary>Searches for a value of specified key. If such key is not found,
        /// the function returns NULL and leaves the stream at next section.</summary>
        /// <param name="key">Key to find.</param>
        /// <param name="searchWholeFile">Sets a search scope. If true, function will not stop at the next IniFileSectionStart.</param>
        public IniFileValue GotoValue(string key, bool searchWholeFile)
        {
            IniFileValue val;
            string str;
            while (true)
            {
                str = ReadLine();
                if (str == null)
                    return null;
                if (IniFileValue.IsLineValid(str.Trim()))
                {
                    val = ParseLine(str) as IniFileValue;
                    if (val != null && (val.Key == key || (!IniFileSettings.CaseSensitive && val.Key.ToLowerInvariant() == key.ToLowerInvariant())))
                        return val;
                }
                if (!searchWholeFile && IniFileSectionStart.IsLineValid(str.Trim()))
                    return null;

            }
        }
    }
    /// <summary>StreamWriter implementation which writes an INI file.
    /// IniFileWriter DOES NOT override any StreamReader methods. New ones are added.</summary>
    public class IniFileWriter : StreamWriter
    {
        /// <summary>Initializes a new instance of IniFileReader from specified stream.</summary>
        public IniFileWriter(Stream str)
            : base(str)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified path.</summary>
        public IniFileWriter(string str)
            : base(str)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified stream and encoding.</summary>
        public IniFileWriter(Stream str, Encoding enc)
            : base(str, enc)
        {
        }
        /// <summary>Initializes a new instance of IniFileReader from specified path and encoding.</summary>
        public IniFileWriter(string str, bool append)
            : base(str, append)
        {
        }
        /// <summary>Writes INI file element to the file.</summary>
        /// <param name="element">Element to write.</param>
        public void WriteElement(IniFileElement element)
        {
            if (!IniFileSettings.PreserveFormatting)
                element.FormatDefault();
            // do not write if: 
            if (!( // 1) element is a blank line AND blank lines are not allowed
                (element is IniFileBlankLine && !IniFileSettings.AllowBlankLines)
                // 2) element is an empty value AND empty values are not allowed
                || (!IniFileSettings.AllowEmptyValues && element is IniFileValue && ((IniFileValue)element).Value == "")))
                base.WriteLine(element.Line);
        }
        /// <summary>Writes collection of INI file elements to the file.</summary>
        /// <param name="elements">Elements collection to write.</param>
        public void WriteElements(IEnumerable<IniFileElement> elements)
        {
            lock (elements)
                foreach (IniFileElement el in elements)
                    WriteElement(el);
        }
        /// <summary>Writes a whole INI to a file</summary>
        /// <param name="file">Section to write.</param>
        public void WriteIniFile(IniFile file)
        {
            WriteElements(file.elements);
        }
        /// <summary>Writes a section to a file</summary>
        /// <param name="section">Section to write.</param>
        public void WriteSection(IniFileSection section)
        {
            WriteElement(section.sectionStart);
            for (int i = section.parent.elements.IndexOf(section.sectionStart) + 1; i < section.parent.elements.Count; i++)
            {
                if (section.parent.elements[i] is IniFileSectionStart)
                    break;
                WriteElement(section.parent.elements[i]);
            }
        }

    }
    /// <summary>Object model for INI file, which stores a whole structure in memory.</summary>
    public class IniFile
    {
        internal List<IniFileSection> sections = new List<IniFileSection>();
        internal List<IniFileElement> elements = new List<IniFileElement>();

        /// <summary>Creates new instance of IniFile.</summary>
        public IniFile()
        {
        }
        /// <summary>Gets a IniFileSection object from it's name</summary>
        /// <param name="sectionName">Name of section to search for. If not found, new one is created.</param>
        public IniFileSection this[string sectionName]
        {
            get
            {
                IniFileSection sect = getSection(sectionName);
                if (sect != null)
                    return sect;
                IniFileSectionStart start;
                if (sections.Count > 0)
                {
                    IniFileSectionStart prev = sections[sections.Count - 1].sectionStart;
                    start = prev.CreateNew(sectionName);
                }
                else
                    start = IniFileSectionStart.FromName(sectionName);
                elements.Add(start);
                sect = new IniFileSection(this, start);
                sections.Add(sect);
                return sect;
            }
        }

        IniFileSection getSection(string name)
        {
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < sections.Count; i++)
                if (sections[i].Name == name || (!IniFileSettings.CaseSensitive && sections[i].Name.ToLowerInvariant() == lower))
                    return sections[i];
            return null;
        }
        /// <summary>Gets an array of names of sections in this INI file.</summary>
        public string[] GetSectionNames()
        {
            string[] ret = new string[sections.Count];
            for (int i = 0; i < sections.Count; i++)
                ret[i] = sections[i].Name;
            return ret;
        }

        /// <summary>Reads a INI file from a file or creates one.</summary>
        public static IniFile FromFile(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                System.IO.File.Create(path).Close();
                return new IniFile();
            }
            IniFileReader reader = new IniFileReader(path);
            IniFile ret = FromStream(reader);
            reader.Close();
            return ret;
        }
        /// <summary>Creates a new IniFile from elements collection (Advanced member).</summary>
        /// <param name="elemes">Elements collection.</param>
        public static IniFile FromElements(IEnumerable<IniFileElement> elemes)
        {
            IniFile ret = new IniFile();
            ret.elements.AddRange(elemes);
            if (ret.elements.Count > 0)
            {
                IniFileSection section = null;
                IniFileElement el;

                if (ret.elements[ret.elements.Count - 1] is IniFileBlankLine)
                    ret.elements.RemoveAt(ret.elements.Count - 1);
                for (int i = 0; i < ret.elements.Count; i++)
                {
                    el = ret.elements[i];
                    if (el is IniFileSectionStart)
                    {
                        section = new IniFileSection(ret, (IniFileSectionStart)el);
                        ret.sections.Add(section);
                    }
                    else if (section != null)
                        section.elements.Add(el);
                    else if (ret.sections.Exists(delegate(IniFileSection a)
                    {
                        return a.Name == "";
                    }))
                        ret.sections[0].elements.Add(el);
                    else if (el is IniFileValue)
                    {
                        section = new IniFileSection(ret, IniFileSectionStart.FromName(""));
                        section.elements.Add(el);
                        ret.sections.Add(section);
                    }
                }
            }
            return ret;
        }
        /// <summary>Reads a INI file from a stream.</summary>
        public static IniFile FromStream(IniFileReader reader)
        {
            return FromElements(reader.ReadElementsToEnd());
        }
        /// <summary>Writes a INI file to a disc, using options in IniFileSettings class</summary>
        public void Save(string path)
        {
            IniFileWriter writer = new IniFileWriter(path);
            Save(writer);
            writer.Close();
        }
        /// <summary>Writes a INI file to a stream, using options in IniFileSettings class</summary>
        public void Save(IniFileWriter writer)
        {
            writer.WriteIniFile(this);
        }
        /// <summary>Deletes a section and all it's values and comments. No exception is thrown if there is no section of requested name.</summary>
        /// <param name="name">Name of section to delete.</param>
        public void DeleteSection(string name)
        {
            IniFileSection section = getSection(name);
            if (section == null)
                return;
            IniFileSectionStart sect = section.sectionStart;
            elements.Remove(sect);
            for (int i = elements.IndexOf(sect) + 1; i < elements.Count; i++)
            {
                if (elements[i] is IniFileSectionStart)
                    break;
                elements.RemoveAt(i);
            }
        }
        /// <summary>Formats whole INI file.</summary>
        /// <param name="preserveIntendation">If true, old intendation will be standarized but not removed.</param>
        public void Format(bool preserveIntendation)
        {
            string lastSectIntend = "";
            string lastValIntend = "";
            IniFileElement el;
            for (int i = 0; i < elements.Count; i++)
            {
                el = elements[i];
                if (preserveIntendation)
                {
                    if (el is IniFileSectionStart)
                        lastValIntend = lastSectIntend = el.Intendation;
                    else if (el is IniFileValue)
                        lastValIntend = el.Intendation;
                }
                el.FormatDefault();
                if (preserveIntendation)
                {
                    if (el is IniFileSectionStart)
                        el.Intendation = lastSectIntend;
                    else if (el is IniFileCommentary && i != elements.Count - 1 && !(elements[i + 1] is IniFileBlankLine))
                        el.Intendation = elements[i + 1].Intendation;
                    else
                        el.Intendation = lastValIntend;
                }
            }
        }
        /// <summary>Joins sections which are definied more than one time.</summary>
        public void UnifySections()
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            IniFileSection sect;
            IniFileElement el;
            IniFileValue val;
            int index;
            for (int i = 0; i < sections.Count; i++)
            {
                sect = sections[i];
                if (dict.ContainsKey(sect.Name))
                {
                    index = dict[sect.Name] + 1;
                    elements.Remove(sect.sectionStart);
                    sections.Remove(sect);
                    for (int j = sect.elements.Count - 1; j >= 0; j--)
                    {
                        el = sect.elements[j];
                        if (!(j == sect.elements.Count - 1 && el is IniFileCommentary))
                            elements.Remove(el);
                        if (!(el is IniFileBlankLine))
                        {
                            elements.Insert(index, el);
                            val = this[sect.Name].firstValue();
                            if (val != null)
                                el.Intendation = val.Intendation;
                            else
                                el.Intendation = this[sect.Name].sectionStart.Intendation;
                        }
                    }
                }
                else
                    dict.Add(sect.Name, elements.IndexOf(sect.sectionStart));
            }
        }
        /// <summary>Gets or sets a header commentary of an INI file. Header comment must if separate from
        /// comment of a first section except when IniFileSetting.SeparateHeader is set to false.</summary>
        public string Header
        {
            get
            {
                if (elements.Count > 0)
                    if (elements[0] is IniFileCommentary && !(!IniFileSettings.SeparateHeader
                        && elements.Count > 1 && !(elements[1] is IniFileBlankLine)))
                        return ((IniFileCommentary)elements[0]).Comment;
                return "";
            }
            set
            {
                if (elements.Count > 0 && elements[0] is IniFileCommentary && !(!IniFileSettings.SeparateHeader
                    && elements.Count > 1 && !(elements[1] is IniFileBlankLine)))
                {
                    if (value == "")
                    {
                        elements.RemoveAt(0);
                        if (IniFileSettings.SeparateHeader && elements.Count > 0 && elements[0] is IniFileBlankLine)
                            elements.RemoveAt(0);
                    }
                    else
                        ((IniFileCommentary)elements[0]).Comment = value;
                }
                else if (value != "")
                {
                    if ((elements.Count == 0 || !(elements[0] is IniFileBlankLine)) && IniFileSettings.SeparateHeader)
                        elements.Insert(0, new IniFileBlankLine(1));
                    elements.Insert(0, IniFileCommentary.FromComment(value));
                }
            }
        }
        /// <summary>Gets or sets a commentary at the end of an INI file.</summary>
        public string Foot
        {
            get
            {
                if (elements.Count > 0)
                {
                    if (elements[elements.Count - 1] is IniFileCommentary)
                        return ((IniFileCommentary)elements[elements.Count - 1]).Comment;
                }
                return "";
            }
            set
            {
                if (value == "")
                {
                    if (elements.Count > 0 && elements[elements.Count - 1] is IniFileCommentary)
                    {
                        elements.RemoveAt(elements.Count - 1);
                        if (elements.Count > 0 && elements[elements.Count - 1] is IniFileBlankLine)
                            elements.RemoveAt(elements.Count - 1);
                    }
                }
                else
                {
                    if (elements.Count > 0)
                    {
                        if (elements[elements.Count - 1] is IniFileCommentary)
                            ((IniFileCommentary)elements[elements.Count - 1]).Comment = value;
                        else
                            elements.Add(IniFileCommentary.FromComment(value));
                        if (elements.Count > 2)
                        {
                            if (!(elements[elements.Count - 2] is IniFileBlankLine) && IniFileSettings.SeparateHeader)
                                elements.Insert(elements.Count - 1, new IniFileBlankLine(1));
                            else if (value == "")
                                elements.RemoveAt(elements.Count - 2);
                        }
                    }
                    else
                        elements.Add(IniFileCommentary.FromComment(value));
                }
            }
        }
    }
    /// <summary>Object model for a section in an INI file, which stores a all values in memory.</summary>
    public class IniFileSection
    {
        internal List<IniFileElement> elements = new List<IniFileElement>();
        internal IniFileSectionStart sectionStart;
        internal IniFile parent;

        internal IniFileSection(IniFile _parent, IniFileSectionStart sect)
        {
            sectionStart = sect;
            parent = _parent;
        }
        /// <summary>Gets or sets the name of the section</summary>
        public string Name
        {
            get
            {
                return sectionStart.SectionName;
            }
            set
            {
                sectionStart.SectionName = value;
            }
        }
        /// <summary>Gets or sets comment associated with this section. In the file a comment must appear exactly
        /// above section's declaration. Returns "" if no comment is provided.</summary>
        public string Comment
        {
            get
            {
                return Name == "" ? "" : getComment(sectionStart);
            }
            set
            {
                if (Name != "")
                    setComment(sectionStart, value);
            }
        }
        void setComment(IniFileElement el, string comment)
        {
            int index = parent.elements.IndexOf(el);
            if (IniFileSettings.CommentChars.Length == 0)
                throw new NotSupportedException("Comments are currently disabled. Setup ConfigFileSettings.CommentChars property to enable them.");
            IniFileCommentary com;
            if (index > 0 && parent.elements[index - 1] is IniFileCommentary)
            {
                com = ((IniFileCommentary)parent.elements[index - 1]);
                if (comment == "")
                    parent.elements.Remove(com);
                else
                {
                    com.Comment = comment;
                    com.Intendation = el.Intendation;
                }
            }
            else if (comment != "")
            {
                com = IniFileCommentary.FromComment(comment);
                com.Intendation = el.Intendation;
                parent.elements.Insert(index, com);
            }
        }
        string getComment(IniFileElement el)
        {
            int index = parent.elements.IndexOf(el);
            if (index != 0 && parent.elements[index - 1] is IniFileCommentary)
                return ((IniFileCommentary)parent.elements[index - 1]).Comment;
            else
                return "";
        }
        IniFileValue getValue(string key)
        {
            string lower = key.ToLowerInvariant();
            IniFileValue val;
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] is IniFileValue)
                {
                    val = (IniFileValue)elements[i];
                    if (val.Key == key || (!IniFileSettings.CaseSensitive && val.Key.ToLowerInvariant() == lower))
                        return val;
                }
            return null;
        }
        /// <summary>Sets the comment for given key.</summary>
        public void SetComment(string key, string comment)
        {
            IniFileValue val = getValue(key);
            if (val == null)
                return;
            setComment(val, comment);
        }
        /// <summary>Sets the inline comment for given key.</summary>
        public void SetInlineComment(string key, string comment)
        {
            IniFileValue val = getValue(key);
            if (val == null)
                return;
            val.InlineComment = comment;
        }
        /// <summary>Gets the inline comment for given key.</summary>
        public string GetInlineComment(string key)
        {
            IniFileValue val = getValue(key);
            if (val == null)
                return null;
            return val.InlineComment;
        }
        /// <summary>Gets or sets the inline for this section.</summary>
        public string InlineComment
        {
            get
            {
                return sectionStart.InlineComment;
            }
            set
            {
                sectionStart.InlineComment = value;
            }
        }
        /// <summary>Gets the comment associated to given key. If there is no comment, empty string is returned.
        /// If the key does not exist, NULL is returned.</summary>
        public string GetComment(string key)
        {
            IniFileValue val = getValue(key);
            if (val == null)
                return null;
            return getComment(val);
        }
        /// <summary>Renames a key.</summary>
        public void RenameKey(string key, string newName)
        {
            IniFileValue v = getValue(key);
            if (key == null)
                return;
            v.Key = newName;
        }
        /// <summary>Deletes a key.</summary>
        public void DeleteKey(string key)
        {
            IniFileValue v = getValue(key);
            if (key == null)
                return;
            parent.elements.Remove(v);
            elements.Remove(v);
        }
        /// <summary>Gets or sets value of the key</summary>
        /// <param name="key">Name of key.</param>
        public string this[string key]
        {
            get
            {
                IniFileValue v = getValue(key);
                return v == null ? null : v.Value;
            }
            set
            {
                IniFileValue v;
                v = getValue(key);
                //if (!IniFileSettings.AllowEmptyValues && value == "") {
                //    if (v != null) {
                //        elements.Remove(v);
                //        parent.elements.Remove(v);
                //        return;
                //    }
                //}
                if (v != null)
                {
                    v.Value = value;
                    return;
                }
                setValue(key, value);
            }
        }
        /// <summary>Gets or sets value of a key.</summary>
        /// <param name="key">Name of the key.</param>
        /// <param name="defaultValue">A value to return if the requested key was not found.</param>
        public string this[string key, string defaultValue]
        {
            get
            {
                string val = this[key];
                if (val == "" || val == null)
                    return defaultValue;
                return val;
            }
            set
            {
                this[key] = value;
            }
        }
        private void setValue(string key, string value)
        {
            IniFileValue ret = null;
            IniFileValue prev = lastValue();

            if (IniFileSettings.PreserveFormatting)
            {
                if (prev != null && prev.Intendation.Length >= sectionStart.Intendation.Length)
                    ret = prev.CreateNew(key, value);
                else
                {
                    IniFileElement el;
                    bool valFound = false;
                    for (int i = parent.elements.IndexOf(sectionStart) - 1; i >= 0; i--)
                    {
                        el = parent.elements[i];
                        if (el is IniFileValue)
                        {
                            ret = ((IniFileValue)el).CreateNew(key, value);
                            valFound = true;
                            break;
                        }
                    }
                    if (!valFound)
                        ret = IniFileValue.FromData(key, value);
                    if (ret.Intendation.Length < sectionStart.Intendation.Length)
                        ret.Intendation = sectionStart.Intendation;
                }
            }
            else
                ret = IniFileValue.FromData(key, value);
            if (prev == null)
            {
                elements.Insert(elements.IndexOf(sectionStart) + 1, ret);
                parent.elements.Insert(parent.elements.IndexOf(sectionStart) + 1, ret);
            }
            else
            {
                elements.Insert(elements.IndexOf(prev) + 1, ret);
                parent.elements.Insert(parent.elements.IndexOf(prev) + 1, ret);
            }
        }
        internal IniFileValue lastValue()
        {
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                if (elements[i] is IniFileValue)
                    return (IniFileValue)elements[i];
            }
            return null;
        }
        internal IniFileValue firstValue()
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] is IniFileValue)
                    return (IniFileValue)elements[i];
            }
            return null;
        }
        /// <summary>Gets an array of names of values in this section.</summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<string> GetKeys()
        {
            List<string> list = new List<string>(elements.Count);
            for (int i = 0; i < elements.Count; i++)
                if (elements[i] is IniFileValue)
                    list.Add(((IniFileValue)elements[i]).Key);
            return new System.Collections.ObjectModel.ReadOnlyCollection<string>(list);
            ;
        }
        /// <summary>Gets a string representation of this IniFileSectionReader object.</summary>
        public override string ToString()
        {
            return sectionStart.ToString() + " (" + elements.Count.ToString() + " elements)";
        }
        /// <summary>Formats whole section.</summary>
        /// <param name="preserveIntendation">Determines whether intendation should be preserved.</param>
        public void Format(bool preserveIntendation)
        {
            IniFileElement el;
            string lastIntend;
            for (int i = 0; i < elements.Count; i++)
            {
                el = elements[i];
                lastIntend = el.Intendation;
                el.FormatDefault();
                if (preserveIntendation)
                    el.Intendation = lastIntend;
            }
        }
    }
    /// <summary>Static class containing format settings for INI files.</summary>
    public static class IniFileSettings
    {
        private static iniFlags flags = (iniFlags)255;
        private static string[] commentChars = { ";", "#" };
        private static char? quoteChar = null;
        private static string defaultValueFormatting = "?=$   ;";
        private static string defaultSectionFormatting = "[$]   ;";
        private static string sectionCloseBracket = "]";
        private static string equalsString = "=";
        private static string tabReplacement = "    ";
        private static string sectionOpenBracket = "[";

        private enum iniFlags
        {
            PreserveFormatting = 1,
            AllowEmptyValues = 2,
            AllowTextOnTheRight = 4,
            GroupElements = 8,
            CaseSensitive = 16,
            SeparateHeader = 32,
            AllowBlankLines = 64,
            AllowInlineComments = 128
        }
        //private static string DefaultCommentaryFormatting = ";$";

        #region Public properties

        /// <summary>Inficates whether parser should preserve formatting. Default TRUE.</summary>
        public static bool PreserveFormatting
        {
            get
            {
                return (flags & iniFlags.PreserveFormatting) == iniFlags.PreserveFormatting;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.PreserveFormatting;
                else
                    flags = flags & ~iniFlags.PreserveFormatting;
            }
        }
        /// <summary>If true empty keys will not be removed. Default TRUE.</summary>
        public static bool AllowEmptyValues
        {
            get
            {
                return (flags & iniFlags.AllowEmptyValues) == iniFlags.AllowEmptyValues;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.AllowEmptyValues;
                else
                    flags = flags & ~iniFlags.AllowEmptyValues;
            }
        }
        /// <summary>If Quotes are on, then it in such situation: |KEY = "VALUE" blabla|, 'blabla' is 
        /// a "text on the right". If this field is set to False, then such string will be ignored.</summary>
        public static bool AllowTextOnTheRight
        {
            get
            {
                return (flags & iniFlags.AllowTextOnTheRight) == iniFlags.AllowTextOnTheRight;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.AllowTextOnTheRight;
                else
                    flags = flags & ~iniFlags.AllowTextOnTheRight;
            }
        }
        /// <summary>Indicates whether comments and blank lines should be grouped
        /// (if true then multiple line comment will be parsed to the one single IniFileComment object).
        /// Otherwise, one IniFileElement will be always representing one single line in the file. Default TRUE.</summary>
        public static bool GroupElements
        {
            get
            {
                return (flags & iniFlags.GroupElements) == iniFlags.GroupElements;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.GroupElements;
                else
                    flags = flags & ~iniFlags.GroupElements;
            }
        }
        /// <summary>Determines whether all searching/testing operation are case-sensitive. Default TRUE.</summary>
        public static bool CaseSensitive
        {
            get
            {
                return (flags & iniFlags.CaseSensitive) == iniFlags.CaseSensitive;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.CaseSensitive;
                else
                    flags = flags & ~iniFlags.CaseSensitive;
            }
        }
        /// <summary>Determines whether a header comment of an INI file is separate from a comment of first section.
        /// If false, comment at the beginning of file may be considered both as header and commentary of the first section. Default TRUE.</summary>
        public static bool SeparateHeader
        {
            get
            {
                return (flags & iniFlags.SeparateHeader) == iniFlags.SeparateHeader;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.SeparateHeader;
                else
                    flags = flags & ~iniFlags.SeparateHeader;
            }
        }
        /// <summary>If true, blank lines will be written to a file. Otherwise, they will ignored.</summary>
        public static bool AllowBlankLines
        {
            get
            {
                return (flags & iniFlags.AllowBlankLines) == iniFlags.AllowBlankLines;
            }
            set
            {
                if (value)
                    flags = flags | iniFlags.AllowBlankLines;
                else
                    flags = flags & ~iniFlags.AllowBlankLines;
            }
        }
        /// <summary>If true, blank lines will be written to a file. Otherwise, they will ignored.</summary>
        public static bool AllowInlineComments
        {
            get
            {
                return (flags & iniFlags.AllowInlineComments) != 0;
            }
            set
            {
                if (value)
                    flags |= iniFlags.AllowInlineComments;
                else
                    flags &= ~iniFlags.AllowInlineComments;
            }
        }
        /// <summary>A string which represents close bracket for a section. If empty or null, sections will
        /// disabled. Default "]"</summary>
        public static string SectionCloseBracket
        {
            get
            {
                return IniFileSettings.sectionCloseBracket;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("SectionCloseBracket");
                IniFileSettings.sectionCloseBracket = value;
            }
        }
        /// <summary>Gets or sets array of strings which start a comment line.
        /// Default is {"#" (hash), ";" (semicolon)}. If empty or null, commentaries
        /// will not be allowed.</summary>
        public static string[] CommentChars
        {
            get
            {
                return IniFileSettings.commentChars;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("CommentChars", "Use empty array to disable comments instead of null");
                IniFileSettings.commentChars = value;
            }
        }
        /// <summary>Gets or sets a character which is used as quote. Default null (not using quotation marks).</summary>
        public static char? QuoteChar
        {
            get
            {
                return IniFileSettings.quoteChar;
            }
            set
            {
                IniFileSettings.quoteChar = value;
            }
        }
        /// <summary>A string which determines default formatting of section headers used in Format() method.
        /// '$' (dollar) means a section's name; '[' and ']' mean brackets; optionally, ';' is an inline comment. Default is "[$]  ;" (e.g. "[Section]  ;comment")</summary>
        public static string DefaultSectionFormatting
        {
            get
            {
                return IniFileSettings.defaultSectionFormatting;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("DefaultSectionFormatting");
                string test = value.Replace("$", "").Replace("[", "").Replace("]", "").Replace(";", "");
                if (test.TrimStart().Length > 0)
                    throw new ArgumentException("DefaultValueFormatting property cannot contain other characters than [,$,] and white spaces.");
                if (!(value.IndexOf('[') < value.IndexOf('$') && value.IndexOf('$') < value.IndexOf(']')
                    && (value.IndexOf(';') == -1 || value.IndexOf(']') < value.IndexOf(';'))))
                    throw new ArgumentException("Special charcters in the formatting strings are in the incorrect order. The valid is: [, $, ].");
                IniFileSettings.defaultSectionFormatting = value;
            }
        }
        /// <summary>A string which determines default formatting of values used in Format() method. '?' (question mark) means a key,
        /// '$' (dollar) means a value and '=' (equality sign) means EqualsString; optionally, ';' is an inline comment.
        /// If QouteChar is not null, '$' will be automatically surrounded with qouetes. Default "?=$  ;" (e.g. "Key=Value  ;comment".</summary>
        public static string DefaultValueFormatting
        {
            get
            {
                return IniFileSettings.defaultValueFormatting;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("DefaultValueFormatting");
                string test = value.Replace("?", "").Replace("$", "").Replace("=", "").Replace(";", "");
                if (test.TrimStart().Length > 0)
                    throw new ArgumentException("DefaultValueFormatting property cannot contain other characters than ?,$,= and white spaces.");
                if (!(((value.IndexOf('?') < value.IndexOf('=') && value.IndexOf('=') < value.IndexOf('$'))
                    || (value.IndexOf('=') == -1 && test.IndexOf('?') < value.IndexOf('$')))
                    && (value.IndexOf(';') == -1 || value.IndexOf('$') < value.IndexOf(';'))))
                    throw new ArgumentException("Special charcters in the formatting strings are in the incorrect order. The valid is: ?, =, $.");
                IniFileSettings.defaultValueFormatting = value;
            }
        }

        /// <summary>A string which represents open bracket for a section. If empty or null, sections will
        /// disabled. Default "[".</summary>
        public static string SectionOpenBracket
        {
            get
            {
                return IniFileSettings.sectionOpenBracket;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("SectionCloseBracket");
                IniFileSettings.sectionOpenBracket = value;
            }
        }
        /// <summary>Gets or sets string used as equality sign (which separates value from key). Default "=".</summary>
        public static string EqualsString
        {
            get
            {
                return IniFileSettings.equalsString;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("EqualsString");
                IniFileSettings.equalsString = value;
            }
        }
        /// <summary>The string which all tabs in intendentation will be replaced with. If null, tabs will not be replaced. Default "    " (four spaces).</summary>
        public static string TabReplacement
        {
            get
            {
                return IniFileSettings.tabReplacement;
            }
            set
            {
                IniFileSettings.tabReplacement = value;
            }
        }
        #endregion

        internal static string trimLeft(ref string str)
        {
            int i = 0;
            StringBuilder ret = new StringBuilder();
            while (i < str.Length && char.IsWhiteSpace(str, i))
            {
                ret.Append(str[i]);
                i++;
            }
            if (str.Length > i)
                str = str.Substring(i);
            else
                str = "";
            return ret.ToString();
        }
        internal static string trimRight(ref string str)
        {
            int i = str.Length - 1;
            StringBuilder build = new StringBuilder();
            while (i >= 0 && char.IsWhiteSpace(str, i))
            {
                build.Append(str[i]);
                i--;
            }
            StringBuilder reversed = new StringBuilder();
            for (int j = build.Length - 1; j >= 0; j--)
                reversed.Append(build[j]);
            if (str.Length - i > 0)
                str = str.Substring(0, i + 1);
            else
                str = "";
            return reversed.ToString();
        }
        internal static string startsWith(string line, string[] array)
        {
            if (array == null)
                return null;
            for (int i = 0; i < array.Length; i++)
                if (line.StartsWith(array[i]))
                    return array[i];
            return null;
        }
        internal struct indexOfAnyResult
        {
            public int index;
            public string any;
            public indexOfAnyResult(int i, string _any)
            {
                any = _any;
                index = i;
            }
        }
        internal static indexOfAnyResult indexOfAny(string text, string[] array)
        {
            for (int i = 0; i < array.Length; i++)
                if (text.Contains(array[i]))
                    return new indexOfAnyResult(text.IndexOf(array[i]), array[i]);
            return new indexOfAnyResult(-1, null);
        }
        internal static string ofAny(int index, string text, string[] array)
        {
            for (int i = 0; i < array.Length; i++)
                if (text.Length - index >= array[i].Length && text.Substring(index, array[i].Length) == array[i])
                    return array[i];
            return null;
        }
        //internal static string endsWith(string line, string[] array)
        //{
        //    if (array == null) return null;
        //    for (int i = 0; i < array.Length; i++)
        //        if (line.EndsWith(array[i]))
        //            return array[i];
        //    return null;
        //}
    }
}