namespace RoslynPad.UI
{
    using System;
    using System.ComponentModel;
    using System.Composition;
    using System.IO;
    using System.Runtime.InteropServices;

    using Newtonsoft.Json;

    using Microsoft.Extensions.Configuration;

    public interface IApplicationSettings : INotifyPropertyChanged
    {
        void LoadDefault();
        void LoadFrom(string path);
        string GetDefaultDocumentPath();

        bool EnableBraceCompletion { get; set; }
        string? WindowBounds { get; set; }
        string? DockLayout { get; set; }
        string? WindowState { get; set; }
        double EditorFontSize { get; set; }
        string? DocumentPath { get; set; }
        bool SearchFileContents { get; set; }
        bool SearchUsingRegex { get; set; }
        bool OptimizeCompilation { get; set; }
        bool SearchWhileTyping { get; set; }
        string DefaultPlatformName { get; set; }
        string EffectiveDocumentPath { get; }
        double? WindowFontSize { get; set; }
        bool FormatDocumentOnComment { get; set; }
    }

    [Export(typeof(IApplicationSettings)), Shared]
    internal class ApplicationSettings : NotificationObject, IApplicationSettings
    {
        private readonly IExceptionManager? _exceptionManager;

        private const int EditorFontSizeDefault = 12;
        private const string DefaultConfigFileName = "RoslynPad.json";

        private string? _path;

        private string? _windowBounds;
        private string? _dockLayout;
        private string? _windowState;
        private double _editorFontSize = EditorFontSizeDefault;
        private string? _documentPath;
        private string? _effectiveDocumentPath;
        private bool _searchFileContents;
        private bool _searchUsingRegex;
        private bool _optimizeCompilation;
        private bool _searchWhileTyping;
        private bool _enableBraceCompletion = true;
        private string _defaultPlatformName;
        private double? _windowFontSize;
        private bool _formatDocumentOnComment = true;

        private readonly IConfigurationRoot _configuration;

        [ImportingConstructor]
        public ApplicationSettings([Import(AllowDefault = true)] IExceptionManager exceptionManager)
        {
            _exceptionManager = exceptionManager;
            _defaultPlatformName = string.Empty;

            _configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
                .Build();
        }

        public void LoadDefault()
        {
            LoadFrom(Path.Combine(GetDefaultDocumentPath(), DefaultConfigFileName));
        }

        public void LoadFrom(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            LoadSettings(path);

            _path = path;
        }

        public bool EnableBraceCompletion
        {
            get => _enableBraceCompletion;
            set => SetProperty(ref _enableBraceCompletion, value);
        }

        public string? WindowBounds
        {
            get => _windowBounds;
            set => SetProperty(ref _windowBounds, value);
        }

        public string? DockLayout
        {
            get => _dockLayout;
            set => SetProperty(ref _dockLayout, value);
        }

        public string? WindowState
        {
            get => _windowState;
            set => SetProperty(ref _windowState, value);
        }

        public double EditorFontSize
        {
            get => _editorFontSize;
            set => SetProperty(ref _editorFontSize, value);
        }

        public string? DocumentPath
        {
            get => _documentPath;
            set => SetProperty(ref _documentPath, value);
        }

        public bool SearchFileContents
        {
            get => _searchFileContents;
            set => SetProperty(ref _searchFileContents, value);
        }

        public bool SearchUsingRegex
        {
            get => _searchUsingRegex;
            set => SetProperty(ref _searchUsingRegex, value);
        }

        public bool OptimizeCompilation
        {
            get => _optimizeCompilation;
            set => SetProperty(ref _optimizeCompilation, value);
        }

        public bool SearchWhileTyping
        {
            get => _searchWhileTyping;
            set => SetProperty(ref _searchWhileTyping, value);
        }

        public string DefaultPlatformName
        {
            get => _defaultPlatformName;
            set => SetProperty(ref _defaultPlatformName, value);
        }

        public double? WindowFontSize
        {
            get => _windowFontSize;
            set => SetProperty(ref _windowFontSize, value);
        }

        public bool FormatDocumentOnComment
        {
            get => _formatDocumentOnComment;
            set => SetProperty(ref _formatDocumentOnComment, value);
        }

        public string EffectiveDocumentPath
        {
            get
            {
                if (_effectiveDocumentPath == null)
                {

                    var userDefinedPath = DocumentPath;
                    _effectiveDocumentPath = !string.IsNullOrEmpty(userDefinedPath) && Directory.Exists(userDefinedPath)
                        ? userDefinedPath!
                        : GetDefaultDocumentPath();
                }

                return _effectiveDocumentPath;
            }
        }

        public string GetDefaultDocumentPath()
        {
            string? documentsPath = _configuration["DefaultDocumentPath"];
            if (!string.IsNullOrWhiteSpace(documentsPath))
            {
                return documentsPath;
            }

            documentsPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrEmpty(documentsPath))
            {
                documentsPath = "/";
                _exceptionManager?.ReportError(new InvalidOperationException("Unable to locate the user documents folder; Using root"));
            }

            return Path.Combine(documentsPath, "RoslynPad");
        }

        protected override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            SaveSettings();
        }

        private void LoadSettings(string path)
        {
            if (!File.Exists(path))
            {
                LoadDefaultSettings();
                return;
            }

            try
            {
                var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
                using var reader = File.OpenText(path);
                serializer.Populate(reader, this);
            }
            catch (Exception e)
            {
                LoadDefaultSettings();

                _exceptionManager?.ReportError(e);
            }
        }

        private void LoadDefaultSettings()
        {
            FormatDocumentOnComment = true;
            EditorFontSize = EditorFontSizeDefault;
        }

        private void SaveSettings()
        {
            if (_path == null) return;

            try
            {
                var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                using var writer = File.CreateText(_path);
                serializer.Serialize(writer, this);
            }
            catch (Exception e)
            {
                _exceptionManager?.ReportError(e);
            }
        }
    }
}
