namespace RoslynPad
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    using Avalon.Windows.Controls;

    using ICSharpCode.AvalonEdit.Document;
    using ICSharpCode.AvalonEdit.Rendering;

    using Microsoft.CodeAnalysis.Text;

    using RoslynPad.Editor;
    using RoslynPad.Runtime;
    using RoslynPad.UI;

    public partial class DocumentView : IDisposable
    {
        public enum ConsoleTextType
        {
            Output = 0,
            Error = 1,
            Warning = 2,
            Information = 3
        }

        private readonly MarkerMargin _errorMargin;
        private OpenDocumentViewModel _viewModel;
        private IResultObject? _contextMenuResultObject;

        private static readonly Color ConsoleWarningColor = Color.FromArgb(255, 183, 122, 0);
        private static readonly Color ConsoleErrorColor = Color.FromArgb(255, 138, 6, 3);
        private static readonly Color ConsoleResultColor = Color.FromArgb(255, 78, 78, 78);
        private static readonly Color ConsoleInformationColor = Color.FromArgb(255, 0, 127, 0);

#pragma warning disable CS8618 // Non-nullable field is uninitialized.
        public DocumentView()
#pragma warning restore CS8618 // Non-nullable field is uninitialized.
        {
            InitializeComponent();

            _errorMargin = new MarkerMargin { Visibility = Visibility.Collapsed, MarkerImage = TryFindResource("ExceptionMarker") as ImageSource, Width = 10 };
            Editor.TextArea.LeftMargins.Insert(0, _errorMargin);
            Editor.PreviewMouseWheel += EditorOnPreviewMouseWheel;
            Editor.TextArea.Caret.PositionChanged += CaretOnPositionChanged;

            OutputText.Background = new SolidColorBrush(Colors.WhiteSmoke);
            OutputText.Document.UndoStack.SizeLimit = 0;

            ConsoleText.Background = new SolidColorBrush(Colors.WhiteSmoke);
            ConsoleText.Document.UndoStack.SizeLimit = 0;

            DataContextChanged += OnDataContextChanged;
        }

        private void CaretOnPositionChanged(object? sender, EventArgs eventArgs)
        {
            Ln.Text = Editor.TextArea.Caret.Line.ToString();
            Col.Text = Editor.TextArea.Caret.Column.ToString();
        }

        private void EditorOnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (_viewModel == null)
            {
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MainViewModel.EditorFontSize += args.Delta > 0 ? 1 : -1;
                args.Handled = true;
            }
        }

        private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            _viewModel = (OpenDocumentViewModel)args.NewValue;
            _viewModel.ResultsAvailable += ResultsAvailable;
            _viewModel.ReadInput += OnReadInput;
            _viewModel.NuGet.PackageInstalled += NuGetOnPackageInstalled;

            _viewModel.EditorFocus += (o, e) => Editor.Focus();
            _viewModel.DocumentUpdated += (o, e) => Dispatcher.InvokeAsync(() => Editor.RefreshHighlighting());
            _viewModel.OutputMessageReceived += s =>
            {
                ShowBottomPaneRow();

                BottomTabs.SelectedItem = OutputTab;
                OutputText.AppendText($"{DateTime.Now:dd/MM/yyyy HH:mm:ss.fff} - {s}{Environment.NewLine}");
                OutputText.ScrollToEnd();
            };
            _viewModel.ConsoleMessageReceived += (s, t) =>
            {
                ConsoleTextType consoleTextType;
                string separator;
                if (t == ConsoleMessageType.Err)
                {
                    separator = "!";
                    consoleTextType = ConsoleTextType.Error;
                }
                else
                {
                    consoleTextType = ConsoleTextType.Output;
                    separator = ">";
                }

                WriteConsole(s, consoleTextType, separator);
            };
            _viewModel.ExecutionStarted += s =>
            {
                _viewModel.Dispatcher.InvokeAsync(() => WriteConsole($"['{s}.csx' code Execution Started]", ConsoleTextType.Information, "-"));
            };
            _viewModel.BuildStarted += () => OutputText.Clear();

            _viewModel.MainViewModel.EditorFontSizeChanged += OnEditorFontSizeChanged;
            Editor.FontSize = _viewModel.MainViewModel.EditorFontSize;

            var documentText = await _viewModel.LoadText().ConfigureAwait(true);

            var documentId = Editor.Initialize(
                _viewModel.MainViewModel.RoslynHost,
                new ClassificationHighlightColors(),
                _viewModel.WorkingDirectory,
                documentText);

            _viewModel.Initialize(
                documentId,
                OnError,
                () => new TextSpan(Editor.SelectionStart, Editor.SelectionLength),
                this);

            Editor.Document.TextChanged += (o, e) => _viewModel.OnTextChanged();
        }

        private void OnReadInput()
        {
            var textBox = new TextBox();

            var dialog = new TaskDialog
            {
                Header = "Console Input",
                Content = textBox,
                Background = Brushes.White,
            };

            textBox.Loaded += (o, e) => textBox.Focus();

            textBox.KeyDown += (o, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    TaskDialog.CancelCommand.Execute(null, dialog);
                }
            };

            dialog.ShowInline(this);

            _viewModel.SendInput(textBox.Text);
        }

        public sealed class OffsetColorizer : DocumentColorizingTransformer
        {
            public OffsetColorizer(Color color)
            {
                Brush = new SolidColorBrush(color);
            }

            public int StartOffset { get; set; }
            public int EndOffset { get; set; }
            public Brush Brush { get; private set; }

            protected override void ColorizeLine(DocumentLine line)
            {
                if (line.Length == 0)
                    return;

                if (line.Offset < StartOffset || line.Offset > EndOffset)
                    return;

                int start = line.Offset > StartOffset ? line.Offset : StartOffset;
                int end = EndOffset > line.EndOffset ? line.EndOffset : EndOffset;

                ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(Brush));
            }
        }

        public void WriteConsole(string text, ConsoleTextType textType = ConsoleTextType.Output, string separator = ">")
        {
            ShowBottomPaneRow();

            BottomTabs.SelectedItem = ConsoleTab;

            var doc = ConsoleText.Document;
            var startOffset = doc.TextLength;
            doc.Insert(doc.TextLength, $"{DateTime.Now:dd/MM/yyyy HH:mm:ss.fff} {separator} {text}{Environment.NewLine}");
            var endOffset = doc.TextLength;

            var colorizer = new OffsetColorizer(GetColor(textType)) { StartOffset = startOffset, EndOffset = endOffset };
            ConsoleText.TextArea.TextView.LineTransformers.Add(colorizer);

            ConsoleText.ScrollToEnd();
        }

        private Color GetColor(ConsoleTextType textType)
        {
            switch (textType)
            {
                case ConsoleTextType.Warning:
                    return ConsoleWarningColor;
                case ConsoleTextType.Error:
                    return ConsoleErrorColor;
                case ConsoleTextType.Information:
                    return ConsoleInformationColor;
                default:
                    return ConsoleResultColor;
            }
        }

        private void ResultsAvailable(IResultObject o)
        {
            ShowBottomPaneRow();
            _viewModel.Dispatcher.InvokeAsync(
                () =>
                {
                    switch (o)
                    {
                        case CompilationErrorResultObject _:
                        case ExceptionResultObject _:
                            BottomTabs.SelectedItem = ErrorsTab;
                            break;
                        case DictionaryListResultObject _:
                            BottomTabs.SelectedItem = DumpTab;
                            break;
                    }
                },
                AppDispatcherPriority.Low);
        }

        private void ShowBottomPaneRow()
        {
            _viewModel.Dispatcher.InvokeAsync(
                () =>
                {
                    var height = BottomPaneRow.Height;
                    if (height.Value == 0)
                    {
                        BottomPaneRow.Height = new GridLength(1, GridUnitType.Star);
                    }
                },
                AppDispatcherPriority.Low);
        }

        private void OnError(ExceptionResultObject? e)
        {
            if (e != null)
            {
                _errorMargin.Visibility = Visibility.Visible;
                _errorMargin.LineNumber = e.LineNumber;
                _errorMargin.Message = "Exception: " + e.Message;
            }
            else
            {
                _errorMargin.Visibility = Visibility.Collapsed;
            }
        }

        private void OnEditorFontSizeChanged(double fontSize)
        {
            Editor.FontSize = fontSize;
        }

        private void NuGetOnPackageInstalled(PackageData package)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var text = $"#r \"nuget:{package.Id}/{package.Version}\"{Environment.NewLine}";
                Editor.Document.Insert(0, text, AnchorMovementType.Default);
            });
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                switch (e.Key)
                {
                    case Key.T:
                        e.Handled = true;
                        NuGetSearch.Focus();
                        break;
                }
            }
        }

        private void Editor_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() => Editor.Focus(), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void Dispose()
        {
            if (_viewModel?.MainViewModel != null)
            {
                _viewModel.MainViewModel.EditorFontSizeChanged -= OnEditorFontSizeChanged;
            }
        }

        private void OnTreeViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    CopyAllResultsToClipboard(withChildren: true);
                }
                else
                {
                    CopyToClipboard(e.OriginalSource);
                }
            }
            else if (e.Key == Key.Enter)
            {
                TryJumpToLine(e.OriginalSource);
            }
        }

        private void OnTreeViewDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TryJumpToLine(e.OriginalSource);
        }

        private void TryJumpToLine(object source)
        {
            var result = (source as FrameworkElement)?.DataContext as CompilationErrorResultObject;
            if (result == null)
            {
                return;
            }

            Editor.TextArea.Caret.Line = result.Line;
            Editor.TextArea.Caret.Column = result.Column;
            Editor.ScrollToLine(result.Line);

            Dispatcher.InvokeAsync(() => Editor.Focus());
        }

        private void CopyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            CopyToClipboard(e.OriginalSource);
        }

        private void CopyClick(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(sender);
        }

        private void CopyToClipboard(object sender)
        {
            var result = (sender as FrameworkElement)?.DataContext as IResultObject ??
                        _contextMenuResultObject;

            if (result != null)
            {
                Clipboard.SetText(ReferenceEquals(sender, CopyValueWithChildren) ? result.ToString() : result.Value);
            }
        }

        private void CopyAllClick(object sender, RoutedEventArgs e)
        {
            var withChildren = ReferenceEquals(sender, CopyAllValuesWithChildren);

            CopyAllResultsToClipboard(withChildren);
        }

        private void CopyAllResultsToClipboard(bool withChildren)
        {
            var builder = new StringBuilder();
            foreach (var result in _viewModel.ResultsInternal)
            {
                if (withChildren)
                {
                    result.WriteTo(builder);
                    builder.AppendLine();
                }
                else
                {
                    builder.AppendLine(result.Value);
                }
            }

            if (builder.Length > 0)
            {
                Clipboard.SetText(builder.ToString());
            }
        }

        private void ResultTree_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // keyboard-activated
            if (e.CursorLeft < 0 || e.CursorTop < 0)
            {
                _contextMenuResultObject = ResultTree.SelectedItem as IResultObject;
            }
            else
            {
                _contextMenuResultObject = (e.OriginalSource as FrameworkElement)?.DataContext as IResultObject;
            }

            var isResult = _contextMenuResultObject != null;
            CopyValue.IsEnabled = isResult;
            CopyValueWithChildren.IsEnabled = isResult;
        }

        private void SearchTerm_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _viewModel.NuGet.Packages?.Any() == true)
            {
                if (!_viewModel.NuGet.IsPackagesMenuOpen)
                {
                    _viewModel.NuGet.IsPackagesMenuOpen = true;
                }

                RootNuGetMenu.Focus();
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Editor.Focus();
            }
        }

        private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}
