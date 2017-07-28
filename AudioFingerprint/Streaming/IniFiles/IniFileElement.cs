using System;
using System.Collections.Generic;
using System.Text;

namespace CDR.IniFiles2
{
    /// <summary>Base class for all Config File elements.</summary>
    public class IniFileElement
    {
        private string line;
        /// <summary>Same as Formatting</summary>
        protected string formatting = "";


        /// <summary>Initializes a new, empty instance IniFileElement</summary>
        protected IniFileElement()
        {
            line = "";
        }
        /// <summary>Initializes a new instance IniFileElement</summary>
        /// <param name="_content">Actual content of a line in a INI file.</param>
        public IniFileElement(string _content)
        {
            line = _content.TrimEnd();
        }
        /// <summary>Gets or sets a formatting string of this INI file element, spicific to it's type. 
        /// See DefaultFormatting property in IniFileSettings for more info.</summary>
        public string Formatting
        {
            get
            {
                return formatting;
            }
            set
            {
                formatting = value;
            }
        }
        /// <summary>Gets or sets a string of white characters which precedes any meaningful content of a line.</summary>
        public string Intendation
        {
            get
            {
                StringBuilder intend = new StringBuilder();
                for (int i = 0; i < formatting.Length; i++)
                {
                    if (!char.IsWhiteSpace(formatting[i]))
                        break;
                    intend.Append(formatting[i]);
                }
                return intend.ToString();
            }
            set
            {
                if (value.TrimStart().Length > 0)
                    throw new ArgumentException("Intendation property cannot contain any characters which are not condsidered as white ones.");
                if (IniFileSettings.TabReplacement != null)
                    value = value.Replace("\t", IniFileSettings.TabReplacement);
                formatting = value + formatting.TrimStart();
                line = value + line.TrimStart();
            }
        }
        /// <summary>Gets full text representation of a config file element, excluding intendation.</summary>
        public string Content
        {
            get
            {
                return line.TrimStart();
            }
            protected set
            {
                line = value;
            }
        }
        /// <summary>Gets full text representation of a config file element, including intendation.</summary>
        public string Line
        {
            get
            {
                string intendation = Intendation;
                if (line.Contains(Environment.NewLine))
                {
                    string[] lines = line.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                    StringBuilder ret = new StringBuilder();
                    ret.Append(lines[0]);
                    for (int i = 1; i < lines.Length; i++)
                        ret.Append(Environment.NewLine + intendation + lines[i]);
                    return ret.ToString();
                }
                else
                    return line;
            }
        }
        /// <summary>Gets a string representation of this IniFileElement object.</summary>
        public override string ToString()
        {
            return "Line: \"" + line + "\"";
        }
        /// <summary>Formats this config element</summary>
        public virtual void FormatDefault()
        {
            Intendation = "";
        }
    }
    /// <summary>Represents section's start line, e.g. "[SectionName]".</summary>
    public class IniFileSectionStart : IniFileElement
    {
        private string sectionName;
        private string textOnTheRight; // e.g.  "[SectionName] some text"
        private string inlineComment, inlineCommentChar;

