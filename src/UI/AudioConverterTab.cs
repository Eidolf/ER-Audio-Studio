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
        private TextBlock _midiSynthStatusText;
        private Border _midiSynthStatusBorder;

        // Codec Management UI
        private Window _codecWindow;
        private Button _btnDownloadCodec;
        private Button _btnDownloadFluidSynth;
        private Button _btnDownloadSoundFont;
        private Button _btnDeleteCodec;
        private TextBlock _codecSizeText;
        private ProgressBar _codecDownloadProgress;
        private TextBlock _codecDownloadStatusText;
        private TextBlock _fsStatusLabel;
        private TextBlock _sfStatusLabel;

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

            // Codec & Synth Status Card
            mainStack.Children.Add(BuildCodecStatusCard());

            // Single File Conversion Card
            mainStack.Children.Add(BuildSingleConversionCard());

            // Batch Conversion Card
            mainStack.Children.Add(BuildBatchConversionCard());

            // Conversion Log Card
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

            // FFmpeg Status Badge
            var ffmpegLabel = new TextBlock
            {
                Text = "FFmpeg:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(ffmpegLabel);

            _codecStatusBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 16, 0)
            };

            _codecStatusText = new TextBlock
            {
                Text = "✗ Nicht installiert",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            };
            _codecStatusBorder.Child = _codecStatusText;
            leftStack.Children.Add(_codecStatusBorder);

            // MIDI Synth Status Badge
            var midiLabel = new TextBlock
            {
                Text = "MIDI Synthesizer:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(midiLabel);

            _midiSynthStatusBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 100, 116, 139)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3)
            };

            _midiSynthStatusText = new TextBlock
            {
                Text = "Standard (Sinus)",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            _midiSynthStatusBorder.Child = _midiSynthStatusText;
            leftStack.Children.Add(_midiSynthStatusBorder);

            statusGrid.Children.Add(leftStack);

            _btnManageCodecs = CreateStyledButton("⚙️ Downloads & Codecs verwalten", Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 34);
            _btnManageCodecs.Click += (s, e) => ShowCodecManagementWindow();
            Grid.SetColumn(_btnManageCodecs, 1);
            statusGrid.Children.Add(_btnManageCodecs);

            stack.Children.Add(statusGrid);

            UpdateCodecStatus();

            return CreateCard("CODEC- & SYNTHESIZER-STATUS", stack);
        }

        private Border BuildSingleConversionCard()
        {
            var stack = new StackPanel();

            // Input File
            var inLabel = new TextBlock
            {
                Text = "Eingangsdatei (WAV, MP3, FLAC, OGG, M4A, MIDI, etc.):",
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

            _btnBrowseOutput = CreateStyledButton("📁 Speichern unter...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
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
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
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
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
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
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
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

            return CreateCard("EINZELDATEI KONVERTIERUNG", stack);
        }

        private Border BuildBatchConversionCard()
        {
            var stack = new StackPanel();

            var desc = new TextBlock
            {
                Text = "Konvertieren Sie mehrere Dateien gleichzeitig in das ausgewählte Format.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(desc);

            _batchListBox = new ListBox
            {
                Height = 110,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(_batchListBox);

            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnAddFiles = CreateStyledButton("➕ Dateien hinzufügen", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _btnAddFiles.Click += (s, e) => AddBatchFiles();
            btnGrid.Children.Add(_btnAddFiles);

            _btnClearBatch = CreateStyledButton("🗑️ Liste leeren", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _btnClearBatch.Margin = new Thickness(8, 0, 0, 0);
            _btnClearBatch.Click += (s, e) => ClearBatchList();
            Grid.SetColumn(_btnClearBatch, 1);
            btnGrid.Children.Add(_btnClearBatch);

            _btnStartBatch = CreateStyledButton("▶ Batch-Konvertierung starten", Color.FromRgb(37, 99, 235), Color.FromRgb(29, 78, 216), 34, 210);
            _btnStartBatch.FontWeight = FontWeights.Bold;
            _btnStartBatch.Click += (s, e) => StartBatchConversion();
            Grid.SetColumn(_btnStartBatch, 3);
            btnGrid.Children.Add(_btnStartBatch);

            stack.Children.Add(btnGrid);

            return CreateCard("STAPELVERARBEITUNG (BATCH)", stack);
        }

        private Border BuildLogCard()
        {
            var stack = new StackPanel();

            _logTextBox = new TextBox
            {
                Height = 110,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                Text = "Bereit für Konvertierung. Wählen Sie eine Audiodatei aus und klicken Sie auf 'Jetzt konvertieren'."
            };
            stack.Children.Add(_logTextBox);

            return CreateCard("KONVERTIERUNGS-PROTOKOLL", stack);
        }

        private void UpdateCodecStatus()
        {
            // FFmpeg Status
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

            // MIDI Synth Status
            if (CodecManager.IsFluidSynthInstalled())
            {
                if (CodecManager.HasHighQualitySoundFont())
                {
                    _midiSynthStatusText.Text = "✓ Studio HQ (SoundFont)";
                    _midiSynthStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    _midiSynthStatusBorder.Background = new SolidColorBrush(Color.FromArgb(50, 34, 197, 94));
                    _midiSynthStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else
                {
                    _midiSynthStatusText.Text = "✓ FluidSynth (GM.DLS)";
                    _midiSynthStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    _midiSynthStatusBorder.Background = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248));
                    _midiSynthStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                }
            }
            else
            {
                _midiSynthStatusText.Text = "Standard (Sinuswellen)";
                _midiSynthStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                _midiSynthStatusBorder.Background = new SolidColorBrush(Color.FromArgb(50, 100, 116, 139));
                _midiSynthStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }

        public void ShowCodecManagementWindow()
        {
            if (_codecWindow != null && _codecWindow.IsVisible)
            {
                _codecWindow.Activate();
                return;
            }

            _codecWindow = new Window
            {
                Title = "Downloads & Codec-Verwaltung - ER Audio Studio",
                Width = 660,
                Height = 680,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _parentWindow,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                ResizeMode = ResizeMode.NoResize
            };

            var mainStack = new StackPanel { Margin = new Thickness(20) };

            // Title
            var title = new TextBlock
            {
                Text = "Komponenten & Codec-Verwaltung",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            };
            mainStack.Children.Add(title);

            var desc = new TextBlock
            {
                Text = "Laden Sie optionale Audiokomponenten für beste Konvertierungsqualität und MIDI-Wiedergabe herunter.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(desc);

            // Progress & Status (global for window)
            _codecDownloadProgress = new ProgressBar
            {
                Height = 8,
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                BorderThickness = new Thickness(0),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_codecDownloadProgress);

            _codecDownloadStatusText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_codecDownloadStatusText);

            // 1. FFmpeg Card
            var ffmpegCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var fStack = new StackPanel();
            var fGrid = new Grid();
            fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fInfo = new StackPanel();
            var fTitle = new TextBlock { Text = "1. FFmpeg Audio-Engine (Konverter)", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var fDesc = new TextBlock { Text = "Ermöglicht Konvertierung aller Formate (MP3, FLAC, OGG, AAC, etc.)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) };
            var fStatus = new TextBlock
            {
                Text = "Status: " + (CodecManager.IsFfmpegInstalled() ? "✓ Installiert (" + (CodecManager.GetFfmpegPath() ?? "") + ")" : "✗ Nicht installiert"),
                FontSize = 11,
                Foreground = CodecManager.IsFfmpegInstalled() ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            fInfo.Children.Add(fTitle);
            fInfo.Children.Add(fDesc);
            fInfo.Children.Add(fStatus);
            fGrid.Children.Add(fInfo);

            _btnDownloadCodec = CreateStyledButton(CodecManager.IsFfmpegInstalled() ? "Erneut laden" : "📥 FFmpeg laden (~40 MB)", Color.FromRgb(59, 130, 246), Color.FromRgb(37, 99, 235), 32);
            _btnDownloadCodec.Click += (s, e) => DownloadCodec();
            Grid.SetColumn(_btnDownloadCodec, 1);
            fGrid.Children.Add(_btnDownloadCodec);
            fStack.Children.Add(fGrid);
            ffmpegCard.Child = fStack;
            mainStack.Children.Add(ffmpegCard);

            // 2. FluidSynth Card
            var fsCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var fsStack = new StackPanel();
            var fsGrid = new Grid();
            fsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fsInfo = new StackPanel();
            var fsTitle = new TextBlock { Text = "2. FluidSynth Synthesizer (MIDI zu Audio)", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var fsDesc = new TextBlock { Text = "Wandelt MIDI in WAV um – wahlweise mit Windows GM oder SoundFonts", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) };
            _fsStatusLabel = new TextBlock
            {
                Text = "Status: " + (CodecManager.IsFluidSynthInstalled() ? "✓ Installiert (" + (CodecManager.GetFluidSynthPath() ?? "") + ")" : "✗ Nicht installiert"),
                FontSize = 11,
                Foreground = CodecManager.IsFluidSynthInstalled() ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            fsInfo.Children.Add(fsTitle);
            fsInfo.Children.Add(fsDesc);
            fsInfo.Children.Add(_fsStatusLabel);
            fsGrid.Children.Add(fsInfo);

            _btnDownloadFluidSynth = CreateStyledButton(CodecManager.IsFluidSynthInstalled() ? "Erneut laden" : "📥 FluidSynth laden (~2.7 MB)", Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 32);
            _btnDownloadFluidSynth.Click += (s, e) => DownloadFluidSynth();
            Grid.SetColumn(_btnDownloadFluidSynth, 1);
            fsGrid.Children.Add(_btnDownloadFluidSynth);
            fsStack.Children.Add(fsGrid);
            fsCard.Child = fsStack;
            mainStack.Children.Add(fsCard);

            // 3. SoundFont Card
            var sfCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var sfStack = new StackPanel();
            var sfGrid = new Grid();
            sfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sfInfo = new StackPanel();
            var sfTitle = new TextBlock { Text = "3. FluidR3 GM SoundFont (Echte Orchester- & Studio-Samples)", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var sfDesc = new TextBlock { Text = "Ersetzt Sinuswellen durch 128 echte akustische & elektronische GM-Instrumente", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) };
            _sfStatusLabel = new TextBlock
            {
                Text = "Status: " + (CodecManager.HasHighQualitySoundFont() ? "✓ Installiert (" + Path.GetFileName(CodecManager.GetSoundFontPath()) + ")" : (CodecManager.HasSoundFont() ? "ℹ Windows Standard GM.DLS aktiv" : "✗ Nicht installiert")),
                FontSize = 11,
                Foreground = CodecManager.HasHighQualitySoundFont() ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            sfInfo.Children.Add(sfTitle);
            sfInfo.Children.Add(sfDesc);
            sfInfo.Children.Add(_sfStatusLabel);
            sfGrid.Children.Add(sfInfo);

            _btnDownloadSoundFont = CreateStyledButton(CodecManager.HasHighQualitySoundFont() ? "Erneut laden" : "📥 SoundFont laden (~140 MB)", Color.FromRgb(139, 92, 246), Color.FromRgb(124, 58, 237), 32);
            _btnDownloadSoundFont.Click += (s, e) => DownloadSoundFont();
            Grid.SetColumn(_btnDownloadSoundFont, 1);
            sfGrid.Children.Add(_btnDownloadSoundFont);
            sfStack.Children.Add(sfGrid);
            sfCard.Child = sfStack;
            mainStack.Children.Add(sfCard);

            // 4. AI Pitch Detection Card (Basic-Pitch / Aubio)
            var aiCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var aiStack = new StackPanel();
            var aiGrid = new Grid();
            aiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            aiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var aiInfo = new StackPanel();
            var aiTitle = new TextBlock { Text = "4. KI Polyphonie-Erkennung (Basic-Pitch / Aubio)", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            var aiDesc = new TextBlock { Text = "Neuronales Netzwerk fÃ¼r komplexe mehrstimmige Melodien (ohne CLI: nativer YIN-Algorithmus)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 2, 0, 0) };
            
            string currentCli = AudioToMidiConverter.FindExternalMidiCli();
            var aiStatus = new TextBlock
            {
                Text = "Status: " + (!string.IsNullOrEmpty(currentCli) ? "âœ“ Aktiv (" + Path.GetFileName(currentCli) + ")" : "â„¹ Integrierter YIN-Algorithmus aktiv (Standard)"),
                FontSize = 11,
                Foreground = !string.IsNullOrEmpty(currentCli) ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            aiInfo.Children.Add(aiTitle);
            aiInfo.Children.Add(aiDesc);
            aiInfo.Children.Add(aiStatus);
            aiGrid.Children.Add(aiInfo);

            var btnOpenCodecsFolder = CreateStyledButton("ðŸ“ codecs-Ordner", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            btnOpenCodecsFolder.ToolTip = "Ã–ffnet den codecs-Ordner fÃ¼r manuelle CLI-Dateien oder Erweiterungen";
            btnOpenCodecsFolder.Click += (s, e) =>
            {
                try
                {
                    string cDir = CodecManager.GetCodecDirectory();
                    System.Diagnostics.Process.Start("explorer.exe", cDir);
                }
                catch { }
            };
            Grid.SetColumn(btnOpenCodecsFolder, 1);
            aiGrid.Children.Add(btnOpenCodecsFolder);
            aiStack.Children.Add(aiGrid);
            aiCard.Child = aiStack;
            mainStack.Children.Add(aiCard);

            // Bottom bar: Size + Delete button
            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _codecSizeText = new TextBlock
            {
                Text = "Gesamtgröße im codecs-Ordner: " + FormatBytes(CodecManager.GetCodecSize()),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            bottomGrid.Children.Add(_codecSizeText);

            _btnDeleteCodec = CreateStyledButton("🗑️ Alle Downloads löschen", Color.FromRgb(239, 68, 68), Color.FromRgb(220, 38, 38), 34);
            _btnDeleteCodec.Click += (s, e) => DeleteCodec();
            Grid.SetColumn(_btnDeleteCodec, 1);
            bottomGrid.Children.Add(_btnDeleteCodec);

            mainStack.Children.Add(bottomGrid);

            _codecWindow.Content = mainStack;
            _codecWindow.ShowDialog();
        }

        private void DownloadCodec()
        {
            SetDownloadUiState(true, "FFmpeg Download wird vorbereitet...");

            CodecManager.DownloadFfmpeg(
                progress =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _codecDownloadProgress.Value = progress;
                        _codecDownloadStatusText.Text = string.Format("FFmpeg wird heruntergeladen... {0}%", progress);
                    });
                },
                (success, message) =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        SetDownloadUiState(false, message);
                        if (success)
                        {
                            _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            _codecSizeText.Text = "Gesamtgröße im codecs-Ordner: " + FormatBytes(CodecManager.GetCodecSize());
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

        private void DownloadFluidSynth()
        {
            SetDownloadUiState(true, "FluidSynth Download wird vorbereitet...");

            CodecManager.DownloadFluidSynth(
                progress =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _codecDownloadProgress.Value = progress;
                        _codecDownloadStatusText.Text = string.Format("FluidSynth wird heruntergeladen... {0}%", progress);
                    });
                },
                (success, message) =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        SetDownloadUiState(false, message);
                        if (success)
                        {
                            _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            if (_fsStatusLabel != null)
                            {
                                _fsStatusLabel.Text = "Status: ✓ Installiert (" + (CodecManager.GetFluidSynthPath() ?? "") + ")";
                                _fsStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            }
                            _codecSizeText.Text = "Gesamtgröße im codecs-Ordner: " + FormatBytes(CodecManager.GetCodecSize());
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

        private void DownloadSoundFont()
        {
            SetDownloadUiState(true, "SoundFont Download wird vorbereitet (~140 MB)...");

            CodecManager.DownloadSoundFont(
                progress =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _codecDownloadProgress.Value = progress;
                        _codecDownloadStatusText.Text = string.Format("FluidR3 GM SoundFont wird geladen... {0}%", progress);
                    });
                },
                (success, message) =>
                {
                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        SetDownloadUiState(false, message);
                        if (success)
                        {
                            _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            if (_sfStatusLabel != null)
                            {
                                _sfStatusLabel.Text = "Status: ✓ Installiert (FluidR3_GM.sf2)";
                                _sfStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                            }
                            _codecSizeText.Text = "Gesamtgröße im codecs-Ordner: " + FormatBytes(CodecManager.GetCodecSize());
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

        private void SetDownloadUiState(bool isDownloading, string status)
        {
            if (_btnDownloadCodec != null) _btnDownloadCodec.IsEnabled = !isDownloading;
            if (_btnDownloadFluidSynth != null) _btnDownloadFluidSynth.IsEnabled = !isDownloading;
            if (_btnDownloadSoundFont != null) _btnDownloadSoundFont.IsEnabled = !isDownloading;
            if (_btnDeleteCodec != null) _btnDeleteCodec.IsEnabled = !isDownloading;

            if (_codecDownloadProgress != null)
            {
                _codecDownloadProgress.Visibility = isDownloading ? Visibility.Visible : Visibility.Collapsed;
                if (isDownloading) _codecDownloadProgress.Value = 0;
            }

            if (_codecDownloadStatusText != null)
            {
                _codecDownloadStatusText.Visibility = Visibility.Visible;
                _codecDownloadStatusText.Text = status;
                _codecDownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }

        private void DeleteCodec()
        {
            var result = MessageBox.Show(_codecWindow ?? _parentWindow,
                "Möchten Sie wirklich alle heruntergeladenen Komponenten (FFmpeg, FluidSynth, SoundFonts) löschen?",
                "Komponenten löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (CodecManager.DeleteCodecs())
                {
                    if (_codecSizeText != null) _codecSizeText.Text = "Gesamtgröße im codecs-Ordner: 0 B";
                    if (_fsStatusLabel != null)
                    {
                        _fsStatusLabel.Text = "Status: ✗ Nicht installiert";
                        _fsStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    }
                    if (_sfStatusLabel != null)
                    {
                        _sfStatusLabel.Text = "Status: ℹ Windows GM.DLS Fallback";
                        _sfStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    }
                    UpdateCodecStatus();
                    MessageBox.Show(_codecWindow ?? _parentWindow, "Alle Komponenten wurden erfolgreich gelöscht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(_codecWindow ?? _parentWindow, "Fehler beim Löschen der Komponenten.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
                AutoFillOutputFile();
            }
        }

        public void SetInputFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (_inputFileTextBox != null)
            {
                _inputFileTextBox.Text = filePath;
                AutoFillOutputFile();
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
                var r = MessageBox.Show(_parentWindow,
                    "FFmpeg ist noch nicht installiert!\n\n" +
                    "Möchten Sie den Download-Manager öffnen, um FFmpeg automatisch zu installieren?",
                    "FFmpeg fehlt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes)
                {
                    ShowCodecManagementWindow();
                }
                return;
            }

            AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
            int bitrate = GetSelectedBitrate();
            int sampleRate = GetSelectedSampleRate();

            // Check if input is MIDI file
            string ext = Path.GetExtension(inputFile).ToLowerInvariant();
            if (ext == ".mid" || ext == ".midi")
            {
                bool hasHq = CodecManager.IsHighQualityMidiAvailable();
                if (!hasHq)
                {
                    // Offer HQ Download dialog
                    bool shouldContinue = ShowMidiQualityDialog();
                    if (!shouldContinue) return;
                }
                else
                {
                    string sf = Path.GetFileName(CodecManager.GetSoundFontPath());
                    _logTextBox.AppendText(string.Format("[{0}] Verwende Studio-Synthesizer: FluidSynth + {1}\n", DateTime.Now.ToString("HH:mm:ss"), sf));
                }

                // Start MIDI conversion in background
                _isConverting = true;
                _btnConvert.IsEnabled = false;
                _btnBrowseInput.IsEnabled = false;
                _btnBrowseOutput.IsEnabled = false;
                _formatComboBox.IsEnabled = false;
                _bitrateComboBox.IsEnabled = false;
                _sampleRateComboBox.IsEnabled = false;
                _progressBar.Value = 0;
                _logTextBox.Clear();

                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    // Step 1: MIDI to WAV
                    string tempWav = Path.Combine(Path.GetTempPath(), "midi_synth_temp_" + Guid.NewGuid().ToString() + ".wav");

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _logTextBox.AppendText(string.Format("[{0}] Schritt 1/2: MIDI zu WAV Synthese...\n", DateTime.Now.ToString("HH:mm:ss")));
                        _progressBar.Value = 10;
                    });

                    bool synthSuccess = MidiSynthesizer.ConvertMidiToWav(inputFile, tempWav, msg =>
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _logTextBox.AppendText(string.Format("[{0}] {1}\n", DateTime.Now.ToString("HH:mm:ss"), msg));
                            _logTextBox.ScrollToEnd();
                        });
                    });

                    if (!synthSuccess)
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _isConverting = false;
                            _btnConvert.IsEnabled = true;
                            _btnBrowseInput.IsEnabled = true;
                            _btnBrowseOutput.IsEnabled = true;
                            _formatComboBox.IsEnabled = true;
                            _bitrateComboBox.IsEnabled = true;
                            _sampleRateComboBox.IsEnabled = true;
                            _logTextBox.AppendText(string.Format("[{0}] ✗ MIDI-Synthese fehlgeschlagen.\n", DateTime.Now.ToString("HH:mm:ss")));
                        });
                        return;
                    }

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _progressBar.Value = 50;
                    });

                    // Step 2: WAV to target format (if not WAV)
                    bool finalSuccess = true;
                    if (format != AudioFormat.WAV)
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _logTextBox.AppendText(string.Format("[{0}] Schritt 2/2: WAV zu {1} Konvertierung...\n",
                                DateTime.Now.ToString("HH:mm:ss"), format.ToString()));
                        });

                        finalSuccess = AudioConverterService.ConvertAudioAdvanced(
                            tempWav,
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
                            prog =>
                            {
                                _parentWindow.Dispatcher.InvokeAsync(() =>
                                {
                                    _progressBar.Value = 50 + (prog / 2);
                                });
                            }
                        );

                        try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
                    }
                    else
                    {
                        // Target is WAV - just move/copy the temp file
                        try
                        {
                            if (File.Exists(outputFile)) File.Delete(outputFile);
                            File.Move(tempWav, outputFile);
                        }
                        catch (Exception ex)
                        {
                            finalSuccess = false;
                            _parentWindow.Dispatcher.InvokeAsync(() =>
                            {
                                _logTextBox.AppendText(string.Format("[{0}] Fehler beim Speichern: {1}\n", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                            });
                        }
                    }

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _isConverting = false;
                        _btnConvert.IsEnabled = true;
                        _btnBrowseInput.IsEnabled = true;
                        _btnBrowseOutput.IsEnabled = true;
                        _formatComboBox.IsEnabled = true;
                        _bitrateComboBox.IsEnabled = true;
                        _sampleRateComboBox.IsEnabled = true;

                        if (finalSuccess)
                        {
                            _progressBar.Value = 100;
                            _logTextBox.AppendText(string.Format("[{0}] ✓ Konvertierung erfolgreich abgeschlossen!\n", DateTime.Now.ToString("HH:mm:ss")));
                            _logTextBox.AppendText(string.Format("[{0}] Zieldatei: {1}\n", DateTime.Now.ToString("HH:mm:ss"), outputFile));

                            var msgResult = MessageBox.Show(_parentWindow,
                                "Konvertierung erfolgreich abgeschlossen!\n\nMöchten Sie den Ordner im Explorer öffnen?",
                                "Erfolg",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (msgResult == MessageBoxResult.Yes)
                            {
                                OpenFolderAndSelectFile(outputFile);
                            }
                        }
                        else
                        {
                            _progressBar.Value = 0;
                            _logTextBox.AppendText(string.Format("[{0}] ✗ Konvertierung fehlgeschlagen.\n", DateTime.Now.ToString("HH:mm:ss")));
                        }
                    });
                });

                return;
            }

            // Normal audio conversion (non-MIDI)
            _isConverting = true;
            _btnConvert.IsEnabled = false;
            _btnBrowseInput.IsEnabled = false;
            _btnBrowseOutput.IsEnabled = false;
            _formatComboBox.IsEnabled = false;
            _bitrateComboBox.IsEnabled = false;
            _sampleRateComboBox.IsEnabled = false;
            _progressBar.Value = 0;
            _logTextBox.Clear();

            _logTextBox.AppendText(string.Format("[{0}] Starte Konvertierung: {1} -> {2}\n",
                DateTime.Now.ToString("HH:mm:ss"), Path.GetFileName(inputFile), Path.GetFileName(outputFile)));

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
                    prog =>
                    {
                        _parentWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _progressBar.Value = prog;
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
                        _progressBar.Value = 100;
                        _logTextBox.AppendText(string.Format("[{0}] ✓ Konvertierung erfolgreich abgeschlossen!\n", DateTime.Now.ToString("HH:mm:ss")));
                        _logTextBox.AppendText(string.Format("[{0}] Zieldatei: {1}\n", DateTime.Now.ToString("HH:mm:ss"), outputFile));

                        var result = MessageBox.Show(_parentWindow,
                            "Konvertierung erfolgreich abgeschlossen!\n\nMöchten Sie den Ordner im Explorer öffnen?",
                            "Erfolg",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            OpenFolderAndSelectFile(outputFile);
                        }
                    }
                    else
                    {
                        _progressBar.Value = 0;
                        _logTextBox.AppendText(string.Format("[{0}] ✗ Konvertierung fehlgeschlagen.\n", DateTime.Now.ToString("HH:mm:ss")));
                    }
                });
            });
        }

        private bool ShowMidiQualityDialog()
        {
            var win = new Window
            {
                Title = "MIDI zu Audio Konvertierung - Qualitätsoptionen",
                Width = 540,
                Height = 310,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _parentWindow,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                ResizeMode = ResizeMode.NoResize
            };

            bool proceed = false;

            var stack = new StackPanel { Margin = new Thickness(22) };

            var title = new TextBlock
            {
                Text = "🎵 MIDI-Datei erkannt",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(title);

            var text = new TextBlock
            {
                Text = "Für erstklassigen Studio-Klang mit echten Instrumenten-Samples können Sie die kostenlosen Komponenten (FluidSynth Synthesizer + Orchester SoundFont) mit 1 Klick herunterladen.\n\nAlternativ kann die Konvertierung sofort mit dem integrierten Standard-Synthesizer fortgesetzt werden.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 18),
                LineHeight = 18
            };
            stack.Children.Add(text);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnDownload = CreateStyledButton("📥 Bessere Qualität herunterladen...", Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 36);
            btnDownload.FontWeight = FontWeights.Bold;
            btnDownload.Margin = new Thickness(0, 0, 10, 0);
            btnDownload.Click += (s, e) =>
            {
                win.Close();
                ShowCodecManagementWindow();
            };
            btnStack.Children.Add(btnDownload);

            var btnStandard = CreateStyledButton("Mit Standard fortfahren", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            btnStandard.Margin = new Thickness(0, 0, 10, 0);
            btnStandard.Click += (s, e) =>
            {
                proceed = true;
                win.Close();
            };
            btnStack.Children.Add(btnStandard);

            var btnCancel = CreateStyledButton("Abbrechen", Color.FromRgb(71, 85, 105), Color.FromRgb(100, 116, 139), 36);
            btnCancel.Click += (s, e) =>
            {
                proceed = false;
                win.Close();
            };
            btnStack.Children.Add(btnCancel);

            stack.Children.Add(btnStack);
            win.Content = stack;
            win.ShowDialog();

            return proceed;
        }

        private void AddBatchFiles()
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
                Title = "Dateien für Batch-Konvertierung auswählen",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
                string ext = AudioConverterService.GetFormatExtension(format);

                foreach (string file in dlg.FileNames)
                {
                    string dir = Path.GetDirectoryName(file);
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string outPath = Path.Combine(dir, baseName + "_converted" + ext);

                    var item = new BatchConversionItem
                    {
                        InputFile = file,
                        OutputFile = outPath,
                        Status = "Bereit",
                        Progress = 0
                    };
                    _batchItems.Add(item);
                    _batchListBox.Items.Add(Path.GetFileName(file) + " -> " + Path.GetFileName(outPath));
                }
            }
        }

        private void ClearBatchList()
        {
            _batchItems.Clear();
            _batchListBox.Items.Clear();
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
                var r = MessageBox.Show(_parentWindow,
                    "FFmpeg ist noch nicht installiert!\n\nMöchten Sie den Download-Manager öffnen, um FFmpeg herunterzuladen?",
                    "FFmpeg fehlt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes)
                {
                    ShowCodecManagementWindow();
                }
                return;
            }

            AudioFormat format = (AudioFormat)_formatComboBox.SelectedIndex;
            int bitrate = GetSelectedBitrate();
            int sampleRate = GetSelectedSampleRate();

            _isConverting = true;
            _btnStartBatch.IsEnabled = false;
            _btnAddFiles.IsEnabled = false;
            _btnClearBatch.IsEnabled = false;
            _progressBar.Value = 0;
            _logTextBox.Clear();

            _logTextBox.AppendText(string.Format("[{0}] Starte Batch-Konvertierung ({1} Dateien)...\n",
                DateTime.Now.ToString("HH:mm:ss"), _batchItems.Count));

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int total = _batchItems.Count;
                int successful = 0;

                for (int i = 0; i < total; i++)
                {
                    var item = _batchItems[i];
                    int index = i;

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _logTextBox.AppendText(string.Format("[{0}] [{1}/{2}] Konvertiere: {3}...\n",
                            DateTime.Now.ToString("HH:mm:ss"), index + 1, total, Path.GetFileName(item.InputFile)));
                    });

                    string inExt = Path.GetExtension(item.InputFile).ToLowerInvariant();
                    bool success = false;

                    if (inExt == ".mid" || inExt == ".midi")
                    {
                        string tempWav = Path.Combine(Path.GetTempPath(), "batch_midi_" + Guid.NewGuid() + ".wav");
                        bool synthOk = MidiSynthesizer.ConvertMidiToWav(item.InputFile, tempWav, null);

                        if (synthOk)
                        {
                            if (format == AudioFormat.WAV)
                            {
                                try
                                {
                                    if (File.Exists(item.OutputFile)) File.Delete(item.OutputFile);
                                    File.Move(tempWav, item.OutputFile);
                                    success = true;
                                }
                                catch { }
                            }
                            else
                            {
                                success = AudioConverterService.ConvertAudioAdvanced(tempWav, item.OutputFile, format, bitrate, sampleRate, null, null);
                                try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
                            }
                        }
                    }
                    else
                    {
                        success = AudioConverterService.ConvertAudioAdvanced(item.InputFile, item.OutputFile, format, bitrate, sampleRate, null, null);
                    }

                    if (success) successful++;

                    _parentWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _progressBar.Value = (int)(((index + 1) / (double)total) * 100);
                    });
                }

                _parentWindow.Dispatcher.InvokeAsync(() =>
                {
                    _isConverting = false;
                    _btnStartBatch.IsEnabled = true;
                    _btnAddFiles.IsEnabled = true;
                    _btnClearBatch.IsEnabled = true;

                    _logTextBox.AppendText(string.Format("[{0}] ✓ Batch-Konvertierung abgeschlossen! {1}/{2} erfolgreich.\n",
                        DateTime.Now.ToString("HH:mm:ss"), successful, total));

                    MessageBox.Show(_parentWindow,
                        string.Format("Batch-Konvertierung abgeschlossen!\n\n{0} von {1} Dateien erfolgreich konvertiert.", successful, total),
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
                case 1: return 44100;
                case 2: return 48000;
                case 3: return 96000;
                default: return 0; // 0 = Keep original
            }
        }

        private void OpenFolderAndSelectFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string argument = "/select, \"" + filePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                else
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", dir);
                    }
                }
            }
            catch { }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        private Border CreateCard(string headerText, UIElement content)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = headerText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(header);
            stack.Children.Add(content);

            card.Child = stack;
            return card;
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
            template.Triggers.Add(triggerIsMouseOver);

            var triggerIsPressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            triggerIsPressed.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)), "border"));
            template.Triggers.Add(triggerIsPressed);

            var triggerDisabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            triggerDisabled.Setters.Add(new Setter(Border.OpacityProperty, 0.4, "border"));
            template.Triggers.Add(triggerDisabled);
            template.VisualTree = factory;

            btn.Template = template;
            return btn;
        }
    }
}
