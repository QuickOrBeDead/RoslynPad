namespace RoslynPad
{
    using System;
    using System.ComponentModel;
    using System.Composition.Hosting;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Xml.Linq;

    using Avalon.Windows.Controls;

    using AvalonDock;
    using AvalonDock.Layout.Serialization;

    using RoslynPad.UI;
    using RoslynPad.Utilities;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainViewModelBase _viewModel;
        private readonly IAppDispatcher _dispatcher;

        private bool _isClosing;
        private bool _isClosed;

        public MainWindow()
        {
            Loaded += OnLoaded;

            var container = new ContainerConfiguration()
                .WithAssembly(typeof(MainViewModelBase).Assembly)   // RoslynPad.Common.UI
                .WithAssembly(typeof(MainWindow).Assembly);         // RoslynPad
            var locator = container.CreateContainer().GetExport<IServiceProvider>();

            _viewModel = locator.GetService<MainViewModelBase>();
            _dispatcher = locator.GetService<IAppDispatcher>();

            DataContext = _viewModel;
            InitializeComponent();
            DocumentsPane.ToggleAutoHide();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            await _viewModel.Initialize().ConfigureAwait(false);
            await _dispatcher.InvokeTaskAsync(LoadDockLayout).ConfigureAwait(false);

            _viewModel.DocumentsLoaded = true;

            await _dispatcher.InvokeTaskAsync(LoadWindowLayout).ConfigureAwait(false);
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!_isClosing)
            {
                SaveDockLayout();
                SaveWindowLayout();
                
                _isClosing = true;
                IsEnabled = false;
                e.Cancel = true;

                try
                {
                    await Task.Run(() => _viewModel.OnExit()).ConfigureAwait(true);
                }
                catch
                {
                    // ignored
                }

                _isClosed = true;
                // ReSharper disable once UnusedVariable
                var closeTask = Dispatcher.InvokeAsync(Close);
            }
            else
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                e.Cancel = !_isClosed;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
        }

        private void LoadWindowLayout()
        {
            var boundsString = _viewModel.Settings.WindowBounds;
            if (!string.IsNullOrEmpty(boundsString))
            {
                try
                {
                    var bounds = Rect.Parse(boundsString);
                    if (bounds != default)
                    {
                        Left = bounds.Left;
                        Top = bounds.Top;
                        Width = bounds.Width;
                        Height = bounds.Height;
                    }
                }
                catch (FormatException)
                {
                }
            }

            if (Enum.TryParse(_viewModel.Settings.WindowState, out WindowState state) &&
                state != WindowState.Minimized)
            {
                WindowState = state;
            }

            if (_viewModel.Settings.WindowFontSize.HasValue)
            {
                FontSize = _viewModel.Settings.WindowFontSize.Value;
            }
        }

        private void SaveWindowLayout()
        {
            _viewModel.Settings.WindowBounds = RestoreBounds.ToString(CultureInfo.InvariantCulture);
            _viewModel.Settings.WindowState = WindowState.ToString();
        }

        private void LoadDockLayout()
        {
            var layout = _viewModel.Settings.DockLayout;
            if (string.IsNullOrEmpty(layout))
            {
                return;
            }

            var layoutSerializer = new XmlLayoutSerializer(DockingManager);
            layoutSerializer.LayoutSerializationCallback += (s, e) =>
            {
                var contentId = e.Model.ContentId;

                if (string.IsNullOrEmpty(contentId) || e.Content != null)
                {
                    return;
                }

                if (File.Exists(contentId))
                {
                    var document = DocumentViewModel.FromPath(contentId);
                    _viewModel.OpenDocument(document);
                }

                e.Cancel = true;
            };

            var reader = new StringReader(layout);
            try
            {
                layoutSerializer.Deserialize(reader);
            }
            catch
            {
                // ignored
            }
        }

        private void SaveDockLayout()
        {
            var serializer = new XmlLayoutSerializer(DockingManager);
            var document = new XDocument();
            using (var writer = document.CreateWriter())
            {
                serializer.Serialize(writer);
            }

            document.Root?.Element("FloatingWindows")?.Remove();
            _viewModel.Settings.DockLayout = document.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Application.Current.Shutdown();
        }

        private async void DockingManager_OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            e.Cancel = true;
            var document = (OpenDocumentViewModel)e.Document.Content;
            await _viewModel.CloseDocument(document).ConfigureAwait(false);
        }

        private void ViewErrorDetails_OnClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.LastError == null)
            {
                return;
            }

            TaskDialog.ShowInline(
                this,
                "Unhandled Exception",
                _viewModel.LastError.ToAsyncString(),
                string.Empty,
                TaskDialogButtons.Close);
        }
    }
}