        private IniFileSectionStart()
            : base()
        {
        }
        /// <summary>Initializes a new instance IniFileSectionStart</summary>
        /// <param name="content">Actual content of a line in an INI file. Initializer assumes that it is valid.</param>
        public IniFileSectionStart(string content)
            : base(content)
        {
            //content = Content;
            formatting = ExtractFormat(content);
            content = content.TrimStart();
            if (IniFileSettings.AllowInlineComments)
            {
                IniFileSettings.indexOfAnyResult result = IniFileSettings.indexOfAny(content, IniFileSettings.CommentChars);
                if (result.index > content.IndexOf(IniFileSettings.SectionCloseBracket))
                {
                    inlineComment = content.Substring(result.index + result.any.Length);
                    inlineCommentChar = result.any;
                    content = content.Substring(0, result.index);
                }
            }
            if (IniFileSettings.AllowTextOnTheRight)
            {
                int closeBracketPos = content.LastIndexOf(IniFileSettings.SectionCloseBracket);
                if (closeBracketPos != content.Length - 1)
                {
                    textOnTheRight = content.Substring(closeBracketPos + 1);
                    content = content.Substring(0, closeBracketPos);
                }
            }
            sectionName = content.Substring(IniFileSettings.SectionOpenBracket.Length, content.Length - IniFileSettings.SectionCloseBracket.Length - IniFileSettings.SectionOpenBracket.Length).Trim();
            Content = content;
            Format();
        }
        /// <summary>Gets or sets a secion's name.</summary>
        public string SectionName
        {
            get
            {
                return sectionName;
            }
            set
            {
                sectionName = value;
                Format();
            }
        }
        /// <summary>Gets or sets an inline comment, which appear after the value.</summary>
        public string InlineComment
        {
            get
            {
                return inlineComment;
            }
            set
            {
                if (!IniFileSettings.AllowInlineComments || IniFileSettings.CommentChars.Length == 0)
                    throw new NotSupportedException("Inline comments are disabled.");
                inlineComment = value;
                Format();
            }
        }
        /// <summary>Determines whether specified string is a representation of particular IniFileElement object.</summary>
        /// <param name="testString">Trimmed test string.</param>
        public static bool IsLineValid(string testString)
        {
            return testString.StartsWith(IniFileSettings.SectionOpenBracket) && testString.EndsWith(IniFileSettings.SectionCloseBracket);
        }
        /// <summary>Gets a string representation of this IniFileSectionStart object.</summary>
        public override string ToString()
        {
            return "Section: \"" + sectionName + "\"";
        }
        /// <summary>Creates a new IniFileSectionStart object basing on a name of section and the formatting style of this section.</summary>
        /// <param name="sectName">Name of the new section</param>
        public IniFileSectionStart CreateNew(string sectName)
        {
            IniFileSectionStart ret = new IniFileSectionStart();
            ret.sectionName = sectName;
            if (IniFileSettings.PreserveFormatting)
            {
                ret.formatting = formatting;
                ret.Format();
            }
            else
                ret.Format();
            return ret;
        }
        /// <summary>Creates a formatting string basing on an actual content of a line.</summary>
        public static string ExtractFormat(string content)
        {
            bool beforeS = false;
            bool afterS = false;
            bool beforeEvery = true;
            char currC;
            string comChar;
            string insideWhiteChars = "";
            StringBuilder form = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                currC = content[i];
                if (char.IsLetterOrDigit(currC) && beforeS)
                {
                    afterS = true;
                    beforeS = false;
                    form.Append('$');
                }
                else if (afterS && char.IsLetterOrDigit(currC))
                {
                    insideWhiteChars = "";
                }
                else if (content.Length - i >= IniFileSettings.SectionOpenBracket.Length && content.Substring(i, IniFileSettings.SectionOpenBracket.Length) == IniFileSettings.SectionOpenBracket && beforeEvery)
                {
                    beforeS = true;
                    beforeEvery = false;
                    form.Append('[');
                }
                else if (content.Length - i >= IniFileSettings.SectionCloseBracket.Length && content.Substring(i, IniFileSettings.SectionOpenBracket.Length) == IniFileSettings.SectionCloseBracket && afterS)
                {
                    form.Append(insideWhiteChars);
                    afterS = false;
                    form.Append(IniFileSettings.SectionCloseBracket);
                }
                else if ((comChar = IniFileSettings.ofAny(i, content, IniFileSettings.CommentChars)) != null)
                {
                    form.Append(';');
                }
                else if (char.IsWhiteSpace(currC))
                {
                    if (afterS)
                        insideWhiteChars += currC;
                    else
                        form.Append(currC);
                }
            }
            string ret = form.ToString();
            if (ret.IndexOf(';') == -1)
                ret += "   ;";
            return ret;
        }
        /// <summary>Formats the IniFileElement object using default format specified in IniFileSettings.</summary>
        public override void FormatDefault()
        {
            Formatting = IniFileSettings.DefaultSectionFormatting;
            Format();
        }
        /// <summary>Formats this element using a formatting string in Formatting property.</summary>
        public void Format()
        {
            Format(formatting);
        }
        /// <summary>Formats this element using given formatting string</summary>
        /// <param name="formatting">Formatting template, where '['-open bracket, '$'-section name, ']'-close bracket, ';'-inline comments.</param>
        public void Format(string formatting)
        {
            char currC;
            StringBuilder build = new StringBuilder();
            for (int i = 0; i < formatting.Length; i++)
            {
                currC = formatting[i];
                if (currC == '$')
                    build.Append(sectionName);
                else if (currC == '[')
                    build.Append(IniFileSettings.SectionOpenBracket);
                else if (currC == ']')
                    build.Append(IniFileSettings.SectionCloseBracket);
                else if (currC == ';' && IniFileSettings.CommentChars.Length > 0 && inlineComment != null)
                    build.Append(IniFileSettings.CommentChars[0]).Append(inlineComment);
                else if (char.IsWhiteSpace(formatting[i]))
                    build.Append(formatting[i]);
            }
            Content = build.ToString().TrimEnd() + (IniFileSettings.AllowTextOnTheRight ? textOnTheRight : "");
        }
        /// <summary>Crates a IniFileSectionStart object from name of a section.</summary>
        /// <param name="sectionName">Name of a section</param>
        public static IniFileSectionStart FromName(string sectionName)
        {
            IniFileSectionStart ret = new IniFileSectionStart();
            ret.SectionName = sectionName;
            ret.FormatDefault();
            return ret;
        }
    }
    /// <summary>Represents one or more blank lines within a config file.</summary>
    public class IniFileBlankLine : IniFileElement
    {
        /// <summary>Initializes a new instance IniFileBlankLine</summary>
        /// <param name="amount">Number of blank lines.</param>
        public IniFileBlankLine(int amount)
            : base("")
        {
            Amount = amount;
        }
        /// <summary>Gets or sets a number of blank lines.</summary>
        public int Amount
        {
            get
            {
                return Line.Length / Environment.NewLine.Length + 1;
            }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Cannot set Amount to less than 1.");
                StringBuilder build = new StringBuilder();
                for (int i = 1; i < value; i++)
                    build.Append(Environment.NewLine);
                Content = build.ToString();
            }
        }
        /// <summary>Determines whether specified string is a representation of particular IniFileElement object.</summary>
        /// <param name="testString">Trimmed test string.</param>
        public static bool IsLineValid(string testString)
        {
            return testString == "";
        }
        /// <summary>Gets a string representation of this IniFileBlankLine object.</summary>
        public override string ToString()
        {
            return Amount.ToString() + " blank line(s)";
        }
        /// <summary>Formats the IniFileElement object using directions in IniFileSettings.</summary>
        public override void FormatDefault()
        {
            Amount = 1;
            base.FormatDefault();
        }
    }
    /// <summary>Represents one or more comment lines in a config file.</summary>
    public class IniFileCommentary : IniFileElement
    {
        private string comment;
        private string commentChar;

        private IniFileCommentary()
        {
        }
        /// <summary>Initializes a new instance IniFileCommentary</summary>
        /// <param name="content">Actual content of a line in a INI file.</param>
        public IniFileCommentary(string content)
            : base(content)
        {
            if (IniFileSettings.CommentChars.Length == 0)
                throw new NotSupportedException("Comments are disabled. Set the IniFileSettings.CommentChars property to turn them on.");
            commentChar = IniFileSettings.startsWith(Content, IniFileSettings.CommentChars);
            if (Content.Length > commentChar.Length)
                comment = Content.Substring(commentChar.Length);
            else
                comment = "";
        }
        /// <summary>Gets or sets comment char used in the config file for this comment.</summary>
        public string CommentChar
        {
            get
            {
                return commentChar;
            }
            set
            {
                if (commentChar != value)
                {
                    commentChar = value;
                    rewrite();
                }
            }
        }
        /// <summary>Gets or sets a commentary string.</summary>
        public string Comment
        {
            get
            {
                return comment;
            }
            set
            {
                if (comment != value)
                {
                    comment = value;
                    rewrite();
                }
            }
        }
        void rewrite()
        {
            StringBuilder newContent = new StringBuilder();
            string[] lines = comment.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            newContent.Append(commentChar + lines[0]);
            for (int i = 1; i < lines.Length; i++)
                newContent.Append(Environment.NewLine + commentChar + lines[i]);
            Content = newContent.ToString();
        }
        /// <summary>Determines whether specified string is a representation of particular IniFileElement object.</summary>
        /// <param name="testString">Trimmed test string.</param>
        public static bool IsLineValid(string testString)
        {
            return IniFileSettings.startsWith(testString.TrimStart(), IniFileSettings.CommentChars) != null;
        }
        /// <summary>Gets a string representation of this IniFileCommentary object.</summary>
        public override string ToString()
        {
            return "Comment: \"" + comment + "\"";
        }
        /// <summary>Gets an IniFileCommentary object from commentary text.</summary>
        /// <param name="comment">Commentary text.</param>
        public static IniFileCommentary FromComment(string comment)
        {
            if (IniFileSettings.CommentChars.Length == 0)
                throw new NotSupportedException("Comments are disabled. Set the IniFileSettings.CommentChars property to turn them on.");
            IniFileCommentary ret = new IniFileCommentary();
            ret.comment = comment;
            ret.CommentChar = IniFileSettings.CommentChars[0];
            return ret;
        }
        /// <summary>Formats IniFileCommentary object to default appearance.</summary>
        public override void FormatDefault()
        {
            base.FormatDefault();
            CommentChar = IniFileSettings.CommentChars[0];
            rewrite();
        }
    }
    /// <summary>Represents one key-value pair.</summary>
    public class IniFileValue : IniFileElement
    {
        private string key;
        private string value;
        private string textOnTheRight; // only if qoutes are on, e.g. "Name = 'Jack' text-on-the-right"
        private string inlineComment, inlineCommentChar;

        private IniFileValue()
            : base()
        {
        }
        /// <summary>Initializes a new instance IniFileValue.</summary>
        /// <param name="content">Actual content of a line in an INI file. Initializer assumes that it is valid.</param>
        public IniFileValue(string content)
            : base(content)
        {
            string[] split = Content.Split(new string[] { IniFileSettings.EqualsString }, StringSplitOptions.None);
            formatting = ExtractFormat(content);
            string split0 = split[0].Trim();
            string split1 = split.Length >= 1 ?
                split[1].Trim()
                : "";

            if (split0.Length > 0)
            {
                if (IniFileSettings.AllowInlineComments)
                {
                    IniFileSettings.indexOfAnyResult result = IniFileSettings.indexOfAny(split1, IniFileSettings.CommentChars);
                    if (result.index != -1)
                    {
                        inlineComment = split1.Substring(result.index + result.any.Length);
                        split1 = split1.Substring(0, result.index).TrimEnd();
                        inlineCommentChar = result.any;
                    }
                }
                if (IniFileSettings.QuoteChar != null && split1.Length >= 2)
                {
                    char quoteChar = (char)IniFileSettings.QuoteChar;
                    if (split1[0] == quoteChar)
                    {
                        int lastQuotePos;
                        if (IniFileSettings.AllowTextOnTheRight)
                        {
                            lastQuotePos = split1.LastIndexOf(quoteChar);
                            if (lastQuotePos != split1.Length - 1)
                                textOnTheRight = split1.Substring(lastQuotePos + 1);
                        }
                        else
                            lastQuotePos = split1.Length - 1;
                        if (lastQuotePos > 0)
                        {
                            if (split1.Length == 2)
                                split1 = "";
                            else
                                split1 = split1.Substring(1, lastQuotePos - 1);
                        }
                    }
                }
                key = split0;
                value = split1;
            }
            Format();
        }
        /// <summary>Gets or sets a name of value.</summary>
        public string Key
        {
            get
            {
                return key;
            }
            set
            {
                key = value;
                Format();
            }
        }
        /// <summary>Gets or sets a value.</summary>
        public string Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
                Format();
            }
        }
        /// <summary>Gets or sets an inline comment, which appear after the value.</summary>
        public string InlineComment
        {
            get
            {
                return inlineComment;
            }
            set
            {
                if (!IniFileSettings.AllowInlineComments || IniFileSettings.CommentChars.Length == 0)
                    throw new NotSupportedException("Inline comments are disabled.");
                if (inlineCommentChar == null)
                    inlineCommentChar = IniFileSettings.CommentChars[0];
                inlineComment = value;
                Format();
            }
        }
        enum feState // stare of format extractor (ExtractFormat method)
        {
            BeforeEvery,
            AfterKey,
            BeforeVal,
            AfterVal
        }
        /// <summary>Creates a formatting string basing on an actual content of a line.</summary>
        public string ExtractFormat(string content)
        {
            //bool afterKey = false; bool beforeVal = false; bool beforeEvery = true; bool afterVal = false;
            //return IniFileSettings.DefaultValueFormatting;
            feState pos = feState.BeforeEvery;
            char currC;
            string comChar;
            string insideWhiteChars = "";
            string theWhiteChar;
            ;
            StringBuilder form = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                currC = content[i];
                if (char.IsLetterOrDigit(currC))
                {
                    if (pos == feState.BeforeEvery)
                    {
                        form.Append('?');
                        pos = feState.AfterKey;
                        //afterKey = true; beforeEvery = false; ;
                    }
                    else if (pos == feState.BeforeVal)
                    {
                        form.Append('$');
                        pos = feState.AfterVal;
                    }
                }

                else if (pos == feState.AfterKey && content.Length - i >= IniFileSettings.EqualsString.Length && content.Substring(i, IniFileSettings.EqualsString.Length) == IniFileSettings.EqualsString)
                {
                    form.Append(insideWhiteChars);
                    pos = feState.BeforeVal;
                    //afterKey = false; beforeVal = true; 
                    form.Append('=');
                }
                else if ((comChar = IniFileSettings.ofAny(i, content, IniFileSettings.CommentChars)) != null)
                {
                    form.Append(insideWhiteChars);
                    form.Append(';');
                }
                else if (char.IsWhiteSpace(currC))
                {
                    if (currC == '\t' && IniFileSettings.TabReplacement != null)
                        theWhiteChar = IniFileSettings.TabReplacement;
                    else
                        theWhiteChar = currC.ToString();
                    if (pos == feState.AfterKey || pos == feState.AfterVal)
                    {
                        insideWhiteChars += theWhiteChar;
                        continue;
                    }
                    else
                        form.Append(theWhiteChar);
                }
                insideWhiteChars = "";
            }
            if (pos == feState.BeforeVal)
            {
                form.Append('$');
                pos = feState.AfterVal;
            }
            string ret = form.ToString();
            if (ret.IndexOf(';') == -1)
                ret += "   ;";
            return ret;
        }

        /// <summary>Formats this element using the format string in Formatting property.</summary>
        public void Format()
        {
            Format(formatting);
        }
        /// <summary>Formats this element using given formatting string</summary>
        /// <param name="formatting">Formatting template, where '?'-key, '='-equality sign, '$'-value, ';'-inline comments.</param>
        public void Format(string formatting)
        {
            char currC;
            StringBuilder build = new StringBuilder();
            for (int i = 0; i < formatting.Length; i++)
            {
                currC = formatting[i];
                if (currC == '?')
                    build.Append(key);
                else if (currC == '$')
                {
                    if (IniFileSettings.QuoteChar != null)
                    {
                        char quoteChar = (char)IniFileSettings.QuoteChar;
                        build.Append(quoteChar).Append(value).Append(quoteChar);
                    }
                    else
                        build.Append(value);
                }
                else if (currC == '=')
                    build.Append(IniFileSettings.EqualsString);
                else if (currC == ';')
                    build.Append(inlineCommentChar + inlineComment);
                else if (char.IsWhiteSpace(formatting[i]))
                    build.Append(currC);
            }
            Content = build.ToString().TrimEnd() + (IniFileSettings.AllowTextOnTheRight ? textOnTheRight : "");
        }
        /// <summary>Formats content using a scheme specified in IniFileSettings.DefaultValueFormatting.</summary>
        public override void FormatDefault()
        {
            Formatting = IniFileSettings.DefaultValueFormatting;
            Format();
        }
        /// <summary>Creates a new IniFileValue object basing on a key and a value and the formatting  of this IniFileValue.</summary>
        /// <param name="key">Name of value</param>
        /// <param name="value">Value</param>
        public IniFileValue CreateNew(string key, string value)
        {
            IniFileValue ret = new IniFileValue();
            ret.key = key;
            ret.value = value;
            if (IniFileSettings.PreserveFormatting)
            {
                ret.formatting = formatting;
                if (IniFileSettings.AllowInlineComments)
                    ret.inlineCommentChar = inlineCommentChar;
                ret.Format();
            }
            else
                ret.FormatDefault();
            return ret;
        }
        /// <summary>Determines whether specified string is a representation of particular IniFileElement object.</summary>
        /// <param name="testString">Trimmed test string.</param>
        public static bool IsLineValid(string testString)
        {
            int index = testString.IndexOf(IniFileSettings.EqualsString);
            return index > 0;
        }
        /// <summary>Sets both key and values. Recommended when both properties have to be changed.</summary>
        public void Set(string key, string value)
        {
            this.key = key;
            this.value = value;
            Format();
        }
        /// <summary>Gets a string representation of this IniFileValue object.</summary>
        public override string ToString()
        {
            return "Value: \"" + key + " = " + value + "\"";
        }
        /// <summary>Crates a IniFileValue object from it's data.</summary>
        /// <param name="key">Value name.</param>
        /// <param name="value">Associated value.</param>
        public static IniFileValue FromData(string key, string value)
        {
            IniFileValue ret = new IniFileValue();
            ret.key = key;
            ret.value = value;
            ret.FormatDefault();
            return ret;
        }
    }
}
