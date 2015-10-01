using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Debug = System.Diagnostics.Debug;
using System.Globalization;
using System.IO;

using RapidSoft.Etl.Runtime.Properties;

namespace RapidSoft.Etl.Runtime.DataSources.Csv
{
    public sealed partial class CsvReader : IDataReader, IEnumerable<IDataRecord>, IDisposable
    {
        #region Constructors

        public CsvReader(TextReader reader)
            : this(reader, null, default(CsvReaderOptions))
        {
        }

        public CsvReader(TextReader reader, CsvSyntaxInfo syntax)
            : this(reader, syntax, default(CsvReaderOptions))
        {
        }

        public CsvReader(TextReader reader, CsvSyntaxInfo syntax, CsvReaderOptions options)
        {
#if DEBUG
            _allocStack = new System.Diagnostics.StackTrace();
#endif

            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            if (syntax == null)
            {
                throw new ArgumentNullException("syntax");
            }

            if (options.BufferSize <= 0)
            {
                _bufferSize = DEFAULT_BUFFER_SIZE;
            }
            else
            {
                _bufferSize = options.BufferSize;
            }

            if (reader is StreamReader)
            {
                Stream stream = ((StreamReader)reader).BaseStream;

                if (stream.CanSeek)
                {
                    // Handle bad implementations returning 0 or less
                    if (stream.Length > 0)
                    {
                        _bufferSize = (int)Math.Min(_bufferSize, stream.Length);
                    }
                }
            }

            if (syntax == null)
            {
                syntax = new CsvSyntaxInfo();
            }
            _sourceReader = reader;

            _fieldDelimiter = syntax.FieldDelimiter;
            _lineDelimiter1 = syntax.LineDelimiter1;
            _lineDelimiter2 = syntax.LineDelimiter2;
            _quote = syntax.Quote;
            _escape = syntax.Escape;
            _comment = '\0';

            _hasHeaders = syntax.HasHeaders;
            _trimmingOptions = options.ValueTrimmingOptions;
            _supportsMultiline = true;
            _skipEmptyLines = true;

            _currentRecordIndex = -1;
            _defaultParseErrorAction = CsvParseErrorAction.RaiseEvent;
        }

        #endregion

        #region Constants

        private const int DEFAULT_BUFFER_SIZE = 0x1000;

        #endregion

        #region Fields

        #region Settings

        private static readonly Type _fieldType = typeof(string);

        private static readonly StringComparer _fieldHeaderComparer = StringComparer.InvariantCultureIgnoreCase;

        private readonly TextReader _sourceReader;

        private readonly int _bufferSize;

        private readonly char _comment;

        private readonly char _escape;

        private readonly char _fieldDelimiter;

        private readonly char _lineDelimiter1;

        private readonly char _lineDelimiter2;

        private readonly char _quote;

        private readonly ValueTrimmingOptions _trimmingOptions;

        private readonly bool _hasHeaders;

        private readonly bool _supportsMultiline;

        private readonly bool _skipEmptyLines;

        #endregion

        #region State

        private CsvParseErrorAction _defaultParseErrorAction;

        private MissingFieldAction _missingFieldAction;

        private bool _initialized;

        private string[] _fieldHeaders;

        private Dictionary<string, int> _fieldHeaderIndexes;

        private long _currentRecordIndex;

        private int _nextFieldStart;

        private int _nextFieldIndex;

        private string[] _fields;

        private int _fieldCount;

        private char[] _buffer;

        private int _bufferLength;

        private bool _eof;

        private bool _eol;

        private bool _firstRecordInCache;

        private bool _missingFieldFlag;

        private bool _parseErrorFlag;

        #endregion

        #endregion

        #region Events

        public event EventHandler<CsvParseErrorEventArgs> ParseError;

        private void OnParseError(CsvParseErrorEventArgs e)
        {
            EventHandler<CsvParseErrorEventArgs> handler = ParseError;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion

        #region Properties

        #region Settings

        public char Comment
        {
            get
            {
                return _comment;
            }
        }

        public char Escape
        {
            get
            {
                return _escape;
            }
        }

        public char FieldDelimiter
        {
            get
            {
                return _fieldDelimiter;
            }
        }

        public char Quote
        {
            get
            {
                return _quote;
            }
        }

        public bool HasHeaders
        {
            get
            {
                return _hasHeaders;
            }
        }

        public ValueTrimmingOptions TrimmingOption
        {
            get
            {
                return _trimmingOptions;
            }
        }

        public int BufferSize
        {
            get
            {
                return _bufferSize;
            }
        }

        public bool SupportsMultiline
        {
            get
            {
                return _supportsMultiline;
            }
        }

        public bool SkipEmptyLines
        {
            get
            {
                return _skipEmptyLines;
            }
         }

        #endregion

        #region State

        public CsvParseErrorAction DefaultParseErrorAction
        {
            get
            {
                return _defaultParseErrorAction;
            }
            set
            {
                _defaultParseErrorAction = value;
            }
        }

        public MissingFieldAction MissingFieldAction
        {
            get
            {
                return _missingFieldAction;
            }
            set
            {
                _missingFieldAction = value;
            }
        }

        public int FieldCount
        {
            get
            {
                EnsureInitialize();
                return _fieldCount;
            }
        }

        public bool EndOfStream
        {
            get
            {
                return _eof;
            }
        }

        public string[] GetFieldHeaders()
        {
            EnsureInitialize();
            Debug.Assert(_fieldHeaders != null, "Field headers must be non null.");

            string[] fieldHeaders = new string[_fieldHeaders.Length];

            for (int i = 0; i < fieldHeaders.Length; i++)
                fieldHeaders[i] = _fieldHeaders[i];

            return fieldHeaders;
        }

        public long CurrentRecordIndex
        {
            get
            {
                return _currentRecordIndex;
            }
        }

        public bool MissingFieldFlag
        {
            get
            {
                return _missingFieldFlag;
            }
        }

        public bool ParseErrorFlag
        {
            get
            {
                return _parseErrorFlag;
            }
        }

        #endregion

        #endregion

        #region Indexers

        public string this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentNullException("name");
                }

