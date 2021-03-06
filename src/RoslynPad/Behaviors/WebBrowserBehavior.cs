namespace RoslynPad.Behaviors
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using System.Windows;

    using WebBrowser = System.Windows.Controls.WebBrowser;

    public sealed class WebBrowserBehavior
    {
        public static readonly DependencyProperty DumpObjectsProperty = DependencyProperty.RegisterAttached(
            "DumpObjects",
            typeof(ObservableCollection<IList<IDictionary<string, object>>>),
            typeof(WebBrowserBehavior),
            new FrameworkPropertyMetadata(OnDumpItemsChanged));

        [AttachedPropertyBrowsableForType(typeof(WebBrowser))]
        public static ObservableCollection<IList<IDictionary<string, object>>> GetDumpObjects(WebBrowser webBrowser)
        {
            return (ObservableCollection<IList<IDictionary<string, object>>>)webBrowser.GetValue(DumpObjectsProperty);
        }

        public static void SetDumpObjects(WebBrowser webBrowser, ObservableCollection<IList<IDictionary<string, object>>> value)
        {
            webBrowser.SetValue(DumpObjectsProperty, value);
        }

        private static void OnDumpItemsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is WebBrowser webBrowser)
            {
                var items = e.NewValue as ObservableCollection<IList<IDictionary<string, object>>> ?? new ObservableCollection<IList<IDictionary<string, object>>>();
                items.CollectionChanged += (sender, args) => ChangeHtml(webBrowser, (ObservableCollection<IList<IDictionary<string, object>>>)sender!);

                ChangeHtml(webBrowser, items);
            }
        }

        private static void ChangeHtml(WebBrowser webBrowser, IList<IList<IDictionary<string, object>>> items)
        {
            var htmlBuilder = new StringBuilder();
            using var stringWriter = new StringWriter(htmlBuilder);
            var xhtmlDumpHelper = new XhtmlDumpHelper(stringWriter);
            xhtmlDumpHelper.WriteBody(items);

            webBrowser.NavigateToString(htmlBuilder.ToString());
        }
    }
}
