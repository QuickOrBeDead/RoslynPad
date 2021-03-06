namespace RoslynPad.Behaviors
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Web.UI;

    public sealed class XhtmlDumpHelper
    {
        private readonly HtmlTextWriter _writer;

        public XhtmlDumpHelper(TextWriter writer)
        {
            _writer = new HtmlTextWriter(writer);
            InitHeader();
        }

        private void InitHeader()
        {
            _writer.WriteLineNoTabs("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">");
            _writer.AddAttribute("xmlns", "http://www.w3.org/1999/xhtml");
            _writer.AddAttribute("xml:lang", "en");
            _writer.RenderBeginTag(HtmlTextWriterTag.Html);
            _writer.RenderBeginTag(HtmlTextWriterTag.Head);

            _writer.RenderBeginTag(HtmlTextWriterTag.Title);
            _writer.Write("XhtmlDump");
            _writer.RenderEndTag();

            _writer.AddAttribute("http-equiv", "content-type");
            _writer.AddAttribute(HtmlTextWriterAttribute.Content, "text/html;charset=utf-8");
            _writer.RenderBeginTag(HtmlTextWriterTag.Meta);
            _writer.RenderEndTag();
            _writer.WriteLine();

            _writer.AddAttribute(HtmlTextWriterAttribute.Name, "generator");
            _writer.AddAttribute(HtmlTextWriterAttribute.Content, "XhtmlDump");
            _writer.RenderBeginTag(HtmlTextWriterTag.Meta);
            _writer.RenderEndTag();
            _writer.WriteLine();

            _writer.AddAttribute(HtmlTextWriterAttribute.Name, "description");
            _writer.AddAttribute(HtmlTextWriterAttribute.Content, "Generated on: " + DateTime.Now);
            _writer.RenderBeginTag(HtmlTextWriterTag.Meta);
            _writer.RenderEndTag();
            _writer.WriteLine();

            _writer.AddAttribute("type", "text/css");
            _writer.RenderBeginTag(HtmlTextWriterTag.Style);
            _writer.WriteLineNoTabs(GetStyleSheetContent());
            _writer.RenderEndTag(); // style

            _writer.RenderEndTag(); // Head
            _writer.WriteLine();

            _writer.RenderBeginTag(HtmlTextWriterTag.Body);
        }

        private static string GetStyleSheetContent()
        {
            var assembly = typeof(XhtmlDumpHelper).Assembly;
            var resourceStream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Behaviors.StyleSheet.css");

            if (resourceStream != null)
            {
                using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }

            return string.Empty;
        }

        public void WriteBody(IList<IList<IDictionary<string, object>>> dumpDictionaryLists)
        {
            for (var i = 0; i < dumpDictionaryLists.Count; i++)
            {
                _writer.AddAttribute(HtmlTextWriterAttribute.Id, $"table-{i}");
                _writer.RenderBeginTag(HtmlTextWriterTag.Table);

                var items = dumpDictionaryLists[i];

                _writer.RenderBeginTag(HtmlTextWriterTag.Tr);

                ICollection<string> columns = items.Count > 0 ? items[0].Keys : new List<string>(0);
                foreach (var columnName in columns)
                {
                    _writer.RenderBeginTag(HtmlTextWriterTag.Th);
                    _writer.Write(columnName);
                    _writer.RenderEndTag();
                }

                _writer.RenderEndTag(); // tr
                _writer.WriteLine();

                // write all the items
                foreach (IDictionary<string, object> item in items)
                {
                    _writer.RenderBeginTag(HtmlTextWriterTag.Tr);

                    foreach (var columnName in columns)
                    {
                        _writer.RenderBeginTag(HtmlTextWriterTag.Td);
                        RenderValue(item[columnName]);
                        _writer.RenderEndTag(); // td
                    }

                    _writer.RenderEndTag(); // tr
                    _writer.WriteLine();
                }

                // end the table
                _writer.RenderEndTag();

                _writer.WriteLine();
            }

            if (dumpDictionaryLists.Count > 0)
            {
                _writer.RenderBeginTag(HtmlTextWriterTag.Script);
                _writer.Write($"var el = document.getElementById('table-{dumpDictionaryLists.Count - 1}'); el.scrollIntoView();");
                _writer.RenderEndTag();
            }

            _writer.RenderEndTag(); // body
            _writer.RenderEndTag(); // html
        }

        private void RenderValue(object? value)
        {
            if (value == null)
            {
                _writer.Write("null");
                return;
            }

            Type valueType = value.GetType();
            if (valueType == typeof(double) ||
                valueType == typeof(decimal) ||
                valueType == typeof(float))
            {
                _writer.Write("{0:0.00}", value);
            }
            else
            {
                _writer.Write(value);
            }
        }
    }
}
