using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ErAudioTool.Audio;

namespace ErAudioTool.UI
{
    public class BatchConversionItem
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
    }

    public class AudioConverterTab
    {
        // UI Controls
        private TextBox _inputFileTextBox;
        private TextBox _outputFileTextBox;
        private ComboBox _formatComboBox;
        private ComboBox _bitrateComboBox;
        private ComboBox _sampleRateComboBox;
        private Button _btnBrowseInput;
        private Button _btnBrowseOutput;
        private Button _btnConvert;
        private Button _btnManageCodecs;
        private TextBox _logTextBox;
        private ProgressBar _progressBar;
        private TextBlock _codecStatusText;
        private Border _codecStatusBorder;

        // Codec Management UI
        private Window _codecWindow;
        private Button _btnDownloadCodec;
        private Button _btnDeleteCodec;
        private TextBlock _codecSizeText;
        private ProgressBar _codecDownloadProgress;
        private TextBlock _codecDownloadStatusText;

        // Batch Conversion UI
        private ListBox _batchListBox;
        private Button _btnAddFiles;
        private Button _btnClearBatch;
        private Button _btnStartBatch;
        private List<BatchConversionItem> _batchItems = new List<BatchConversionItem>();

        private Window _parentWindow;
        private bool _isConverting = false;

        public AudioConverterTab(Window parentWindow)
        {
            _parentWindow = parentWindow;
        }

        public UIElement BuildUI()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16)
            };

            var mainStack = new StackPanel();

            // Codec Status Card
            mainStack.Children.Add(BuildCodecStatusCard());

            // Single File Conversion Card
            mainStack.Children.Add(BuildSingleConversionCard());

            // Batch Conversion Card
            mainStack.Children.Add(BuildBatchConversionCard());

            // Log Card
            mainStack.Children.Add(BuildLogCard());

            scrollViewer.Content = mainStack;
            return scrollViewer;
        }

        private Border BuildCodecStatusCard()
        {
            var stack = new StackPanel();

            var statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var statusLabel = new TextBlock
            {
                Text = "FFmpeg Status:",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(statusLabel);

            _codecStatusBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 12, 4)
            };

            _codecStatusText = new TextBlock
            {
                Text = "✗ Nicht installiert",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            };
            _codecStatusBorder.Child = _codecStatusText;
            leftStack.Children.Add(_codecStatusBorder);

            statusGrid.Children.Add(leftStack);

            _btnManageCodecs = CreateStyledButton("⚙️ Codecs verwalten", Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 34);
            _btnManageCodecs.Click += (s, e) => ShowCodecManagementWindow();
            Grid.SetColumn(_btnManageCodecs, 1);
            statusGrid.Children.Add(_btnManageCodecs);

            stack.Children.Add(statusGrid);

            UpdateCodecStatus();

            return CreateCard("CODEC-STATUS & VERWALTUNG", stack);
        }

        private Border BuildSingleConversionCard()
        {
            var stack = new StackPanel();

            // Input File
            var inLabel = new TextBlock
            {
                Text = "Eingangsdatei (WAV, MP3, FLAC, OGG, M4A, etc.):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(inLabel);

            var inGrid = new Grid();
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _inputFileTextBox = new TextBox
            {
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _inputFileTextBox.TextChanged += (s, e) => AutoFillOutputFile();
            inGrid.Children.Add(_inputFileTextBox);

            _btnBrowseInput = CreateStyledButton("📁 Durchsuchen...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _btnBrowseInput.Click += (s, e) => BrowseInputFile();
            Grid.SetColumn(_btnBrowseInput, 1);
            inGrid.Children.Add(_btnBrowseInput);

            stack.Children.Add(inGrid);

            // Output File
            var outLabel = new TextBlock
            {
                Text = "Ausgabedatei:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 10, 0, 6)
            };
            stack.Children.Add(outLabel);

            var outGrid = new Grid();
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _outputFileTextBox = new TextBox
            {
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            outGrid.Children.Add(_outputFileTextBox);

            _btnBrowseOutput = CreateStyledButton("💾 Speicherort...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _btnBrowseOutput.Click += (s, e) => BrowseOutputFile();
            Grid.SetColumn(_btnBrowseOutput, 1);
            outGrid.Children.Add(_btnBrowseOutput);

            stack.Children.Add(outGrid);

            // Format & Settings Grid
            var settingsGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Format Column
            var formatStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            var formatLabel = new TextBlock
            {
                Text = "Zielformat:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            formatStack.Children.Add(formatLabel);

            _formatComboBox = new ComboBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            foreach (AudioFormat format in Enum.GetValues(typeof(AudioFormat)))
            {
                _formatComboBox.Items.Add(format.ToString());
            }
            _formatComboBox.SelectedIndex = 0; // MP3
            _formatComboBox.SelectionChanged += (s, e) => AutoFillOutputFile();

            formatStack.Children.Add(_formatComboBox);
            settingsGrid.Children.Add(formatStack);

            // Bitrate Column
            var bitrateStack = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            var bitrateLabel = new TextBlock
            {
                Text = "Bitrate:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            bitrateStack.Children.Add(bitrateLabel);

            _bitrateComboBox = new ComboBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };
            _bitrateComboBox.Items.Add("128 kbps");
            _bitrateComboBox.Items.Add("192 kbps");
            _bitrateComboBox.Items.Add("256 kbps");
            _bitrateComboBox.Items.Add("320 kbps (High Quality)");
            _bitrateComboBox.SelectedIndex = 3; // 320 kbps
            bitrateStack.Children.Add(_bitrateComboBox);
            Grid.SetColumn(bitrateStack, 1);
            settingsGrid.Children.Add(bitrateStack);

            // Sample Rate Column
            var sampleStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            var sampleLabel = new TextBlock
            {
                Text = "Abtastrate:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            sampleStack.Children.Add(sampleLabel);

            _sampleRateComboBox = new ComboBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };
            _sampleRateComboBox.Items.Add("Original");
            _sampleRateComboBox.Items.Add("44100 Hz");
            _sampleRateComboBox.Items.Add("48000 Hz");
            _sampleRateComboBox.Items.Add("96000 Hz");
            _sampleRateComboBox.SelectedIndex = 0;
            sampleStack.Children.Add(_sampleRateComboBox);
            Grid.SetColumn(sampleStack, 2);
            settingsGrid.Children.Add(sampleStack);

            stack.Children.Add(settingsGrid);

            // Progress Bar
            _progressBar = new ProgressBar
            {
                Height = 8,
                Margin = new Thickness(0, 16, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                BorderThickness = new Thickness(0),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            stack.Children.Add(_progressBar);

            // Convert Button
            _btnConvert = CreateStyledButton("🔄 Jetzt konvertieren", Color.FromRgb(34, 197, 94), Color.FromRgb(22, 163, 74), 42, 240);
            _btnConvert.FontWeight = FontWeights.Bold;
            _btnConvert.FontSize = 13;
            _btnConvert.HorizontalAlignment = HorizontalAlignment.Center;
            _btnConvert.Margin = new Thickness(0, 14, 0, 0);
            _btnConvert.Click += (s, e) => StartConversion();
            stack.Children.Add(_btnConvert);

            return CreateCard("EINZEL-KONVERTIERUNG", stack);
        }

        private Border BuildBatchConversionCard()
        {
            var stack = new StackPanel();

            var btnGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnAddFiles = CreateStyledButton("➕ Dateien hinzufügen", Color.FromRgb(59, 130, 246), Color.FromRgb(37, 99, 235), 32);
            _btnAddFiles.Click += (s, e) => AddBatchFiles();
            btnGrid.Children.Add(_btnAddFiles);

            _btnClearBatch = CreateStyledButton("🗑️ Liste leeren", Color.FromRgb(239, 68, 68), Color.FromRgb(220, 38, 38), 32);
            _btnClearBatch.Margin = new Thickness(8, 0, 0, 0);
            _btnClearBatch.Click += (s, e) => ClearBatchList();
            Grid.SetColumn(_btnClearBatch, 1);
            btnGrid.Children.Add(_btnClearBatch);

            _btnStartBatch = CreateStyledButton("▶️ Alle konvertieren", Color.FromRgb(34, 197, 94), Color.FromRgb(22, 163, 74), 32);
            _btnStartBatch.Margin = new Thickness(8, 0, 0, 0);
            _btnStartBatch.Click += (s, e) => StartBatchConversion();
            Grid.SetColumn(_btnStartBatch, 3);
            btnGrid.Children.Add(_btnStartBatch);

            stack.Children.Add(btnGrid);

            _batchListBox = new ListBox
            {
                MinHeight = 120,
                MaxHeight = 200,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Foreground = Brushes.White
            };
            stack.Children.Add(_batchListBox);

            return CreateCard("BATCH-KONVERTIERUNG (MEHRERE DATEIEN)", stack);
        }

        private Border BuildLogCard()
        {
            var stack = new StackPanel();

            _logTextBox = new TextBox
            {
                Height = 140,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                Text = "Bereit für Konvertierung. Wählen Sie eine Audiodatei aus und klicken Sie auf 'Jetzt konvertieren'."
            };
            stack.Children.Add(_logTextBox);

            return CreateCard("KONVERTIERUNGS-PROTOKOLL", stack);
        }

        private void UpdateCodecStatus()
        {
            if (CodecManager.IsFfmpegInstalled())
            {
                _codecStatusText.Text = "✓ Installiert";
                _codecStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                _codecStatusBorder.Background = new SolidColorBrush(Color.FromArgb(50, 34, 197, 94));
                _codecStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                _codecStatusText.Text = "✗ Nicht installiert";
                _codecStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                _codecStatusBorder.Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68));
                _codecStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }

        private void ShowCodecManagementWindow()
        {
            if (_codecWindow != null && _codecWindow.IsVisible)
            {
                _codecWindow.Activate();
                return;
            }

            _codecWindow = new Window
            {
                Title = "Codec-Verwaltung - FFmpeg",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _parentWindow,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                ResizeMode = ResizeMode.NoResize
            };

            var mainStack = new StackPanel { Margin = new Thickness(20) };

            // Title
            var title = new TextBlock
            {
                Text = "FFmpeg Codec-Verwaltung",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainStack.Children.Add(title);

            var desc = new TextBlock
            {
                Text = "FFmpeg wird für die Audio-Konvertierung zwischen verschiedenen Formaten benötigt.\nDie Anwendung kann FFmpeg automatisch herunterladen und installieren.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(desc);

            // Status Section
            var statusCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var statusStack = new StackPanel();

            var statusLabel = new TextBlock
            {
                Text = "Status: " + (CodecManager.IsFfmpegInstalled() ? "✓ Installiert" : "✗ Nicht installiert"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = CodecManager.IsFfmpegInstalled() ?
                    new SolidColorBrush(Color.FromRgb(34, 197, 94)) :
                    new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            statusStack.Children.Add(statusLabel);

            _codecSizeText = new TextBlock
            {
                Text = "Installationsgröße: " + FormatBytes(CodecManager.GetCodecSize()),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            statusStack.Children.Add(_codecSizeText);

            var pathLabel = new TextBlock
            {
                Text = "Pfad: " + (CodecManager.GetFfmpegPath() ?? "Nicht gefunden"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            statusStack.Children.Add(pathLabel);

            statusCard.Child = statusStack;
            mainStack.Children.Add(statusCard);

            // Download Section
            var downloadCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var downloadStack = new StackPanel();

            _codecDownloadProgress = new ProgressBar
            {
                Height = 8,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                BorderThickness = new Thickness(0),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = Visibility.Collapsed
            };
            downloadStack.Children.Add(_codecDownloadProgress);

            _codecDownloadStatusText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            downloadStack.Children.Add(_codecDownloadStatusText);

            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnDownloadCodec = CreateStyledButton("📥 FFmpeg herunterladen (~50 MB)", Color.FromRgb(59, 130, 246), Color.FromRgb(37, 99, 235), 38);
            _btnDownloadCodec.HorizontalAlignment = HorizontalAlignment.Left;
            _btnDownloadCodec.Click += (s, e) => DownloadCodec();
            btnGrid.Children.Add(_btnDownloadCodec);

            _btnDeleteCodec = CreateStyledButton("🗑️ Codecs löschen", Color.FromRgb(239, 68, 68), Color.FromRgb(220, 38, 38), 38);
            _btnDeleteCodec.Click += (s, e) => DeleteCodec();
            Grid.SetColumn(_btnDeleteCodec, 1);
            btnGrid.Children.Add(_btnDeleteCodec);

            downloadStack.Children.Add(btnGrid);
            downloadCard.Child = downloadStack;
            mainStack.Children.Add(downloadCard);

            // Info
            var info = new TextBlock
            {
                Text = "ℹ️ FFmpeg ist ein Open-Source-Projekt unter GPL/LGPL Lizenz.\nQuelle: gyan.dev/ffmpeg (offizieller Windows-Build)",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(info);

            _codecWindow.Content = mainStack;
            _codecWindow.ShowDialog();
        }

        private void DownloadCodec()
        {
            _btnDownloadCodec.IsEnabled = false;
            _btnDeleteCodec.IsEnabled = false;
            _codecDownloadProgress.Visibility = Visibility.Visible;
            _codecDownloadStatusText.Visibility = Visibility.Visible;
            _codecDownloadStatusText.Text = "Download wird vorbereitet...";

            CodecManager.DownloadFfmpeg(
                progress =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _codecDownloadProgress.Value = progress;
                        _codecDownloadStatusText.Text = string.Format("Herunterladen... {0}%", progress);
                    });
                },
                (success, message) =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _btnDownloadCodec.IsEnabled = true;
                        _btnDeleteCodec.IsEnabled = true;
                        _codecDownloadProgress.Visibility = Visibility.Collapsed;
                        _codecDownloadStatusText.Text = message;

                        if (success)
                        {
                            _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            _codecSizeText.Text = "Installationsgröße: " + FormatBytes(CodecManager.GetCodecSize());
                            UpdateCodecStatus();
                            MessageBox.Show(_codecWindow, message, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            MessageBox.Show(_codecWindow, message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
            );
        }

        private void DeleteCodec()
        {
            var result = MessageBox.Show(_codecWindow,
                "Möchten Sie wirklich alle heruntergeladenen Codecs löschen?\nDies entfernt FFmpeg vollständig.",
                "Codecs löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (CodecManager.DeleteCodecs())
                {
                    _codecSizeText.Text = "Installationsgröße: 0 B";
                    UpdateCodecStatus();
                    MessageBox.Show(_codecWindow, "Codecs wurden erfolgreich gelöscht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(_codecWindow, "Fehler beim Löschen der Codecs.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BrowseInputFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Alle Audiodateien|*.wav;*.mp3;*.flac;*.ogg;*.aac;*.m4a;*.wma;*.opus;*.aiff;*.mid;*.midi|" +
                         "WAV Dateien|*.wav|" +
                         "MP3 Dateien|*.mp3|" +
                         "FLAC Dateien|*.flac|" +
                         "OGG Dateien|*.ogg|" +
                         "AAC/M4A Dateien|*.aac;*.m4a|" +
                         "MIDI Dateien|*.mid;*.midi|" +
                         "Alle Dateien (*.*)|*.*",
                Title = "Eingangsdatei auswählen"
            };

            if (dlg.ShowDialog() == true)
            {
                _inputFileTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseOutputFile()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Alle Dateien (*.*)|*.*",
                Title = "Ausgabedatei speichern unter"
            };

            if (!string.IsNullOrEmpty(_outputFileTextBox.Text))
            {
                try
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_outputFileTextBox.Text);
                    dlg.FileName = Path.GetFileName(_outputFileTextBox.Text);
                }
                catch { }
            }

            if (dlg.ShowDialog() == true)
            {
                _outputFileTextBox.Text = dlg.FileName;
            }
        }

        private void AutoFillOutputFile()
        {
            if (string.IsNullOrEmpty(_inputFileTextBox.Text)) return;
            if (!File.Exists(_inputFileTextBox.Text)) return;

            try
            {
                string dir = Path.GetDirectoryName(_inputFileTextBox.Text);
                string baseName = Path.GetFileNameWithoutExtension(_inputFileTextBox.Text);
                AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
                string ext = AudioConverterService.GetFormatExtension(format);
                _outputFileTextBox.Text = Path.Combine(dir, baseName + "_converted" + ext);
            }
            catch { }
        }

        private void StartConversion()
        {
            string inputFile = _inputFileTextBox.Text.Trim();
            string outputFile = _outputFileTextBox.Text.Trim();

            if (string.IsNullOrEmpty(inputFile) || !File.Exists(inputFile))
            {
                MessageBox.Show(_parentWindow, "Bitte wählen Sie eine gültige Eingangsdatei aus.", "Datei fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(outputFile))
            {
                MessageBox.Show(_parentWindow, "Bitte geben Sie eine Ausgabedatei an.", "Ausgabe fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!CodecManager.IsFfmpegInstalled())
            {
                MessageBox.Show(_parentWindow, "FFmpeg ist nicht installiert. Bitte installieren Sie FFmpeg über 'Codecs verwalten'.", "FFmpeg fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if input is MIDI file
            string ext = Path.GetExtension(inputFile).ToLowerInvariant();
            if (ext == ".mid" || ext == ".midi")
            {
                MessageBox.Show(_parentWindow,
                    "MIDI-Dateien können nicht direkt in Audio konvertiert werden.\n\n" +
                    "MIDI ist ein Notenformat, kein Audioformat. Um MIDI abzuspielen oder zu konvertieren:\n" +
                    "1. Nutzen Sie einen Software-Synthesizer (z.B. VirtualMIDISynth, FluidSynth)\n" +
                    "2. Oder importieren Sie die MIDI-Datei in eine DAW (Digital Audio Workstation)\n" +
                    "3. Rendern Sie dann das Audio und konvertieren Sie die WAV/MP3-Ausgabe",
                    "MIDI zu Audio nicht unterstützt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _isConverting = true;
            _btnConvert.IsEnabled = false;
            _btnBrowseInput.IsEnabled = false;
            _btnBrowseOutput.IsEnabled = false;
            _formatComboBox.IsEnabled = false;
            _bitrateComboBox.IsEnabled = false;
            _sampleRateComboBox.IsEnabled = false;
            _progressBar.Value = 0;
            _logTextBox.Clear();

            AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
            int bitrate = GetSelectedBitrate();
            int sampleRate = GetSelectedSampleRate();

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool success = AudioConverterService.ConvertAudioAdvanced(
                    inputFile,
                    outputFile,
                    format,
                    bitrate,
                    sampleRate,
                    msg =>
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _logTextBox.AppendText(string.Format("[{0}] {1}\n", DateTime.Now.ToString("HH:mm:ss"), msg));
                            _logTextBox.ScrollToEnd();
                        });
                    },
                    progress =>
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _progressBar.Value = progress;
                        });
                    }
                );

                _parentWindow.Dispatcher.InvokeAsync(() =>
                {
                    _isConverting = false;
                    _btnConvert.IsEnabled = true;
                    _btnBrowseInput.IsEnabled = true;
                    _btnBrowseOutput.IsEnabled = true;
                    _formatComboBox.IsEnabled = true;
                    _bitrateComboBox.IsEnabled = true;
                    _sampleRateComboBox.IsEnabled = true;

                    if (success)
                    {
                        var result = MessageBox.Show(_parentWindow,
                            "Konvertierung erfolgreich abgeschlossen!\n\nMöchten Sie die Datei im Explorer öffnen?",
                            "Erfolg",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + outputFile + "\"");
                        }
                    }
                });
            });
        }

        private void AddBatchFiles()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Alle Audiodateien|*.wav;*.mp3;*.flac;*.ogg;*.aac;*.m4a;*.wma;*.opus;*.aiff|" +
                         "WAV Dateien|*.wav|" +
                         "MP3 Dateien|*.mp3|" +
                         "FLAC Dateien|*.flac|" +
                         "OGG Dateien|*.ogg|" +
                         "AAC/M4A Dateien|*.aac;*.m4a|" +
                         "Alle Dateien (*.*)|*.*",
                Title = "Dateien für Batch-Konvertierung auswählen",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
                string ext = AudioConverterService.GetFormatExtension(format);

                int midiCount = 0;
                foreach (string file in dlg.FileNames)
                {
                    string fileExt = Path.GetExtension(file).ToLowerInvariant();
                    if (fileExt == ".mid" || fileExt == ".midi")
                    {
                        midiCount++;
                        continue; // Skip MIDI files
                    }

                    string dir = Path.GetDirectoryName(file);
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string outputFile = Path.Combine(dir, baseName + "_converted" + ext);

                    var item = new BatchConversionItem
                    {
                        InputFile = file,
                        OutputFile = outputFile,
                        Status = "Wartend...",
                        Progress = 0
                    };

                    _batchItems.Add(item);

                    var itemText = new TextBlock
                    {
                        Text = string.Format("{0} → {1}", Path.GetFileName(file), Path.GetFileName(outputFile)),
                        Foreground = Brushes.White,
                        FontSize = 11
                    };
                    _batchListBox.Items.Add(itemText);
                }

                int addedCount = dlg.FileNames.Length - midiCount;
                _logTextBox.AppendText(string.Format("[{0}] {1} Dateien zur Batch-Liste hinzugefügt.\n", DateTime.Now.ToString("HH:mm:ss"), addedCount));

                if (midiCount > 0)
                {
                    _logTextBox.AppendText(string.Format("[{0}] Warnung: {1} MIDI-Datei(en) übersprungen (MIDI zu Audio nicht unterstützt).\n",
                        DateTime.Now.ToString("HH:mm:ss"), midiCount));
                }
            }
        }

        private void ClearBatchList()
        {
            _batchItems.Clear();
            _batchListBox.Items.Clear();
            _logTextBox.AppendText(string.Format("[{0}] Batch-Liste geleert.\n", DateTime.Now.ToString("HH:mm:ss")));
        }

        private void StartBatchConversion()
        {
            if (_batchItems.Count == 0)
            {
                MessageBox.Show(_parentWindow, "Keine Dateien in der Batch-Liste.", "Leere Liste", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!CodecManager.IsFfmpegInstalled())
            {
                MessageBox.Show(_parentWindow, "FFmpeg ist nicht installiert. Bitte installieren Sie FFmpeg über 'Codecs verwalten'.", "FFmpeg fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _btnStartBatch.IsEnabled = false;
            _btnAddFiles.IsEnabled = false;
            _btnClearBatch.IsEnabled = false;

            AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
            int bitrate = GetSelectedBitrate();
            int sampleRate = GetSelectedSampleRate();

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int successCount = 0;
                int failCount = 0;

                for (int i = 0; i < _batchItems.Count; i++)
                {
                    var item = _batchItems[i];
                    int index = i;

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _logTextBox.AppendText(string.Format("\n[{0}] Konvertiere {1}/{2}: {3}\n",
                            DateTime.Now.ToString("HH:mm:ss"), index + 1, _batchItems.Count, Path.GetFileName(item.InputFile)));
                    });

                    bool success = AudioConverterService.ConvertAudioAdvanced(
                        item.InputFile,
                        item.OutputFile,
                        format,
                        bitrate,
                        sampleRate,
                        msg =>
                        {
                            _parentWindow.Dispatcher.InvokeAsync(() =>
                            {
                                _logTextBox.AppendText(string.Format("  {0}\n", msg));
                                _logTextBox.ScrollToEnd();
                            });
                        },
                        progress =>
                        {
                            _parentWindow.Dispatcher.InvokeAsync(() =>
                            {
                                _progressBar.Value = progress;
                            });
                        }
                    );

                    if (success) successCount++;
                    else failCount++;
                }

                _parentWindow.Dispatcher.InvokeAsync(() =>
                {
                    _btnStartBatch.IsEnabled = true;
                    _btnAddFiles.IsEnabled = true;
                    _btnClearBatch.IsEnabled = true;
                    _progressBar.Value = 0;

                    _logTextBox.AppendText(string.Format("\n[{0}] === Batch-Konvertierung abgeschlossen ===\n", DateTime.Now.ToString("HH:mm:ss")));
                    _logTextBox.AppendText(string.Format("✓ Erfolgreich: {0}\n", successCount));
                    _logTextBox.AppendText(string.Format("✗ Fehlgeschlagen: {0}\n", failCount));

                    MessageBox.Show(_parentWindow,
                        string.Format("Batch-Konvertierung abgeschlossen!\n\nErfolgreich: {0}\nFehlgeschlagen: {1}", successCount, failCount),
                        "Batch abgeschlossen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            });
        }

        private int GetSelectedBitrate()
        {
            switch (_bitrateComboBox.SelectedIndex)
            {
                case 0: return 128;
                case 1: return 192;
                case 2: return 256;
                case 3: return 320;
                default: return 320;
            }
        }

        private int GetSelectedSampleRate()
        {
            switch (_sampleRateComboBox.SelectedIndex)
            {
                case 0: return 0; // Original
                case 1: return 44100;
                case 2: return 48000;
                case 3: return 96000;
                default: return 0;
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        private Border CreateCard(string headerTitle, UIElement content)
        {
            var cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 14, 18, 16),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var stack = new StackPanel();
            var title = new TextBlock
            {
                Text = headerTitle,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(title);
            if (content != null)
            {
                stack.Children.Add(content);
            }

            cardBorder.Child = stack;
            return cardBorder;
        }

        private Button CreateStyledButton(string text, Color normalColor, Color hoverColor, double height, double width = 0)
        {
            var btn = new Button
            {
                Content = text,
                Height = height,
                Foreground = Brushes.White,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(14, 0, 14, 0)
            };
            if (width > 0) btn.Width = width;

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(normalColor));
            factory.SetValue(Border.PaddingProperty, new Thickness(12, 0, 12, 0));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentPresenter);

            var triggerIsMouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            triggerIsMouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "border"));

            var triggerIsEnabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            triggerIsEnabled.Setters.Add(new Setter(Border.OpacityProperty, 0.4, "border"));

            template.Triggers.Add(triggerIsMouseOver);
            template.Triggers.Add(triggerIsEnabled);
            template.VisualTree = factory;

            btn.Template = template;
            return btn;
        }
    }
}
