namespace RoslynPad.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using System.Xml;

    internal interface IConsoleDumper
    {
        bool SupportsRedirect { get; }

        TextWriter CreateWriter(string? header = null);

        TextReader CreateReader();

        void Dump(DumpData data);

        void DumpException(Exception exception);

        void Flush();

        void DumpProgress(ProgressResultObject result);

        void DumpDictionaryList(DictionaryListResultObject result);
    }

    internal class DirectConsoleDumper : IConsoleDumper
    {
        private readonly object _lock = new object();

        public bool SupportsRedirect => false;

        public TextWriter CreateWriter(string? header = null)
        {
            throw new NotSupportedException();
        }

        public void Dump(DumpData data)
        {
            try
            {
                DumpResultObject(ResultObject.Create(data.Object, data.Quotas, data.Header));
            }
            catch (Exception ex)
            {
                try
                {
                    Console.WriteLine("Error during Dump: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void DumpException(Exception exception)
        {
            throw new NotSupportedException();
        }

        public TextReader CreateReader()
        {
            throw new NotSupportedException();
        }

        private void DumpResultObject(ResultObject resultObject, int indent = 0)
        {
            lock (_lock)
            {
                if (indent > 0)
                {
                    Console.Write(string.Empty.PadLeft(indent));
                }

                Console.Write(resultObject.HasChildren ? "+ " : "  ");

                if (resultObject.Header != null)
                {
                    Console.Write($"[{resultObject.Header}]: ");
                }

                Console.WriteLine(resultObject.Value);

                if (resultObject.Children != null)
                {
                    foreach (var child in resultObject.Children)
                    {
                        DumpResultObject(child, indent + 2);
                    }
                }

                if (indent == 0)
                {
                    Console.WriteLine();
                }
            }
        }

        public void Flush()
        {
        }

        public void DumpProgress(ProgressResultObject result)
            => throw new NotSupportedException($"Dumping progress is not supported with {nameof(DirectConsoleDumper)}");

        public void DumpDictionaryList(DictionaryListResultObject result)
        {
            throw new NotImplementedException();
        }
    }

    internal class JsonConsoleDumper : IConsoleDumper, IDisposable
    {
        private const int MaxDumpsPerSession = 100000;

        private static readonly byte[] NewLine = Encoding.Default.GetBytes(Environment.NewLine);

        private readonly string _exceptionResultTypeName;
        private readonly string _inputReadRequestTypeName;
        private readonly string _progressResultTypeName;
        private readonly string _dictionaryListResultTypeName;

        private readonly Stream _stream;

        private readonly object _lock;

        private int _dumpCount;

        public JsonConsoleDumper()
        {
            _stream = Console.OpenStandardOutput();

            _lock = new object();

            var assemblyName = typeof(ExceptionResultObject).Assembly.GetName().Name;
            _exceptionResultTypeName = $"{typeof(ExceptionResultObject).FullName}, {assemblyName}";
            _inputReadRequestTypeName = $"{typeof(InputReadRequest).FullName}, {assemblyName}";
            _progressResultTypeName = $"{typeof(ProgressResultObject).FullName}, {assemblyName}";
            _dictionaryListResultTypeName = $"{typeof(DictionaryListResultObject).FullName}, {assemblyName}";
        }

        private XmlDictionaryWriter CreateJsonWriter()
        {
            // this assembly shouldn't have any external dependencies, so using this legacy JSON writer
            return JsonReaderWriterFactory.CreateJsonWriter(_stream, Encoding.UTF8, ownsStream: false);
        }

        public bool SupportsRedirect => true;

        public TextWriter CreateWriter(string? header = null)
        {
            return new ConsoleRedirectWriter(this, header);
        }

        public TextReader CreateReader()
        {
            return new ConsoleReader(this);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public void Dump(DumpData data)
        {
            if (!CanDump())
            {
                return;
            }

            try
            {
                DumpResultObject(ResultObject.Create(data.Object, data.Quotas, data.Header));
            }
            catch (Exception ex)
            {
                try
                {
                    DumpMessage("Error during Dump: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void DumpException(Exception exception)
        {
            if (!CanDump())
            {
                return;
            }

            try
            {
                DumpExceptionResultObject(ExceptionResultObject.Create(exception));
            }
            catch (Exception ex)
            {
                try
                {
                    DumpMessage("Error during Dump: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void DumpProgress(ProgressResultObject result)
        {
            lock (_lock)
            {
                string jsonValue = result.Progress.HasValue
                    ? result.Progress.Value.ToString(CultureInfo.InvariantCulture)
                    : "null";

                using (var jsonWriter = CreateJsonWriter())
                {
                    jsonWriter.WriteStartElement("root", string.Empty);
                    jsonWriter.WriteAttributeString("type", "object");
                    jsonWriter.WriteElementString("$type", _progressResultTypeName);
                    jsonWriter.WriteElementString("p", jsonValue);
                    jsonWriter.WriteEndElement();
                }

                DumpNewLine();
            }
        }

        public void DumpDictionaryList(DictionaryListResultObject result)
        {
            lock (_lock)
            {
                using (var jsonWriter = CreateJsonWriter())
                {
                    jsonWriter.WriteStartElement("root", string.Empty);
                    jsonWriter.WriteAttributeString("type", "object");
                    jsonWriter.WriteElementString("$type", _dictionaryListResultTypeName);

                    jsonWriter.WriteStartElement("i");
                    jsonWriter.WriteAttributeString("type", "array");

                    foreach (var item in result.Items)
                    {
                        WriteDictionary(jsonWriter, item);
                    }

                    jsonWriter.WriteEndElement();
                    jsonWriter.WriteEndElement();
                }

                DumpNewLine();
            }
        }

        public void Flush()
        {
            _stream.Flush();
        }

        private bool CanDump()
        {
            var currentCount = Interlocked.Increment(ref _dumpCount);
            if (currentCount >= MaxDumpsPerSession)
            {
                if (currentCount == MaxDumpsPerSession)
                {
                    DumpMessage("<max results reached>");
                }

                return false;
            }

            return true;
        }

        private void DumpMessage(string message)
        {
            lock (_lock)
            {
                using (var jsonWriter = CreateJsonWriter())
                {
                    jsonWriter.WriteStartElement("root", string.Empty);
                    jsonWriter.WriteAttributeString("type", "object");
                    jsonWriter.WriteElementString("v", message);
                    jsonWriter.WriteEndElement();
                }

                DumpNewLine();
            }
        }

        private void DumpInputReadRequest()
        {
            try
            {
                lock (_lock)
                {
                    using (var jsonWriter = CreateJsonWriter())
                    {
                        jsonWriter.WriteStartElement("root", string.Empty);
                        jsonWriter.WriteAttributeString("type", "object");
                        jsonWriter.WriteElementString("$type", _inputReadRequestTypeName);
                        jsonWriter.WriteEndElement();
                    }

                    DumpNewLine();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void DumpExceptionResultObject(ExceptionResultObject result)
        {
            lock (_lock)
            {
                using (var jsonWriter = CreateJsonWriter())
                {
                    jsonWriter.WriteStartElement("root", string.Empty);
                    jsonWriter.WriteAttributeString("type", "object");
                    jsonWriter.WriteElementString("$type", _exceptionResultTypeName);
                    jsonWriter.WriteElementString("m", result.Message);
                    jsonWriter.WriteStartElement("l");
                    jsonWriter.WriteValue(result.LineNumber);
                    jsonWriter.WriteEndElement();
                    WriteResultObjectContent(jsonWriter, result);
                    jsonWriter.WriteEndElement();
                }

                DumpNewLine();
            }
        }

        private void DumpResultObject(ResultObject result)
        {
            lock (_lock)
            {
                using (var jsonWriter = CreateJsonWriter())
                {
                    WriteResultObject(jsonWriter, result, isRoot: true);
                }

                DumpNewLine();
            }
        }

        private void DumpNewLine()
        {
            _stream.Write(NewLine, 0, NewLine.Length);
        }

        private void WriteResultObject(XmlDictionaryWriter jsonWriter, ResultObject result, bool isRoot)
        {
            jsonWriter.WriteStartElement(isRoot ? "root" : "item", string.Empty);
            jsonWriter.WriteAttributeString("type", "object");
            WriteResultObjectContent(jsonWriter, result);
            jsonWriter.WriteEndElement();
        }

        private void WriteDictionary(XmlWriter jsonWriter, IDictionary<string, object?> dic)
        {
            jsonWriter.WriteStartElement("item", string.Empty);
            jsonWriter.WriteAttributeString("type", "object");

            foreach (var o in dic)
            {
                if (o.Value == null)
                {
                    jsonWriter.WriteElementString(o.Key, "null");
                }
                else
                {
                    jsonWriter.WriteStartElement(o.Key);
                    jsonWriter.WriteValue(o.Value);
                    jsonWriter.WriteEndElement();
                }
              
            }

            jsonWriter.WriteEndElement();
        }

        private void WriteResultObjectContent(XmlDictionaryWriter jsonWriter, ResultObject result)
        {
            jsonWriter.WriteElementString("t", result.Type);
            jsonWriter.WriteElementString("h", result.Header);
            jsonWriter.WriteElementString("v", result.Value);
            jsonWriter.WriteStartElement("x");
            jsonWriter.WriteValue(result.IsExpanded);
            jsonWriter.WriteEndElement();

            if (result.Children != null)
            {
                jsonWriter.WriteStartElement("c");
                jsonWriter.WriteAttributeString("type", "array");

                foreach (var child in result.Children)
                {
                    WriteResultObject(jsonWriter, child, isRoot: false);
                }

                jsonWriter.WriteEndElement();
            }
        }

        /// <summary>
        /// Redirects the console to the Dump method.
        /// </summary>
        private class ConsoleRedirectWriter : TextWriter
        {
            private readonly JsonConsoleDumper _dumper;
            private readonly string? _header;

            public override Encoding Encoding => Encoding.UTF8;

            public ConsoleRedirectWriter(JsonConsoleDumper dumper, string? header = null)
            {
                _dumper = dumper;
                _header = header;
            }

            public override void Write(string? value)
            {
                if (string.Equals(Environment.NewLine, value, StringComparison.Ordinal))
                {
                    return;
                }

                Dump(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer != null)
                {
                    if (EndsWithNewLine(buffer, index, count))
                    {
                        count -= Environment.NewLine.Length;
                    }

                    if (count > 0)
                    {
                        Dump(new string(buffer, index, count));
                    }
                }
            }

            private static bool EndsWithNewLine(char[] buffer, int index, int count)
            {
                var nl = Environment.NewLine;

                if (count < nl.Length)
                {
                    return false;
                }

                for (int i = nl.Length; i >= 1; --i)
                {
                    if (buffer[index + count - i] != nl[nl.Length - i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override void Write(char value)
            {
                Dump(value);
            }

            private void Dump(object? value)
            {
                _dumper.Dump(new DumpData(value, _header, DumpQuotas.Default));
            }
        }

        private class ConsoleReader : TextReader
        {
            private readonly TextReader _reader;
            private readonly JsonConsoleDumper _dumper;

            private string? _readString;
            private int _readPosition;

            public ConsoleReader(JsonConsoleDumper dumper)
            {
                _dumper = dumper;
                _reader = new StreamReader(Console.OpenStandardInput());
            }

            public override int Read()
            {
                if (_readString == null || _readPosition >= _readString.Length - 1)
                {
                    _dumper.DumpInputReadRequest();

                    _readString = _reader.ReadLine() + Environment.NewLine;
                    _readPosition = 0;
                }

                return _readString[_readPosition++];
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    _reader.Dispose();
                }
            }
        }
    }
}