                var index = GetFieldIndex(name);
                if (index < 0)
                {
                    throw new IndexOutOfRangeException(string.Format(Resources.FieldHeaderNotFound, name));
                }

                return this[index];
            }
        }

        public string this[int index]
        {
            get
            {
                var value = ReadField(index, false, false);
                return value == string.Empty ? null : value;
            }
        }

        #endregion

        #region Methods

        public int GetFieldIndex(string header)
        {
            EnsureInitialize();

            int index;

            if (_fieldHeaderIndexes != null && _fieldHeaderIndexes.TryGetValue(header, out index))
            {
                return index;
            }
            else if (!_hasHeaders)
            {
                if (int.TryParse(header, out index))
                {
                    return index;
                }
            }

            return -1;
        }

        public void CopyCurrentRecordTo(string[] array)
        {
            CopyCurrentRecordTo(array, 0);
        }

        public void CopyCurrentRecordTo(string[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException("index", index, string.Empty);

            if (_currentRecordIndex < 0 || !_initialized)
                throw new InvalidOperationException(Resources.CsvReaderNoCurrentRecord);

            if (array.Length - index < _fieldCount)
                throw new ArgumentException(Resources.CsvReaderNotEnoughSpaceInArray, "array");

            for (int i = 0; i < _fieldCount; i++)
            {
                if (_parseErrorFlag)
                    array[index + i] = null;
                else
                    array[index + i] = this[i];
            }
        }

        public string GetCurrentRawData()
        {
            if (_buffer != null && _bufferLength > 0)
                return new string(_buffer, 0, _bufferLength);
            else
                return string.Empty;
        }

        public void MoveTo(long record)
        {
            if (record < 0)
                throw new ArgumentOutOfRangeException("record", record, Resources.CsvReaderRecordIndexLessThanZero);

            if (record < _currentRecordIndex)
                throw new InvalidOperationException(Resources.CsvReaderCannotMovePreviousRecordInForwardOnly);

            // Get fileNumber of sourceRecord to read

            long offset = record - _currentRecordIndex;

            if (offset > 0)
            {
                do
                {
                    if (!ReadNextRecord(false, false))
                        throw new EndOfStreamException(string.Format(CultureInfo.InvariantCulture, Resources.CsvReaderCannotReadRecordAtIndex, _currentRecordIndex - offset));
                }
                while (--offset > 0);
            }
        }

        private void EnsureInitialize()
        {
            if (!_initialized)
                this.ReadNextRecord(true, false);

            Debug.Assert(_fieldHeaders != null);
            Debug.Assert(_fieldHeaders.Length > 0 || (_fieldHeaders.Length == 0 && _fieldHeaderIndexes == null));
        }

        private bool IsWhiteSpace(char c)
        {
            // Handle cases where the delimiter is a whitespace (e.g. tab)
            if (c == _fieldDelimiter)
                return false;
            else
            {
                // See char.IsLatin1(char c) in Reflector
                if (c <= '\x00ff')
                    return (c == ' ' || c == '\t');
                else
                    return (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.SpaceSeparator);
            }
        }

        private bool ParseNewLine(ref int pos)
        {
            Debug.Assert(pos <= _bufferLength);

            // Check if already at the end of the buffer
            if (pos == _bufferLength)
            {
                pos = 0;

                if (!ReadBuffer())
                    return false;
            }

            char c = _buffer[pos];

            // Treat \r as new line only if it's not the delimiter

            if (c == _lineDelimiter1 && _fieldDelimiter != _lineDelimiter1)
            {
                pos++;

                // Skip following \n (if there is one)

                if (pos < _bufferLength)
                {
                    if (_buffer[pos] == _lineDelimiter2)
                        pos++;
                }
                else
                {
                    if (ReadBuffer())
                    {
                        if (_buffer[0] == _lineDelimiter2)
                            pos = 1;
                        else
                            pos = 0;
                    }
                }

                if (pos >= _bufferLength)
                {
                    ReadBuffer();
                    pos = 0;
                }

                return true;
            }
            else if (c == _lineDelimiter2)
            {
                pos++;

                if (pos >= _bufferLength)
                {
                    ReadBuffer();
                    pos = 0;
                }

                return true;
            }

            return false;
        }

        private bool IsNewLine(int pos)
        {
            Debug.Assert(pos < _bufferLength);

            char c = _buffer[pos];

            if (c == _lineDelimiter2)
                return true;
            else if (c == _lineDelimiter1 && _fieldDelimiter != _lineDelimiter1)
                return true;
            else
                return false;
        }

        private bool ReadBuffer()
        {
            if (_eof)
                return false;

            CheckDisposed();

            _bufferLength = _sourceReader.Read(_buffer, 0, _bufferSize);

            if (_bufferLength > 0)
                return true;
            else
            {
                _eof = true;
                _buffer = null;

                return false;
            }
        }

        private string ReadField(int field, bool initializing, bool discardValue)
        {
            if (!initializing)
            {
                if (field < 0 || field >= _fieldCount)
                    throw new ArgumentOutOfRangeException("field", field, string.Format(CultureInfo.InvariantCulture, Resources.FieldIndexOutOfRange, field));

                if (_currentRecordIndex < 0)
                    throw new InvalidOperationException(Resources.CsvReaderNoCurrentRecord);

                // Directly return name if cached
                if (_fields[field] != null)
                    return _fields[field];
                else if (_missingFieldFlag)
                    return HandleMissingField(null, field, ref _nextFieldStart);
            }

            CheckDisposed();

            int index = _nextFieldIndex;

            while (index < field + 1)
            {
                // Handle case where stated start of name is past buffer
                // This can occur because _nextFieldStart is simply 1 + last char position of previous name
                if (_nextFieldStart == _bufferLength)
                {
                    _nextFieldStart = 0;

                    // Possible EOF will be handled later (see Handle_EOF1)
                    ReadBuffer();
                }

                string value = null;

                if (_missingFieldFlag)
                {
                    value = HandleMissingField(value, index, ref _nextFieldStart);
                }
                else if (_nextFieldStart == _bufferLength)
                {
                    // Handle_EOF1: Handle EOF here

                    // If current name is the requested name, then the defaultValue of the name is "" as in "f1,f2,f3,(\s*)"
                    // otherwise, the CSV is malformed

                    if (index == field)
                    {
                        if (!discardValue)
                        {
                            value = string.Empty;
                            _fields[index] = value;
                        }
                    }
                    else
                    {
                        value = HandleMissingField(value, index, ref _nextFieldStart);
                    }
                }
                else
                {
                    // Trim spaces at start
                    if ((_trimmingOptions & ValueTrimmingOptions.UnquotedOnly) != 0)
                        SkipWhiteSpaces(ref _nextFieldStart);

                    if (_eof)
                    {
                        value = string.Empty;
                    }
                    else if (_buffer[_nextFieldStart] != _quote)
                    {
                        // Non-quoted name

                        int start = _nextFieldStart;
                        int pos = _nextFieldStart;

                        for (; ; )
                        {
                            while (pos < _bufferLength)
                            {
                                char c = _buffer[pos];

                                if (c == _fieldDelimiter)
                                {
                                    _nextFieldStart = pos + 1;

                                    break;
                                }
                                else if (c == _lineDelimiter1 || c == _lineDelimiter2)
                                {
                                    _nextFieldStart = pos;
                                    _eol = true;

                                    break;
                                }
                                else
                                {
                                    pos++;
                                }
                            }

                            if (pos < _bufferLength)
                            {
                                break;
                            }
                            else
                            {
                                if (!discardValue)
                                {
                                    value += new string(_buffer, start, pos - start);
                                }

                                start = 0;
                                pos = 0;
                                _nextFieldStart = 0;

                                if (!ReadBuffer())
                                    break;
                            }
                        }

                        if (!discardValue)
                        {
                            if ((_trimmingOptions & ValueTrimmingOptions.UnquotedOnly) == 0)
                            {
                                if (!_eof && pos > start)
                                {
                                    value += new string(_buffer, start, pos - start);
                                }
                            }
                            else
                            {
                                if (!_eof && pos > start)
                                {
                                    // Do the trimming
                                    pos--;
                                    while (pos > -1 && IsWhiteSpace(_buffer[pos]))
                                        pos--;
                                    pos++;

                                    if (pos > 0)
                                        value += new string(_buffer, start, pos - start);
                                }
                                else
                                    pos = -1;

                                // If pos <= 0, that means the trimming went past buffer start,
                                // and the concatenated defaultValue needs to be trimmed too.
                                if (pos <= 0)
                                {
                                    pos = (value == null ? -1 : value.Length - 1);

                                    // Do the trimming
                                    while (pos > -1 && IsWhiteSpace(value[pos]))
                                        pos--;

                                    pos++;

                                    if (pos > 0 && pos != value.Length)
                                        value = value.Substring(0, pos);
                                }
                            }

                            if (value == null)
                                value = string.Empty;
                        }

                        if (_eol || _eof)
                        {
                            _eol = ParseNewLine(ref _nextFieldStart);

                            // Reaching a new line is ok as long as the parser is initializing or it is the last name
                            if (!initializing && index != _fieldCount - 1)
                            {
                                if (value != null && value.Length == 0)
                                    value = null;

                                value = HandleMissingField(value, index, ref _nextFieldStart);
                            }
                        }

                        if (!discardValue)
                            _fields[index] = value;
                    }
                    else
                    {
                        // Quoted name

                        // Skip quote
                        int start = _nextFieldStart + 1;
                        int pos = start;

                        bool quoted = true;
                        bool escaped = false;

                        if ((_trimmingOptions & ValueTrimmingOptions.QuotedOnly) != 0)
                        {
                            SkipWhiteSpaces(ref start);
                            pos = start;
                        }

                        for (; ; )
                        {
                            while (pos < _bufferLength)
                            {
                                char c = _buffer[pos];

                                if (escaped)
                                {
                                    escaped = false;
                                    start = pos;
                                }
                                // IF current char is escape AND (escape and quote are different OR next char is a quote)
                                else if (c == _escape && (_escape != _quote || (pos + 1 < _bufferLength && _buffer[pos + 1] == _quote) || (pos + 1 == _bufferLength && _sourceReader.Peek() == _quote)))
                                {
                                    if (!discardValue)
                                        value += new string(_buffer, start, pos - start);

                                    escaped = true;
                                }
                                else if (c == _quote)
                                {
                                    quoted = false;
                                    break;
                                }

                                pos++;
                            }

                            if (!quoted)
                                break;
                            else
                            {
                                if (!discardValue && !escaped)
                                    value += new string(_buffer, start, pos - start);

                                start = 0;
                                pos = 0;
                                _nextFieldStart = 0;

                                if (!ReadBuffer())
                                {
                                    HandleParseError(new MalformedCsvException(GetCurrentRawData(), _nextFieldStart, Math.Max(0, _currentRecordIndex), index), ref _nextFieldStart);
                                    return null;
                                }
                            }
                        }

                        if (!_eof)
                        {
                            // Append remaining parsed buffer content
                            if (!discardValue && pos > start)
                                value += new string(_buffer, start, pos - start);

                            if (!discardValue && value != null && (_trimmingOptions & ValueTrimmingOptions.QuotedOnly) != 0)
                            {
                                int newLength = value.Length;
                                while (newLength > 0 && IsWhiteSpace(value[newLength - 1]))
                                    newLength--;

                                if (newLength < value.Length)
                                    value = value.Substring(0, newLength);
                            }

                            // Skip quote
                            _nextFieldStart = pos + 1;

                            // Skip whitespaces between the quote and the delimiter/eol
                            SkipWhiteSpaces(ref _nextFieldStart);

                            // Skip delimiter
                            bool delimiterSkipped;
                            if (_nextFieldStart < _bufferLength && _buffer[_nextFieldStart] == _fieldDelimiter)
                            {
                                _nextFieldStart++;
                                delimiterSkipped = true;
                            }
                            else
                            {
                                delimiterSkipped = false;
                            }

                            // Skip new line delimiter if initializing or last name
                            // (if the next name is missing, it will be caught when parsed)
                            if (!_eof && !delimiterSkipped && (initializing || index == _fieldCount - 1))
                                _eol = ParseNewLine(ref _nextFieldStart);

                            // If no delimiter is present after the quoted name and it is not the last name, then it is a parsing error
                            if (!delimiterSkipped && !_eof && !(_eol || IsNewLine(_nextFieldStart)))
                                HandleParseError(new MalformedCsvException(GetCurrentRawData(), _nextFieldStart, Math.Max(0, _currentRecordIndex), index), ref _nextFieldStart);
                        }

                        if (!discardValue)
                        {
                            if (value == null)
                                value = string.Empty;

                            _fields[index] = value;
                        }
                    }
                }

                _nextFieldIndex = Math.Max(index + 1, _nextFieldIndex);

                if (index == field)
                {
                    // If initializing, return null to signify the last name has been reached

                    if (initializing)
                    {
                        if (_eol || _eof)
                            return null;
                        else
                            return string.IsNullOrEmpty(value) ? string.Empty : value;
                    }
                    else
                        return value;
                }

                index++;
            }

            // Getting here is bad ...
            HandleParseError(new MalformedCsvException(GetCurrentRawData(), _nextFieldStart, Math.Max(0, _currentRecordIndex), index), ref _nextFieldStart);
            return null;
        }

        private bool ReadNextRecord(bool onlyReadHeaders, bool skipToNextLine)
        {
            if (_eof)
            {
                if (_firstRecordInCache)
                {
                    _firstRecordInCache = false;
                    _currentRecordIndex++;

                    return true;
                }
                else
                    return false;
            }

            CheckDisposed();

            if (!_initialized)
            {
                _buffer = new char[_bufferSize];

                // will be replaced if and when headers are read
                _fieldHeaders = new string[0];

                if (!ReadBuffer())
                    return false;

                if (!SkipEmptyAndCommentedLines(ref _nextFieldStart))
                    return false;

                // Keep growing _fields array until the last name has been found
                // and then resize it to its final correct size

                _fieldCount = 0;
                _fields = new string[16];

                while (ReadField(_fieldCount, true, false) != null)
                {
                    if (_parseErrorFlag)
                    {
                        _fieldCount = 0;
                        Array.Clear(_fields, 0, _fields.Length);
                        _parseErrorFlag = false;
                        _nextFieldIndex = 0;
                    }
                    else
                    {
                        _fieldCount++;

                        if (_fieldCount == _fields.Length)
                            Array.Resize<string>(ref _fields, (_fieldCount + 1) * 2);
                    }
                }

                // _fieldCount contains the last name index, but it must contains the name count,
                // so increment by 1
                _fieldCount++;

                if (_fields.Length != _fieldCount)
                    Array.Resize<string>(ref _fields, _fieldCount);

                _initialized = true;

                // If headers are present, call ReadNextRecord again
                if (_hasHeaders)
                {
                    // Don't count first sourceRecord as it was the headers
                    _currentRecordIndex = -1;

                    _firstRecordInCache = false;

                    _fieldHeaders = new string[_fieldCount];
                    _fieldHeaderIndexes = new Dictionary<string, int>(_fieldCount, _fieldHeaderComparer);

                    for (int i = 0; i < _fields.Length; i++)
                    {
                        _fieldHeaders[i] = _fields[i];
                        //todo: handle exception with duplicate headers
                        _fieldHeaderIndexes.Add(_fields[i], i);
                    }

                    // Proceed to first sourceRecord
                    if (!onlyReadHeaders)
                    {
                        // Calling again ReadNextRecord() seems to be simpler, 
                        // but in fact would probably cause many subtle bugs because the derived does not expect a recursive behavior
                        // so simply do what is needed here and no more.

                        if (!SkipEmptyAndCommentedLines(ref _nextFieldStart))
                            return false;

                        Array.Clear(_fields, 0, _fields.Length);
                        _nextFieldIndex = 0;
                        _eol = false;

                        _currentRecordIndex++;
                        return true;
                    }
                }
                else
                {
                    if (onlyReadHeaders)
                    {
                        _firstRecordInCache = true;
                        _currentRecordIndex = -1;
                    }
                    else
                    {
                        _firstRecordInCache = false;
                        _currentRecordIndex = 0;
                    }

                    _fieldHeaders = new string[_fields.Length];
                    _fieldHeaderIndexes = new Dictionary<string, int>(_fieldHeaders.Length, _fieldHeaderComparer);

                    for (int i = 0; i < _fields.Length - 1; i++)
                    {
                        _fieldHeaders[i] = i.ToString();
                        _fieldHeaderIndexes.Add(_fields[i], i);
                    }
                }
            }
            else
            {
                if (skipToNextLine)
                    SkipToNextLine(ref _nextFieldStart);
                else if (_currentRecordIndex > -1 && !_missingFieldFlag)
                {
                    // If not already at end of sourceRecord, move there
                    if (!_eol && !_eof)
                    {
                        if (!_supportsMultiline)
                            SkipToNextLine(ref _nextFieldStart);
                        else
                        {
                            // a dirty trick to handle the case where extra fields are present
                            while (ReadField(_nextFieldIndex, true, true) != null)
                            {
                            }
                        }
                    }
                }

                if (!_firstRecordInCache && !SkipEmptyAndCommentedLines(ref _nextFieldStart))
                    return false;

                if (_hasHeaders || !_firstRecordInCache)
                    _eol = false;

                // Check to see if the first sourceRecord is in cache.
                // This can happen when initializing a mapReader with no headers
                // because one sourceRecord must be read to get the name count automatically
                if (_firstRecordInCache)
                    _firstRecordInCache = false;
                else
                {
                    Array.Clear(_fields, 0, _fields.Length);
                    _nextFieldIndex = 0;
                }

                _missingFieldFlag = false;
                _parseErrorFlag = false;
                _currentRecordIndex++;
            }

            return true;
        }

        private bool SkipEmptyAndCommentedLines(ref int pos)
        {
            if (pos < _bufferLength)
                DoSkipEmptyAndCommentedLines(ref pos);

            while (pos >= _bufferLength && !_eof)
            {
                if (ReadBuffer())
                {
                    pos = 0;
                    DoSkipEmptyAndCommentedLines(ref pos);
                }
                else
                    return false;
            }

            return !_eof;
        }

        private void DoSkipEmptyAndCommentedLines(ref int pos)
        {
            while (pos < _bufferLength)
            {
                if (_buffer[pos] == _comment)
                {
                    pos++;
                    SkipToNextLine(ref pos);
                }
                else if (_skipEmptyLines && ParseNewLine(ref pos))
                    continue;
                else
                    break;
            }
        }

        private bool SkipWhiteSpaces(ref int pos)
        {
            for (; ; )
            {
                while (pos < _bufferLength && IsWhiteSpace(_buffer[pos]))
                    pos++;

                if (pos < _bufferLength)
                    break;
                else
                {
                    pos = 0;

                    if (!ReadBuffer())
                        return false;
                }
            }

            return true;
        }

        private bool SkipToNextLine(ref int pos)
        {
            // ((pos = 0) == 0) is a little trick to reset position inline
            while ((pos < _bufferLength || (ReadBuffer() && ((pos = 0) == 0))) && !ParseNewLine(ref pos))
                pos++;

            return !_eof;
        }

        private void HandleParseError(MalformedCsvException error, ref int pos)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            _parseErrorFlag = true;

            switch (_defaultParseErrorAction)
            {
                case CsvParseErrorAction.ThrowException:
                    throw error;

                case CsvParseErrorAction.RaiseEvent:
                    CsvParseErrorEventArgs e = new CsvParseErrorEventArgs(error, CsvParseErrorAction.ThrowException);
                    OnParseError(e);

                    switch (e.Action)
                    {
                        case CsvParseErrorAction.ThrowException:
                            throw e.Error;

                        case CsvParseErrorAction.RaiseEvent:
                            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resources.CsvReaderParseErrorActionInvalidInsideParseErrorEvent, e.Action), e.Error);

                        case CsvParseErrorAction.AdvanceToNextLine:
                            // already at EOL when fields are missing, so don't skip to next line in that case
                            if (!_missingFieldFlag && pos >= 0)
                                SkipToNextLine(ref pos);
                            break;

                        default:
                            throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, Resources.CsvReaderParseErrorActionNotSupported, e.Action), e.Error);
                    }
                    break;

                case CsvParseErrorAction.AdvanceToNextLine:
                    // already at EOL when fields are missing, so don't skip to next line in that case
                    if (!_missingFieldFlag && pos >= 0)
                        SkipToNextLine(ref pos);
                    break;

                default:
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, Resources.CsvReaderParseErrorActionNotSupported, _defaultParseErrorAction), error);
            }
        }

        private string HandleMissingField(string value, int fieldIndex, ref int currentPosition)
        {
            if (fieldIndex < 0 || fieldIndex >= _fieldCount)
                throw new ArgumentOutOfRangeException("fieldIndex", fieldIndex, string.Format(CultureInfo.InvariantCulture, Resources.FieldIndexOutOfRange, fieldIndex));

            _missingFieldFlag = true;

            for (int i = fieldIndex + 1; i < _fieldCount; i++)
                _fields[i] = null;

            if (value != null)
                return value;
            else
            {
                switch (_missingFieldAction)
                {
                    case MissingFieldAction.ParseError:
                        HandleParseError(new MissingFieldCsvException(GetCurrentRawData(), currentPosition, Math.Max(0, _currentRecordIndex), fieldIndex), ref currentPosition);
                        return value;

                    case MissingFieldAction.ReplaceByEmpty:
                        return string.Empty;

                    case MissingFieldAction.ReplaceByNull:
                        return null;

                    default:
                        throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, Resources.CsvReaderMissingFieldActionNotSupported, _missingFieldAction));
                }
            }
        }

        private void ValidateDataReader(DataReaderValidations validations)
        {
            if ((validations & DataReaderValidations.IsInitialized) != 0 && !_initialized)
                throw new InvalidOperationException(Resources.CsvReaderNoCurrentRecord);

            if ((validations & DataReaderValidations.IsNotClosed) != 0 && _isDisposed)
                throw new InvalidOperationException(Resources.CsvReaderClosed);
        }

        private long CopyFieldToArray(int field, long fieldOffset, Array destinationArray, int destinationOffset, int length)
        {
            EnsureInitialize();

            if (field < 0 || field >= _fieldCount)
                throw new ArgumentOutOfRangeException("field", field, string.Format(CultureInfo.InvariantCulture, Resources.FieldIndexOutOfRange, field));

            if (fieldOffset < 0 || fieldOffset >= int.MaxValue)
                throw new ArgumentOutOfRangeException("fieldOffset");

            // Array.Copy(...) will do the remaining argument checks

            if (length == 0)
                return 0;

            string value = this[field];

            if (value == null)
                value = string.Empty;

            Debug.Assert(fieldOffset < int.MaxValue);

            Debug.Assert(destinationArray.GetType() == typeof(char[]) || destinationArray.GetType() == typeof(byte[]));

            if (destinationArray.GetType() == typeof(char[]))
                Array.Copy(value.ToCharArray((int)fieldOffset, length), 0, destinationArray, destinationOffset, length);
            else
            {
                char[] chars = value.ToCharArray((int)fieldOffset, length);
                byte[] source = new byte[chars.Length];
                ;

                for (int i = 0; i < chars.Length; i++)
                    source[i] = Convert.ToByte(chars[i]);

                Array.Copy(source, 0, destinationArray, destinationOffset, length);
            }

            return length;
        }

        private Exception GetCastException(Type type)
        {
            return new InvalidCastException(string.Format(Properties.Resources.CsvReaderCannotConvertValue, _fieldType.FullName, type.FullName));
        }

        private void CheckIndex(int i)
        {
            if (i < 0 || i >= _fieldCount)
            {
                throw new IndexOutOfRangeException(string.Format(Properties.Resources.CsvReaderFieldIndexNotFound, i));
            }
        }

        #endregion

        #region IDataReader Members

        int IDataReader.RecordsAffected
        {
            get
            {
                return -1;
            }
        }

        bool IDataReader.IsClosed
        {
            get
            {
                return _eof;
            }
        }

        bool IDataReader.NextResult()
        {
            ValidateDataReader(DataReaderValidations.IsNotClosed);
            return false;
        }

        void IDataReader.Close()
        {
            Dispose();
        }

        public bool Read()
        {
            ValidateDataReader(DataReaderValidations.IsNotClosed);
            return ReadNextRecord(false, false);
        }

        int IDataReader.Depth
        {
            get
            {
                ValidateDataReader(DataReaderValidations.IsNotClosed);
                return 0;
            }
        }

        DataTable IDataReader.GetSchemaTable()
        {
            EnsureInitialize();
            ValidateDataReader(DataReaderValidations.IsNotClosed);

            DataTable schema = new DataTable("SchemaTable");
            schema.Locale = CultureInfo.InvariantCulture;
            schema.MinimumCapacity = _fieldCount;

            schema.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.BaseSchemaName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.BaseTableName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.ColumnName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.DataType, typeof(object)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.IsAliased, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.IsExpression, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.IsKey, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.IsLong, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(short)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.NumericScale, typeof(short)).ReadOnly = true;
            schema.Columns.Add(SchemaTableColumn.ProviderType, typeof(int)).ReadOnly = true;

            schema.Columns.Add(SchemaTableOptionalColumn.BaseCatalogName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableOptionalColumn.BaseServerName, typeof(string)).ReadOnly = true;
            schema.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableOptionalColumn.IsHidden, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableOptionalColumn.IsReadOnly, typeof(bool)).ReadOnly = true;
            schema.Columns.Add(SchemaTableOptionalColumn.IsRowVersion, typeof(bool)).ReadOnly = true;

            string[] columnNames;

            if (_hasHeaders)
                columnNames = _fieldHeaders;
            else
            {
                columnNames = new string[_fieldCount];

                for (int i = 0; i < _fieldCount; i++)
                    columnNames[i] = "Column" + i.ToString(CultureInfo.InvariantCulture);
            }

            // null marks columns that will change for each row
            object[] schemaRow = new object[] { 
                    true,					// 00- AllowDBNull
                    null,					// 01- BaseColumnName
                    string.Empty,			// 02- BaseSchemaName
                    string.Empty,			// 03- BaseTableName
                    null,					// 04- ColumnName
                    null,					// 05- ColumnOrdinal
                    int.MaxValue,			// 06- ColumnSize
                    typeof(string),			// 07- DataType
                    false,					// 08- IsAliased
                    false,					// 09- IsExpression
                    false,					// 10- IsKey
                    false,					// 11- IsLong
                    false,					// 12- IsUnique
                    DBNull.Value,			// 13- NumericPrecision
                    DBNull.Value,			// 14- NumericScale
                    (int) DbType.String,	// 15- ProviderType

                    string.Empty,			// 16- BaseCatalogName
                    string.Empty,			// 17- BaseServerName
                    false,					// 18- IsAutoIncrement
                    false,					// 19- IsHidden
                    true,					// 20- IsReadOnly
                    false					// 21- IsRowVersion
              };

            for (int i = 0; i < columnNames.Length; i++)
            {
                schemaRow[1] = columnNames[i]; // Base column stepName
                schemaRow[4] = columnNames[i]; // Column stepName
                schemaRow[5] = i; // Column ordinal

                schema.Rows.Add(schemaRow);
            }

            return schema;
        }

        #endregion

        #region IDataRecord Members

        object IDataRecord.this[string name]
        {
            get
            {
                ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
                return this[name];
            }
        }

        object IDataRecord.this[int i]
        {
            get
            {
                ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
                return this[i];
            }
        }

        bool IDataRecord.GetBoolean(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Boolean));
        }

        long IDataRecord.GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
            return CopyFieldToArray(i, fieldOffset, buffer, bufferoffset, length);
        }

        byte IDataRecord.GetByte(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Byte));
        }

        char IDataRecord.GetChar(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Char));
        }

        long IDataRecord.GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
            return CopyFieldToArray(i, fieldoffset, buffer, bufferoffset, length);
        }

        IDataReader IDataRecord.GetData(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(IDataReader));
        }

        string IDataRecord.GetDataTypeName(int i)
        {
            ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
            return _fieldType.FullName;
        }

        DateTime IDataRecord.GetDateTime(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(DateTime));
        }

        decimal IDataRecord.GetDecimal(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Decimal));
        }

        double IDataRecord.GetDouble(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Double));
        }

        Guid IDataRecord.GetGuid(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Guid));
        }

        Type IDataRecord.GetFieldType(int i)
        {
            EnsureInitialize();
            ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);

            CheckIndex(i);
            return _fieldType;
        }

        float IDataRecord.GetFloat(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Single));
        }

        int IDataRecord.GetValues(object[] values)
        {
            ValidateDataReader(DataReaderValidations.IsInitialized | DataReaderValidations.IsNotClosed);
            for (int i = 0; i < _fieldCount; i++)
            {
                values[i] = this[i];
            }

            return _fieldCount;
        }

        bool IDataRecord.IsDBNull(int i)
        {
            CheckIndex(i);
            return false;
        }

        short IDataRecord.GetInt16(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Int16));
        }

        int IDataRecord.GetInt32(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Int32));
        }

        long IDataRecord.GetInt64(int i)
        {
            CheckIndex(i);
            throw GetCastException(typeof(Int64));
        }

        string IDataRecord.GetName(int i)
        {
            CheckIndex(i);

            if (_hasHeaders)
            {
                return _fieldHeaders[i];
            }
            else
            {
                return i.ToString();
            }
        }

        int IDataRecord.GetOrdinal(string name)
        {
            int index;
            if (!_fieldHeaderIndexes.TryGetValue(name, out index))
            {
                throw new IndexOutOfRangeException(string.Format(Resources.FieldHeaderNotFound, name));
            }
            return index;
        }

        string IDataRecord.GetString(int i)
        {
            return this[i];
        }

        object IDataRecord.GetValue(int i)
        {
            return this[i];
        }

        #endregion

        #region IEnumerable<IDataRecord> Members

        public IEnumerator<IDataRecord> GetEnumerator()
        {
            return new CsvReader.DataRecordEnumerator(this);
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IDisposable members

#if DEBUG
        private System.Diagnostics.StackTrace _allocStack;
#endif

        private bool _isDisposed = false;

        private readonly object _lock = new object();

        [System.ComponentModel.Browsable(false)]
        public bool IsDisposed
        {
            get
            {
                return _isDisposed;
            }
        }

        /// <summary>
        /// Checks if the instance has been disposed of, and if it has, throws an <see cref="T:System.ComponentModel.ObjectDisposedException"/>; otherwise, does nothing.
        /// </summary>
        /// <exception cref="T:System.ComponentModel.ObjectDisposedException">
        /// 	The instance has been disposed of.
        /// </exception>
        /// <remarks>
        /// 	Derived classes should call this method at the start of all methods and properties that should not be accessed after a call to <see cref="M:Dispose()"/>.
        /// </remarks>
        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        /// <summary>
        /// Releases all resources used by the instance.
        /// </summary>
        /// <remarks>
        /// 	Calls <see cref="M:Dispose(Boolean)"/> with the disposing param set to <see langword="true"/> to free unmanaged and managed Resources.
        /// </remarks>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by this instance and optionally releases the managed Resources.
        /// </summary>
        /// <param stepName="disposing">
        /// 	<see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged Resources.
        /// </param>
        private void Dispose(bool disposing)
        {
            // Refer to http://www.bluebytesoftware.com/blog/PermaLink,guid,88e62cdf-5919-4ac7-bc33-20c06ae539ae.aspx
            // Refer to http://www.gotdotnet.com/team/libraries/whitepapers/resourcemanagement/resourcemanagement.aspx

            // No exception should ever be thrown except in critical scenarios.
            // Unhandled exceptions during finalization will tear down the process.
            if (!_isDisposed)
            {
                try
                {
                    // Dispose-time code should call Dispose() on all owned objects that implement the IDisposable interface. 
                    // "owned" means objects whose lifetime is solely controlled by the container. 
                    // In cases where ownership is not as straightforward, techniques such as HandleCollector can be used.  
                    // Large managed object fields should be nulled out.

                    // Dispose-time code should also set references of all owned objects to null, after disposing them. This will allow the referenced objects to be garbage collected even if not all references to the "parent" are released. It may be a significant memory consumption win if the referenced objects are large, such as big arrays, collections, etc. 
                    if (disposing)
                    {
                        // Acquire a lock on the object while disposing.

                        if (_sourceReader != null)
                        {
                            lock (_lock)
                            {
                                if (_sourceReader != null)
                                {
                                    _sourceReader.Dispose();

                                    //_sourceReader = null;
                                    _buffer = null;
                                    _eof = true;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // Ensure that the flag is set
                    _isDisposed = true;
                }
            }
        }

        ~CsvReader()
        {
#if DEBUG
            Debug.WriteLine("FinalizableObject was not disposed" + _allocStack.ToString());
#endif

            Dispose(false);
        }

        #endregion
    }
}