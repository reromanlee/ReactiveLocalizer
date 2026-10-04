namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// Walks the lines of a file's text without allocating them, accepting <c>\r\n</c>, <c>\n</c> and <c>\r</c> line
    /// endings alike and skipping a leading byte order mark.
    /// </summary>
    internal struct DocumentLineReader
    {
        private const char ByteOrderMark = '\uFEFF';

        private readonly string _text;
        private int _position;
        private int _lineNumber;

        public DocumentLineReader(string text)
        {
            _text = text ?? string.Empty;
            _position = _text.Length > 0 && _text[0] == ByteOrderMark ? 1 : 0;
            _lineNumber = 0;
        }

        /// <summary>
        /// Reads the next line as a range of the text, without its line ending. Returns false once the text is
        /// exhausted; a final line without a line ending is still read, while a text ending in one adds no empty line.
        /// </summary>
        public bool TryReadLine(out int lineNumber, out int start, out int length)
        {
            if (_position >= _text.Length)
            {
                lineNumber = _lineNumber;
                start = _text.Length;
                length = 0;
                return false;
            }
            start = _position;
            int end = start;
            // Scan to the first line ending character, or the end of the text.
            while (end < _text.Length && _text[end] != '\n' && _text[end] != '\r')
            {
                end++;
            }
            length = end - start;
            // Step over the line ending, treating \r\n as one ending rather than two.
            if (end < _text.Length && _text[end] == '\r' && end + 1 < _text.Length && _text[end + 1] == '\n')
            {
                end++;
            }
            _position = end + 1;
            _lineNumber++;
            lineNumber = _lineNumber;
            return true;
        }
    }
}
