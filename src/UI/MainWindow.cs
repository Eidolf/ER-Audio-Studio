using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ErAudioTool.Audio;

namespace ErAudioTool.UI
{
    public class RecordingItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Timestamp { get; set; }
        public string Duration { get; set; }
        public string FileSize { get; set; }
    }

    public class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly WasapiLoopbackEngine _engine;
        private readonly SimpleAudioPlayer _player;
        private readonly List<RecordingItem> _recordingsList = new List<RecordingItem>();

        // UI Controls
        private ComboBox _deviceComboBox;
        private Button _refreshDevicesButton;
        private TextBlock _deviceFormatText;

        private VuMeterControl _vuMeter;
        private TextBlock _audioActivityBadge;

        private TextBlock _timerText;
        private TextBlock _fileSizeText;
        private TextBlock _formatInfoText;
        private TextBlock _statusPillText;
        private Border _statusPillBorder;

        private Button _btnRecord;
        private Button _btnPause;
        private Button _btnOpenFolder;

        private TextBox _outputDirTextBox;
        private Button _browseDirButton;
        private TextBox _prefixTextBox;
        private ComboBox _formatComboBox;

        private ListBox _historyListBox;

        // MIDI UI Controls
        private TextBox _midiInputTextBox;
        private TextBox _midiOutputTextBox;
        private Slider _midiThresholdSlider;
        private TextBlock _midiThresholdText;
        private Slider _midiMinDurationSlider;
        private TextBlock _midiMinDurationText;
        private Slider _midiBpmSlider;
        private TextBlock _midiBpmText;
        private CheckBox _midiAddDrumsCheckBox;
        private CheckBox _midiAddBassCheckBox;
        private CheckBox _midiAddChordsCheckBox;
        private CheckBox _midiUseCliCheckBox;
        private Button _btnMidiDownloadCli;
        private TextBlock _midiCliStatusText;
        private Button _btnConvertMidi;
        private TextBox _midiLogTextBox;

        // Tab Controls
        private Button _btnTabRecord;
        private Button _btnTabMidi;
        private Button _btnTabConvert;
        private Button _btnTabArrange;
        private UIElement _recordTabContent;
        private UIElement _midiTabContent;
        private UIElement _converterTabContent;
        private UIElement _arrangeTabContent;
        private AudioPreAnalysis _lastPreAnalysis;
        private AudioConverterTab _converterTab;

        // Arrange & EQ Tab Controls
        private TextBox _arrangeInputTextBox;
        private TextBox _arrangeOutputTextBox;
        private Slider _eqBassSlider;
        private Slider _eqMidSlider;
        private Slider _eqTrebleSlider;
        private Slider _masterVolumeSlider;
        private TextBlock _eqBassText;
        private TextBlock _eqMidText;
        private TextBlock _eqTrebleText;
        private TextBlock _masterVolumeText;
        private CheckBox _arrangeNormalizeCheckBox;
        private CheckBox _arrangeCompressCheckBox;
        private CheckBox _arrangeReverbCheckBox;
        private Slider _arrangeCompressRatioSlider;
        private Slider _arrangeReverbAmountSlider;
        private TextBlock _arrangeCompressRatioText;
        private TextBlock _arrangeReverbAmountText;
        private Button _btnProcessArrange;
        private TextBox _arrangeLogTextBox;

        private string _outputDirectory;
        private DispatcherTimer _uiTimer;
        private TimeSpan _currentDuration;
        private long _currentBytes;

        public MainWindow()
        {
            _engine = new WasapiLoopbackEngine();
            _player = new SimpleAudioPlayer();

            // Default output directory: recordings in workspace or user's Music/Recordings folder
            string defaultRecDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings");
            if (!Directory.Exists(defaultRecDir))
            {
                try { Directory.CreateDirectory(defaultRecDir); } catch { }
            }
            _outputDirectory = defaultRecDir;

            InitializeWindow();
            BuildUi();
            HookEvents();
            LoadAudioDevices();
            LoadExistingRecordings();

            // UI update timer for clock and stats
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void InitializeWindow()
        {
            Title = "ER Audio Loopback Recorder";
            Width = 1100;
            Height = 900;
            MinWidth = 900;
            MinHeight = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // #0F172A

            Loaded += (s, e) =>
            {
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    int darkMode = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
                }
                catch { }
            };

            Closed += (s, e) =>
            {
                if (_uiTimer != null) _uiTimer.Stop();
                if (_player != null) _player.Dispose();
                if (_engine != null) _engine.Dispose();
            };
        }

        private void BuildUi()
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scrollable Content
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // --- HEADER ---
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBorder = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(8),
                Background = new LinearGradientBrush(Color.FromRgb(6, 182, 212), Color.FromRgb(59, 130, 246), 45),
                Margin = new Thickness(0, 0, 14, 0)
            };
            var iconText = new TextBlock
            {
                Text = "●",
                FontSize = 22,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            titleStack.Children.Add(iconBorder);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleText = new TextBlock
            {
                Text = "ER AUDIO LOOPBACK RECORDER",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            var subText = new TextBlock
            {
                Text = "System-Audio direkt aufnehmen ohne Qualitätsverlust (WASAPI)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(titleText);
            textStack.Children.Add(subText);
            titleStack.Children.Add(textStack);
            headerGrid.Children.Add(titleStack);

            // Status Pill
            _statusPillBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusPillText = new TextBlock
            {
                Text = "● BEREIT",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            _statusPillBorder.Child = _statusPillText;

            // Tab Switcher Buttons in Header
            var tabsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

            _btnTabRecord = CreateStyledButton("🎙 Aufnahme", Color.FromRgb(37, 99, 235), Color.FromRgb(29, 78, 216), 34);
            _btnTabRecord.Click += (s, e) => SwitchToRecordTab();
            tabsStack.Children.Add(_btnTabRecord);

            _btnTabConvert = CreateStyledButton("🔄 Konverter", Color.FromRgb(34, 197, 94), Color.FromRgb(22, 163, 74), 34);
            _btnTabConvert.Margin = new Thickness(8, 0, 0, 0);
            _btnTabConvert.Opacity = 0.6;
            _btnTabConvert.Click += (s, e) => SwitchToConverterTab();
            tabsStack.Children.Add(_btnTabConvert);

            _btnTabMidi = CreateStyledButton("🎹 Audio zu MIDI", Color.FromRgb(147, 51, 234), Color.FromRgb(126, 34, 206), 34);
            _btnTabMidi.Margin = new Thickness(8, 0, 0, 0);
            _btnTabMidi.Opacity = 0.6;
            _btnTabMidi.Click += (s, e) => SwitchToMidiTab();
            tabsStack.Children.Add(_btnTabMidi);

            _btnTabArrange = CreateStyledButton("🎛 Arrange & EQ", Color.FromRgb(236, 72, 153), Color.FromRgb(219, 39, 119), 34);
            _btnTabArrange.Margin = new Thickness(8, 0, 0, 0);
            _btnTabArrange.Opacity = 0.6;
            _btnTabArrange.Click += (s, e) => SwitchToArrangeTab();
            tabsStack.Children.Add(_btnTabArrange);

            var headerRightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            headerRightStack.Children.Add(tabsStack);
            headerRightStack.Children.Add(_statusPillBorder);
            Grid.SetColumn(headerRightStack, 1);
            headerGrid.Children.Add(headerRightStack);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // Container for Tab Views
            var mainContentGrid = new Grid();
            Grid.SetRow(mainContentGrid, 1);
            rootGrid.Children.Add(mainContentGrid);

            // --- TAB 1: RECORD CONTENT (SCROLLABLE) ---
            var recordScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16)
            };
            _recordTabContent = recordScrollViewer;
            mainContentGrid.Children.Add(_recordTabContent);

            var contentStack = new StackPanel();

            // 1. Device Selection Card
            var devGrid = new Grid();
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _deviceComboBox = new ComboBox
            {
                Height = 36,
                                Foreground = Brushes.White,
                                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _deviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
            devGrid.Children.Add(_deviceComboBox);

            _refreshDevicesButton = CreateStyledButton("🔄 Aktualisieren", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            _refreshDevicesButton.Click += (s, e) => LoadAudioDevices();
            Grid.SetColumn(_refreshDevicesButton, 1);
            devGrid.Children.Add(_refreshDevicesButton);

            var devStack = new StackPanel();
            devStack.Children.Add(devGrid);

            _deviceFormatText = new TextBlock
            {
                Text = "Format: Ermittle Gerätedaten...",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            devStack.Children.Add(_deviceFormatText);
            contentStack.Children.Add(CreateCard("WIEDERGABEGERÄT (LOOPBACK-QUELLE)", devStack));

            // 2. VU-Meter Card
            var meterStack = new StackPanel();
            _vuMeter = new VuMeterControl { Height = 58 };
            meterStack.Children.Add(_vuMeter);

            var meterFooter = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            meterFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            meterFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var meterDesc = new TextBlock
            {
                Text = "Pegelanzeige des gewählten Ausgangs (Echtzeit)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            meterFooter.Children.Add(meterDesc);

            _audioActivityBadge = new TextBlock
            {
                Text = "● Audio aktiv",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            Grid.SetColumn(_audioActivityBadge, 1);
            meterFooter.Children.Add(_audioActivityBadge);

            meterStack.Children.Add(meterFooter);
            contentStack.Children.Add(CreateCard("LIVE AUDIO-PEGEL (VU-METER)", meterStack));

            // 3. Recording Hero Card
            var heroStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            // Big Timer
            _timerText = new TextBlock
            {
                Text = "00:00:00.0",
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4)
            };
            heroStack.Children.Add(_timerText);

            // Stats info line
            var statsGrid = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            _fileSizeText = new TextBlock
            {
                Text = "Größe: 0.0 MB",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 20, 0)
            };
            _formatInfoText = new TextBlock
            {
                Text = "Format: 16-Bit PCM WAV (48 kHz)",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            statsGrid.Children.Add(_fileSizeText);
            statsGrid.Children.Add(_formatInfoText);
            heroStack.Children.Add(statsGrid);

            // Action Buttons
            var btnGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnRecord = CreateStyledButton("●  Aufnahme starten", Color.FromRgb(239, 68, 68), Color.FromRgb(220, 38, 38), 44, 210);
            _btnRecord.FontWeight = FontWeights.Bold;
            _btnRecord.FontSize = 14;
            _btnRecord.Click += BtnRecord_Click;
            btnGrid.Children.Add(_btnRecord);

            _btnPause = CreateStyledButton("⏸  Pause", Color.FromRgb(245, 158, 11), Color.FromRgb(217, 119, 6), 44, 120);
            _btnPause.Margin = new Thickness(14, 0, 0, 0);
            _btnPause.IsEnabled = false;
            _btnPause.Click += BtnPause_Click;
            Grid.SetColumn(_btnPause, 1);
            btnGrid.Children.Add(_btnPause);

            heroStack.Children.Add(btnGrid);

            var recordHeroCard = CreateCard("AUFNAHME-STEUERUNG", heroStack);
            recordHeroCard.Background = new SolidColorBrush(Color.FromRgb(24, 34, 53));
            recordHeroCard.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            contentStack.Children.Add(recordHeroCard);

            // 4. Settings Card (Output folder & format)
            var setStack = new StackPanel();

            // Folder row
            var folderLabel = new TextBlock
            {
                Text = "Zielverzeichnis für Aufnahmen:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            setStack.Children.Add(folderLabel);

            var folderGrid = new Grid();
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _outputDirTextBox = new TextBox
            {
                Text = _outputDirectory,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _outputDirTextBox.TextChanged += (s, e) => _outputDirectory = _outputDirTextBox.Text;
            folderGrid.Children.Add(_outputDirTextBox);

            _browseDirButton = CreateStyledButton("📁 Durchsuchen...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _browseDirButton.Margin = new Thickness(0, 0, 8, 0);
            _browseDirButton.Click += BrowseDirButton_Click;
            Grid.SetColumn(_browseDirButton, 1);
            folderGrid.Children.Add(_browseDirButton);

            _btnOpenFolder = CreateStyledButton("📂 Ordner öffnen", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(_outputDirectory))
                {
                    Process.Start("explorer.exe", _outputDirectory);
                }
            };
            Grid.SetColumn(_btnOpenFolder, 2);
            folderGrid.Children.Add(_btnOpenFolder);

            setStack.Children.Add(folderGrid);

            // Format & Prefix row
            var optGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            optGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Format selector
            var formatStack = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            var formatLabel = new TextBlock
            {
                Text = "Audioformat:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            formatStack.Children.Add(formatLabel);

            _formatComboBox = new ComboBox
            {
                Height = 32,
                                Foreground = Brushes.White,
                                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _formatComboBox.Items.Add("16-Bit PCM WAV (Universell kompatibel)");
            _formatComboBox.Items.Add("32-Bit Float WAV (Verlustfrei / Studio)");
            _formatComboBox.SelectedIndex = 0;
            _formatComboBox.SelectionChanged += (s, e) =>
            {
                _engine.OutputFormat = _formatComboBox.SelectedIndex == 0 ? WavOutputFormat.Pcm16 : WavOutputFormat.Float32;
                UpdateFormatBadge();
            };
            formatStack.Children.Add(_formatComboBox);
            optGrid.Children.Add(formatStack);

            // Prefix
            var prefixStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            var prefixLabel = new TextBlock
            {
                Text = "Dateinamen-Präfix:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            prefixStack.Children.Add(prefixLabel);

            _prefixTextBox = new TextBox
            {
                Text = "Loopback",
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0)
            };
            prefixStack.Children.Add(_prefixTextBox);
            Grid.SetColumn(prefixStack, 1);
            optGrid.Children.Add(prefixStack);

            setStack.Children.Add(optGrid);
            contentStack.Children.Add(CreateCard("SPEICHERORT & EINSTELLUNGEN", setStack));

            // 5. History & Preview Card
            var histStack = new StackPanel();
            _historyListBox = new ListBox
            {
                MaxHeight = 160,
                MinHeight = 80,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Foreground = Brushes.White
            };
            histStack.Children.Add(_historyListBox);
            contentStack.Children.Add(CreateCard("AUFNAHMEN DIESER SITZUNG (VERLAUF & VORSCHAU)", histStack));

            recordScrollViewer.Content = contentStack;

            // --- TAB 2: MIDI CONVERTER CONTENT (SCROLLABLE) ---
            var midiScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16),
                Visibility = Visibility.Collapsed
            };
            _midiTabContent = midiScrollViewer;
            mainContentGrid.Children.Add(_midiTabContent);

            var midiStack = new StackPanel();

            // Card 1: File Selection
            var midiFilesCardStack = new StackPanel();

            var inLabel = new TextBlock
            {
                Text = "Eingangs-Audiodatei (WAV, MP3, FLAC, OGG, M4A):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            midiFilesCardStack.Children.Add(inLabel);

            var inGrid = new Grid();
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _midiInputTextBox = new TextBox
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
            inGrid.Children.Add(_midiInputTextBox);

            var btnBrowseIn = CreateStyledButton("📁 Durchsuchen...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            btnBrowseIn.Click += (s, e) => SelectMidiInputFile();
            Grid.SetColumn(btnBrowseIn, 1);
            inGrid.Children.Add(btnBrowseIn);
            midiFilesCardStack.Children.Add(inGrid);

            var outLabel = new TextBlock
            {
                Text = "Ziel MIDI-Datei (.mid):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 10, 0, 6)
            };
            midiFilesCardStack.Children.Add(outLabel);

            var outGrid = new Grid();
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _midiOutputTextBox = new TextBox
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
            outGrid.Children.Add(_midiOutputTextBox);

            var btnBrowseOut = CreateStyledButton("💾 Speicherort...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            btnBrowseOut.Click += (s, e) => SelectMidiOutputFile();
            Grid.SetColumn(btnBrowseOut, 1);
            outGrid.Children.Add(btnBrowseOut);
            midiFilesCardStack.Children.Add(outGrid);

            midiStack.Children.Add(CreateCard("DATEI-AUSWAHL", midiFilesCardStack));

            // Card 2: Pitch & Detection Parameters
            var paramStack = new StackPanel();

            // Threshold Slider
            var threshHeader = new Grid();
            threshHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            threshHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var threshLabel = new TextBlock
            {
                Text = "Lautstärke-Schwellenwert (Rauschfilter):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            threshHeader.Children.Add(threshLabel);

            _midiThresholdText = new TextBlock
            {
                Text = "-42 dB",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Grid.SetColumn(_midiThresholdText, 1);
            threshHeader.Children.Add(_midiThresholdText);
            paramStack.Children.Add(threshHeader);

            _midiThresholdSlider = new Slider
            {
                Minimum = -60.0,
                Maximum = -15.0,
                Value = -42.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _midiThresholdSlider.ValueChanged += (s, e) =>
            {
                if (_midiThresholdText != null) _midiThresholdText.Text = string.Format("{0:0} dB", _midiThresholdSlider.Value);
            };
            paramStack.Children.Add(_midiThresholdSlider);

            // Min Duration Slider
            var durHeader = new Grid();
            durHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            durHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var durLabel = new TextBlock
            {
                Text = "Minimale Notenlänge:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            durHeader.Children.Add(durLabel);

            _midiMinDurationText = new TextBlock
            {
                Text = "80 ms",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Grid.SetColumn(_midiMinDurationText, 1);
            durHeader.Children.Add(_midiMinDurationText);
            paramStack.Children.Add(durHeader);

            _midiMinDurationSlider = new Slider
            {
                Minimum = 0.03,
                Maximum = 0.30,
                Value = 0.08,
                TickFrequency = 0.01,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _midiMinDurationSlider.ValueChanged += (s, e) =>
            {
                if (_midiMinDurationText != null) _midiMinDurationText.Text = string.Format("{0:0} ms", _midiMinDurationSlider.Value * 1000.0);
            };
            paramStack.Children.Add(_midiMinDurationSlider);

            // Tempo BPM Slider
            var bpmHeader = new Grid();
            bpmHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bpmHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bpmLabel = new TextBlock
            {
                Text = "Song-Tempo (BPM / Taktgeschwindigkeit):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            bpmHeader.Children.Add(bpmLabel);

            _midiBpmText = new TextBlock
            {
                Text = "120 BPM",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Grid.SetColumn(_midiBpmText, 1);
            bpmHeader.Children.Add(_midiBpmText);
            paramStack.Children.Add(bpmHeader);

            _midiBpmSlider = new Slider
            {
                Minimum = 60.0,
                Maximum = 180.0,
                Value = 120.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 14)
            };
            _midiBpmSlider.ValueChanged += (s, e) =>
            {
                if (_midiBpmText != null) _midiBpmText.Text = string.Format("{0:0} BPM", _midiBpmSlider.Value);
            };
            paramStack.Children.Add(_midiBpmSlider);

            // Multi-Instrument Arrangement Section
            var arrLabel = new TextBlock
            {
                Text = "INSTRUMENTAL-ARRANGEMENT FÜR SUNO / DAWS:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            paramStack.Children.Add(arrLabel);

            _midiAddDrumsCheckBox = new CheckBox
            {
                Content = "🥁 Takt & Beat hinzufügen (Kanal 10: Kick, Snare & Hi-Hat)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            paramStack.Children.Add(_midiAddDrumsCheckBox);

            _midiAddBassCheckBox = new CheckBox
            {
                Content = "🎸 Bassline / Fundament erzeugen (Kanal 2: Electric Bass)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            paramStack.Children.Add(_midiAddBassCheckBox);

            _midiAddChordsCheckBox = new CheckBox
            {
                Content = "✨ Harmonie-Akkorde / Rhythmusgitarre (Kanal 3: Acoustic Guitar)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            paramStack.Children.Add(_midiAddChordsCheckBox);

            // CLI Tool Option Checkbox
            _midiUseCliCheckBox = new CheckBox
            {
                Content = "Erweiterte KI/CLI-Erkennung bevorzugen falls installiert (basic-pitch / aubio)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 14)
            };
            // CLI Tool Option Checkbox & Download Button
            var cliRowGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            cliRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cliRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var cliLeftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _midiUseCliCheckBox.Margin = new Thickness(0);
            cliLeftStack.Children.Add(_midiUseCliCheckBox);
            _midiCliStatusText = new TextBlock { Text = "", FontSize = 11, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            cliLeftStack.Children.Add(_midiCliStatusText);
            cliRowGrid.Children.Add(cliLeftStack);
            _btnMidiDownloadCli = CreateStyledButton("📥 KI / Codecs verwalten", Color.FromRgb(59, 130, 246), Color.FromRgb(37, 99, 235), 26);
            _btnMidiDownloadCli.FontSize = 11;
            _btnMidiDownloadCli.Padding = new Thickness(10, 0, 10, 0);
            _btnMidiDownloadCli.Click += (s, e) => { if (_converterTab != null) { _converterTab.ShowCodecManagementWindow(); UpdateMidiCliStatusBadge(); } };
            Grid.SetColumn(_btnMidiDownloadCli, 1);
            cliRowGrid.Children.Add(_btnMidiDownloadCli);
            paramStack.Children.Add(cliRowGrid);
            UpdateMidiCliStatusBadge();

            // Convert Button
            _btnConvertMidi = CreateStyledButton("🎹  Multi-Instrument MIDI erstellen", Color.FromRgb(147, 51, 234), Color.FromRgb(126, 34, 206), 42, 290);
            _btnConvertMidi.FontWeight = FontWeights.Bold;
            _btnConvertMidi.FontSize = 13;
            _btnConvertMidi.HorizontalAlignment = HorizontalAlignment.Center;
            _btnConvertMidi.Click += (s, e) => ConvertAudioToMidi();
            paramStack.Children.Add(_btnConvertMidi);

            midiStack.Children.Add(CreateCard("ERKENNUNGS-PARAMETER & AKTION", paramStack));

            // Card 3: Log & Note Inspection
            var logStack = new StackPanel();
            _midiLogTextBox = new TextBox
            {
                Height = 160,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                Text = "Bereit für Konvertierung. Wählen Sie oben eine Audiodatei oder klicken Sie in den Aufnahmen auf '🎹 MIDI'."
            };
            logStack.Children.Add(_midiLogTextBox);
            midiStack.Children.Add(CreateCard("KONVERTIERUNGS-PROTOKOLL & NOTEN-INSPEKTION", logStack));

            midiScrollViewer.Content = midiStack;

            // --- TAB 3: AUDIO CONVERTER CONTENT ---
            _converterTab = new AudioConverterTab(this);
            _converterTabContent = _converterTab.BuildUI();
            _converterTabContent.Visibility = Visibility.Collapsed;
            mainContentGrid.Children.Add(_converterTabContent);

            // --- TAB 4: ARRANGE & EQ CONTENT (SCROLLABLE) ---
            var arrangeScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16),
                Visibility = Visibility.Collapsed
            };
            _arrangeTabContent = arrangeScrollViewer;
            mainContentGrid.Children.Add(_arrangeTabContent);

            var arrangeStack = new StackPanel();

            // Card 1: File Selection
            var arrangeFilesStack = new StackPanel();

            var arrangeInLabel = new TextBlock
            {
                Text = "Eingangs-Audiodatei (WAV, MP3, FLAC, OGG):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            arrangeFilesStack.Children.Add(arrangeInLabel);

            var arrangeInGrid = new Grid();
            arrangeInGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            arrangeInGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _arrangeInputTextBox = new TextBox
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
            arrangeInGrid.Children.Add(_arrangeInputTextBox);

            var btnArrangeBrowseIn = CreateStyledButton("📁 Durchsuchen...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            btnArrangeBrowseIn.Click += (s, e) => SelectArrangeInputFile();
            Grid.SetColumn(btnArrangeBrowseIn, 1);
            arrangeInGrid.Children.Add(btnArrangeBrowseIn);
            arrangeFilesStack.Children.Add(arrangeInGrid);

            var arrangeOutLabel = new TextBlock
            {
                Text = "Ausgabe-Audiodatei:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 10, 0, 6)
            };
            arrangeFilesStack.Children.Add(arrangeOutLabel);

            var arrangeOutGrid = new Grid();
            arrangeOutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            arrangeOutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _arrangeOutputTextBox = new TextBox
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
            arrangeOutGrid.Children.Add(_arrangeOutputTextBox);

            var btnArrangeBrowseOut = CreateStyledButton("💾 Speicherort...", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            btnArrangeBrowseOut.Click += (s, e) => SelectArrangeOutputFile();
            Grid.SetColumn(btnArrangeBrowseOut, 1);
            arrangeOutGrid.Children.Add(btnArrangeBrowseOut);
            arrangeFilesStack.Children.Add(arrangeOutGrid);

            arrangeStack.Children.Add(CreateCard("DATEI-AUSWAHL", arrangeFilesStack));

            // Card 2: Equalizer Controls
            var eqStack = new StackPanel();

            var eqDesc = new TextBlock
            {
                Text = "Passen Sie die Frequenzbereiche an, um den Klang zu optimieren:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            eqStack.Children.Add(eqDesc);

            // Bass Slider
            var bassHeader = new Grid();
            bassHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bassHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var bassLabel = new TextBlock
            {
                Text = "🔊 Bass (60-250 Hz):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            bassHeader.Children.Add(bassLabel);

            _eqBassText = new TextBlock
            {
                Text = "0 dB",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153))
            };
            Grid.SetColumn(_eqBassText, 1);
            bassHeader.Children.Add(_eqBassText);
            eqStack.Children.Add(bassHeader);

            _eqBassSlider = new Slider
            {
                Minimum = -12.0,
                Maximum = 12.0,
                Value = 0.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _eqBassSlider.ValueChanged += (s, e) =>
            {
                if (_eqBassText != null) _eqBassText.Text = string.Format("{0:+0;-0;0} dB", _eqBassSlider.Value);
            };
            eqStack.Children.Add(_eqBassSlider);

            // Mid Slider
            var midHeader = new Grid();
            midHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            midHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var midLabel = new TextBlock
            {
                Text = "🎸 Mitten (250 Hz - 4 kHz):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            midHeader.Children.Add(midLabel);

            _eqMidText = new TextBlock
            {
                Text = "0 dB",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153))
            };
            Grid.SetColumn(_eqMidText, 1);
            midHeader.Children.Add(_eqMidText);
            eqStack.Children.Add(midHeader);

            _eqMidSlider = new Slider
            {
                Minimum = -12.0,
                Maximum = 12.0,
                Value = 0.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _eqMidSlider.ValueChanged += (s, e) =>
            {
                if (_eqMidText != null) _eqMidText.Text = string.Format("{0:+0;-0;0} dB", _eqMidSlider.Value);
            };
            eqStack.Children.Add(_eqMidSlider);

            // Treble Slider
            var trebleHeader = new Grid();
            trebleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            trebleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var trebleLabel = new TextBlock
            {
                Text = "✨ Höhen (4 kHz - 16 kHz):",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            trebleHeader.Children.Add(trebleLabel);

            _eqTrebleText = new TextBlock
            {
                Text = "0 dB",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153))
            };
            Grid.SetColumn(_eqTrebleText, 1);
            trebleHeader.Children.Add(_eqTrebleText);
            eqStack.Children.Add(trebleHeader);

            _eqTrebleSlider = new Slider
            {
                Minimum = -12.0,
                Maximum = 12.0,
                Value = 0.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 12)
            };
            _eqTrebleSlider.ValueChanged += (s, e) =>
            {
                if (_eqTrebleText != null) _eqTrebleText.Text = string.Format("{0:+0;-0;0} dB", _eqTrebleSlider.Value);
            };
            eqStack.Children.Add(_eqTrebleSlider);

            // Master Volume
            var volumeHeader = new Grid();
            volumeHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volumeHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var volumeLabel = new TextBlock
            {
                Text = "🔉 Master-Lautstärke:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            volumeHeader.Children.Add(volumeLabel);

            _masterVolumeText = new TextBlock
            {
                Text = "0 dB",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Grid.SetColumn(_masterVolumeText, 1);
            volumeHeader.Children.Add(_masterVolumeText);
            eqStack.Children.Add(volumeHeader);

            _masterVolumeSlider = new Slider
            {
                Minimum = -20.0,
                Maximum = 12.0,
                Value = 0.0,
                TickFrequency = 1.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _masterVolumeSlider.ValueChanged += (s, e) =>
            {
                if (_masterVolumeText != null) _masterVolumeText.Text = string.Format("{0:+0;-0;0} dB", _masterVolumeSlider.Value);
            };
            eqStack.Children.Add(_masterVolumeSlider);

            arrangeStack.Children.Add(CreateCard("3-BAND EQUALIZER", eqStack));

            // Card 3: Professional Effects
            var effectsStack = new StackPanel();

            var effectsLabel = new TextBlock
            {
                Text = "PROFESSIONELLE AUDIO-EFFEKTE:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            effectsStack.Children.Add(effectsLabel);

            _arrangeNormalizeCheckBox = new CheckBox
            {
                Content = "🎚 Normalisierung (Optimale Lautstärke ohne Clipping)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            effectsStack.Children.Add(_arrangeNormalizeCheckBox);

            _arrangeCompressCheckBox = new CheckBox
            {
                Content = "📊 Dynamik-Kompressor (Gleichmäßigere Lautstärke)",
                IsChecked = false,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _arrangeCompressCheckBox.Checked += (s, e) => UpdateCompressorVisibility();
            _arrangeCompressCheckBox.Unchecked += (s, e) => UpdateCompressorVisibility();
            effectsStack.Children.Add(_arrangeCompressCheckBox);

            // Compressor Ratio (initially hidden)
            var compressRatioHeader = new Grid { Margin = new Thickness(20, 0, 0, 8), Visibility = Visibility.Collapsed };
            compressRatioHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            compressRatioHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var compressRatioLabel = new TextBlock
            {
                Text = "Kompressions-Verhältnis:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            compressRatioHeader.Children.Add(compressRatioLabel);

            _arrangeCompressRatioText = new TextBlock
            {
                Text = "4:1",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153))
            };
            Grid.SetColumn(_arrangeCompressRatioText, 1);
            compressRatioHeader.Children.Add(_arrangeCompressRatioText);
            effectsStack.Children.Add(compressRatioHeader);

            _arrangeCompressRatioSlider = new Slider
            {
                Minimum = 2.0,
                Maximum = 10.0,
                Value = 4.0,
                TickFrequency = 0.5,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(20, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            _arrangeCompressRatioSlider.ValueChanged += (s, e) =>
            {
                if (_arrangeCompressRatioText != null) _arrangeCompressRatioText.Text = string.Format("{0:0.0}:1", _arrangeCompressRatioSlider.Value);
            };
            effectsStack.Children.Add(_arrangeCompressRatioSlider);

            _arrangeReverbCheckBox = new CheckBox
            {
                Content = "🌊 Hall-Effekt / Reverb (Räumliche Tiefe)",
                IsChecked = false,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _arrangeReverbCheckBox.Checked += (s, e) => UpdateReverbVisibility();
            _arrangeReverbCheckBox.Unchecked += (s, e) => UpdateReverbVisibility();
            effectsStack.Children.Add(_arrangeReverbCheckBox);

            // Reverb Amount (initially hidden)
            var reverbAmountHeader = new Grid { Margin = new Thickness(20, 0, 0, 8), Visibility = Visibility.Collapsed };
            reverbAmountHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reverbAmountHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var reverbAmountLabel = new TextBlock
            {
                Text = "Hall-Intensität:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            reverbAmountHeader.Children.Add(reverbAmountLabel);

            _arrangeReverbAmountText = new TextBlock
            {
                Text = "30%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153))
            };
            Grid.SetColumn(_arrangeReverbAmountText, 1);
            reverbAmountHeader.Children.Add(_arrangeReverbAmountText);
            effectsStack.Children.Add(reverbAmountHeader);

            _arrangeReverbAmountSlider = new Slider
            {
                Minimum = 10.0,
                Maximum = 80.0,
                Value = 30.0,
                TickFrequency = 5.0,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(20, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _arrangeReverbAmountSlider.ValueChanged += (s, e) =>
            {
                if (_arrangeReverbAmountText != null) _arrangeReverbAmountText.Text = string.Format("{0:0}%", _arrangeReverbAmountSlider.Value);
            };
            effectsStack.Children.Add(_arrangeReverbAmountSlider);

            arrangeStack.Children.Add(CreateCard("PROFESSIONELLE EFFEKTE", effectsStack));

            // Process Button
            var processButtonStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            _btnProcessArrange = CreateStyledButton("🎛  Audio professionell bearbeiten", Color.FromRgb(236, 72, 153), Color.FromRgb(219, 39, 119), 44, 320);
            _btnProcessArrange.FontWeight = FontWeights.Bold;
            _btnProcessArrange.FontSize = 13;
            _btnProcessArrange.Click += (s, e) => ProcessArrangeAudio();
            processButtonStack.Children.Add(_btnProcessArrange);
            arrangeStack.Children.Add(processButtonStack);

            // Card 4: Processing Log
            var arrangeLogStack = new StackPanel();
            _arrangeLogTextBox = new TextBox
            {
                Height = 180,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                Text = "Bereit für Audio-Bearbeitung. Wählen Sie eine Datei und passen Sie die EQ-Einstellungen an."
            };
            arrangeLogStack.Children.Add(_arrangeLogTextBox);
            arrangeStack.Children.Add(CreateCard("VERARBEITUNGS-PROTOKOLL", arrangeLogStack));

            arrangeScrollViewer.Content = arrangeStack;

            // --- FOOTER ---
            var footerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 10, 24, 10)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var footerText = new TextBlock
            {
                Text = "ER Audio Tool v1.0 • Native WASAPI Loopback • Bereit",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            footerGrid.Children.Add(footerText);

            var keyHint = new TextBlock
            {
                Text = "Tastenkombination: Leertaste zum Starten/Stoppen",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            Grid.SetColumn(keyHint, 1);
            footerGrid.Children.Add(keyHint);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 2);
            rootGrid.Children.Add(footerBorder);

            Content = rootGrid;

            // Shortcut
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Space && !(_outputDirTextBox.IsFocused || _prefixTextBox.IsFocused))
                {
                    BtnRecord_Click(this, null);
                    e.Handled = true;
                }
            };
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

        private void HookEvents()
        {
            _engine.PeakUpdated += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _vuMeter.SetLevels(e.Left, e.Right);
                    if (e.Master > 0.005f)
                    {
                        _audioActivityBadge.Text = "● Audio aktiv";
                        _audioActivityBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    }
                    else
                    {
                        _audioActivityBadge.Text = "○ Stille";
                        _audioActivityBadge.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    }
                });
            };

            _engine.StatsUpdated += (s, e) =>
            {
                _currentDuration = e.Elapsed;
                _currentBytes = e.BytesWritten;
            };

            _engine.StateChanged += (s, state) =>
            {
                Dispatcher.InvokeAsync(() => UpdateStateUi(state));
            };

            _engine.RecordingFinished += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddRecordingToHistory(e.FilePath, e.Duration, e.FileSizeBytes);
                });
            };

            _engine.ErrorOccurred += (s, ex) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(this, "Fehler bei der Aufnahme: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };
        }

        private void LoadAudioDevices()
        {
            var devices = AudioDeviceEnumerator.GetRenderDevices();
            _deviceComboBox.Items.Clear();

            int defaultIndex = 0;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                _deviceComboBox.Items.Add(dev);
                if (dev.IsDefault) defaultIndex = i;
            }

            if (_deviceComboBox.Items.Count > 0)
            {
                _deviceComboBox.SelectedIndex = defaultIndex;
            }
            else
            {
                _deviceFormatText.Text = "Kein Audiogerät gefunden!";
            }
        }

        private void LoadExistingRecordings()
        {
            try
            {
                if (!Directory.Exists(_outputDirectory))
                {
                    return;
                }

                // Get all WAV files in the output directory
                var files = Directory.GetFiles(_outputDirectory, "*.wav")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .Take(20) // Load last 20 recordings
                    .ToArray();

                foreach (var filePath in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (!fileInfo.Exists) continue;

                        // Try to get duration from WAV file
                        TimeSpan duration = TimeSpan.Zero;
                        try
                        {
                            duration = GetWavFileDuration(filePath);
                        }
                        catch { }

                        AddRecordingToHistory(filePath, duration, fileInfo.Length, false);
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }
            catch
            {
                // Ignore errors when loading existing recordings
            }
        }

        private TimeSpan GetWavFileDuration(string filePath)
        {
            try
            {
                using (var fs = File.OpenRead(filePath))
                using (var br = new BinaryReader(fs))
                {
                    // Read WAV header
                    var riff = new string(br.ReadChars(4));
                    if (riff != "RIFF") return TimeSpan.Zero;

                    br.ReadInt32(); // file size
                    var wave = new string(br.ReadChars(4));
                    if (wave != "WAVE") return TimeSpan.Zero;

                    int channels = 2;
                    int sampleRate = 44100;
                    int bitsPerSample = 16;
                    int dataSize = 0;

                    // Find fmt and data chunks
                    while (fs.Position < fs.Length - 8)
                    {
                        var chunkId = new string(br.ReadChars(4));
                        var chunkSize = br.ReadInt32();

                        if (chunkId == "fmt ")
                        {
                            br.ReadInt16(); // audio format
                            channels = br.ReadInt16();
                            sampleRate = br.ReadInt32();
                            br.ReadInt32(); // byte rate
                            br.ReadInt16(); // block align
                            bitsPerSample = br.ReadInt16();

                            if (chunkSize > 16)
                            {
                                br.ReadBytes(chunkSize - 16);
                            }
                        }
                        else if (chunkId == "data")
                        {
                            dataSize = chunkSize;
                            break;
                        }
                        else
                        {
                            br.ReadBytes(chunkSize);
                        }
                    }

                    if (dataSize > 0 && sampleRate > 0 && channels > 0 && bitsPerSample > 0)
                    {
                        int bytesPerSample = bitsPerSample / 8;
                        int totalSamples = dataSize / (channels * bytesPerSample);
                        double durationSeconds = (double)totalSamples / sampleRate;
                        return TimeSpan.FromSeconds(durationSeconds);
                    }
                }
            }
            catch { }

            return TimeSpan.Zero;
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dev = _deviceComboBox.SelectedItem as AudioDeviceInfo;
            if (dev != null)
            {
                _engine.SetDevice(dev);
                _deviceFormatText.Text = string.Format("Format: {0} ({1})", dev.FormatDescription, dev.IsDefault ? "Standardgerät" : "Alternativ");
                UpdateFormatBadge();
            }
        }

        private void UpdateFormatBadge()
        {
            var dev = _deviceComboBox.SelectedItem as AudioDeviceInfo;
            int sr = dev != null ? dev.SampleRate : 48000;
            string fmtName = _engine.OutputFormat == WavOutputFormat.Pcm16 ? "16-Bit PCM WAV" : "32-Bit Float WAV";
            _formatInfoText.Text = string.Format("Format: {0} ({1:N0} Hz Stereo)", fmtName, sr);
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_engine.State == RecordingState.Recording || _engine.State == RecordingState.Paused)
            {
                _timerText.Text = string.Format("{0:00}:{1:00}:{2:00}.{3:0}",
                    (int)_currentDuration.TotalHours,
                    _currentDuration.Minutes,
                    _currentDuration.Seconds,
                    _currentDuration.Milliseconds / 100);

                double mb = (double)_currentBytes / (1024 * 1024);
                _fileSizeText.Text = string.Format("Größe: {0:0.0} MB", mb);
            }
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.State == RecordingState.Idle)
            {
                // Start recording
                var dev = _deviceComboBox.SelectedItem as AudioDeviceInfo ?? AudioDeviceEnumerator.GetDefaultRenderDevice();
                if (dev == null)
                {
                    MessageBox.Show(this, "Bitte wählen Sie ein Audiogerät aus.", "Kein Gerät", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string prefix = string.IsNullOrEmpty(_prefixTextBox.Text.Trim()) ? "Loopback" : _prefixTextBox.Text.Trim();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = string.Format("{0}_{1}.wav", prefix, timestamp);
                string outPath = Path.Combine(_outputDirectory, fileName);

                try
                {
                    _currentDuration = TimeSpan.Zero;
                    _currentBytes = 0;
                    _engine.StartRecording(dev, outPath, _engine.OutputFormat);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Start fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Stop recording
                _engine.StopRecording();
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.State == RecordingState.Recording)
            {
                _engine.PauseRecording();
            }
            else if (_engine.State == RecordingState.Paused)
            {
                _engine.ResumeRecording();
            }
        }

        private void UpdateStateUi(RecordingState state)
        {
            switch (state)
            {
                case RecordingState.Idle:
                    _statusPillText.Text = "● BEREIT";
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                    _btnRecord.Content = "●  Aufnahme starten";
                    _btnRecord.IsEnabled = true;
                    ApplyButtonColor(_btnRecord, Color.FromRgb(239, 68, 68), Color.FromRgb(220, 38, 38));
                    _btnPause.IsEnabled = false;
                    _btnPause.Content = "⏸  Pause";

                    _deviceComboBox.IsEnabled = true;
                    _refreshDevicesButton.IsEnabled = true;
                    _formatComboBox.IsEnabled = true;
                    break;

                case RecordingState.Recording:
                    _statusPillText.Text = "● AUFNAHME LÄUFT";
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(60, 239, 68, 68));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));

                    _btnRecord.Content = "■  Aufnahme beenden";
                    ApplyButtonColor(_btnRecord, Color.FromRgb(71, 85, 105), Color.FromRgb(100, 116, 139));
                    _btnPause.IsEnabled = true;
                    _btnPause.Content = "⏸  Pause";
                    ApplyButtonColor(_btnPause, Color.FromRgb(245, 158, 11), Color.FromRgb(217, 119, 6));

                    _deviceComboBox.IsEnabled = false;
                    _refreshDevicesButton.IsEnabled = false;
                    _formatComboBox.IsEnabled = false;
                    break;

                case RecordingState.Paused:
                    _statusPillText.Text = "⏸ PAUSIERT";
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));

                    _btnPause.Content = "▶  Fortsetzen";
                    ApplyButtonColor(_btnPause, Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105));
                    break;

                case RecordingState.Stopping:
                    _statusPillText.Text = "⏳ WIRD GESPEICHERT...";
                    _btnRecord.IsEnabled = false;
                    _btnPause.IsEnabled = false;
                    break;
            }
        }

        private void ApplyButtonColor(Button btn, Color normalColor, Color hoverColor)
        {
            var template = btn.Template;
            var border = template.FindName("border", btn) as Border;
            if (border != null)
            {
                border.Background = new SolidColorBrush(normalColor);
            }
        }

        private void BrowseDirButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Wählen Sie das Zielverzeichnis für Loopback-Aufnahmen:";
                dlg.SelectedPath = _outputDirectory;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputDirectory = dlg.SelectedPath;
                    _outputDirTextBox.Text = _outputDirectory;
                }
            }
        }

        private void AddRecordingToHistory(string filePath, TimeSpan duration, long fileSize, bool isNewRecording = true)
        {
            string fileName = Path.GetFileName(filePath);
            string durStr = string.Format("{0:00}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
            double mb = (double)fileSize / (1024 * 1024);
            string sizeStr = string.Format("{0:0.0} MB", mb);

            // For existing recordings, show file creation time instead of current time
            string timeStr;
            if (isNewRecording)
            {
                timeStr = DateTime.Now.ToString("HH:mm:ss");
            }
            else
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    timeStr = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm");
                }
                catch
                {
                    timeStr = "Unbekannt";
                }
            }

            var item = new RecordingItem
            {
                FilePath = filePath,
                FileName = fileName,
                Timestamp = timeStr,
                Duration = durStr,
                FileSize = sizeStr
            };
            _recordingsList.Insert(0, item);

            // Create UI item
            var grid = new Grid { Margin = new Thickness(4, 6, 4, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            var nameBlock = new TextBlock
            {
                Text = fileName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            var metaBlock = new TextBlock
            {
                Text = string.Format("Dauer: {0} • Größe: {1} • {2}", durStr, sizeStr, timeStr),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(nameBlock);
            infoStack.Children.Add(metaBlock);
            grid.Children.Add(infoStack);

            var actionsStack = new StackPanel { Orientation = Orientation.Horizontal };

            var btnPlay = CreateStyledButton("▶ Abspielen", Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 28);
            btnPlay.Margin = new Thickness(0, 0, 6, 0);
            btnPlay.Click += (s, ev) =>
            {
                if (_player.IsPlaying && _player.CurrentFile == filePath)
                {
                    _player.Stop();
                    btnPlay.Content = "▶ Abspielen";
                }
                else
                {
                    _player.Play(filePath);
                    btnPlay.Content = "⏹ Stopp";
                }
            };
            _player.PlaybackStopped += (s, ev) =>
            {
                Dispatcher.InvokeAsync(() => btnPlay.Content = "▶ Abspielen");
            };
            actionsStack.Children.Add(btnPlay);

            var btnMidi = CreateStyledButton("🎹 MIDI", Color.FromRgb(147, 51, 234), Color.FromRgb(126, 34, 206), 28);
            btnMidi.Margin = new Thickness(6, 0, 0, 0);
            btnMidi.Click += (s, ev) =>
            {
                SwitchToMidiTab(filePath);
            };
            actionsStack.Children.Add(btnMidi);

            var btnConvert = CreateStyledButton("🔄 Konverter", Color.FromRgb(34, 197, 94), Color.FromRgb(22, 163, 74), 28);
            btnConvert.Margin = new Thickness(6, 0, 0, 0);
            btnConvert.Click += (s, ev) =>
            {
                OpenInConverter(filePath);
            };
            actionsStack.Children.Add(btnConvert);

            Grid.SetColumn(actionsStack, 1);
            grid.Children.Add(actionsStack);

            _historyListBox.Items.Insert(0, grid);
        }

        private void SwitchToMidiTab(string filePath = null)
        {
            if (_midiTabContent != null)
            {
                _recordTabContent.Visibility = Visibility.Collapsed;
                _converterTabContent.Visibility = Visibility.Collapsed;
                _arrangeTabContent.Visibility = Visibility.Collapsed;
                _midiTabContent.Visibility = Visibility.Visible;
                _btnTabRecord.Opacity = 0.6;
                _btnTabConvert.Opacity = 0.6;
                _btnTabArrange.Opacity = 0.6;
                _btnTabMidi.Opacity = 1.0;

                if (!string.IsNullOrEmpty(filePath))
                {
                    _midiInputTextBox.Text = filePath;
                    string outDir = Path.GetDirectoryName(filePath);
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    _midiOutputTextBox.Text = Path.Combine(outDir, baseName + ".mid");
                    RunPreAnalysis(filePath);
                }
            }
        }

        private void SwitchToRecordTab()
        {
            if (_recordTabContent != null)
            {
                _midiTabContent.Visibility = Visibility.Collapsed;
                _converterTabContent.Visibility = Visibility.Collapsed;
                _arrangeTabContent.Visibility = Visibility.Collapsed;
                _recordTabContent.Visibility = Visibility.Visible;
                _btnTabMidi.Opacity = 0.6;
                _btnTabConvert.Opacity = 0.6;
                _btnTabArrange.Opacity = 0.6;
                _btnTabRecord.Opacity = 1.0;
            }
        }

        private void SwitchToConverterTab()
        {
            if (_converterTabContent != null)
            {
                _recordTabContent.Visibility = Visibility.Collapsed;
                _midiTabContent.Visibility = Visibility.Collapsed;
                _arrangeTabContent.Visibility = Visibility.Collapsed;
                _converterTabContent.Visibility = Visibility.Visible;
                _btnTabRecord.Opacity = 0.6;
                _btnTabMidi.Opacity = 0.6;
                _btnTabArrange.Opacity = 0.6;
                _btnTabConvert.Opacity = 1.0;
            }
        }

        private void SwitchToArrangeTab()
        {
            if (_arrangeTabContent != null)
            {
                _recordTabContent.Visibility = Visibility.Collapsed;
                _midiTabContent.Visibility = Visibility.Collapsed;
                _converterTabContent.Visibility = Visibility.Collapsed;
                _arrangeTabContent.Visibility = Visibility.Visible;
                _btnTabRecord.Opacity = 0.6;
                _btnTabMidi.Opacity = 0.6;
                _btnTabConvert.Opacity = 0.6;
                _btnTabArrange.Opacity = 1.0;
            }
        }

        private void SelectMidiInputFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audiodateien (*.wav;*.mp3;*.ogg;*.flac;*.aac)|*.wav;*.mp3;*.ogg;*.flac;*.aac|WAV Dateien (*.wav)|*.wav|Alle Dateien (*.*)|*.*",
                Title = "Audiodatei für MIDI-Konvertierung auswählen"
            };
            if (dlg.ShowDialog() == true)
            {
                _midiInputTextBox.Text = dlg.FileName;
                string dir = Path.GetDirectoryName(dlg.FileName);
                string baseName = Path.GetFileNameWithoutExtension(dlg.FileName);
                _midiOutputTextBox.Text = Path.Combine(dir, baseName + ".mid");
                RunPreAnalysis(dlg.FileName);
            }
        }

        private void SelectMidiOutputFile()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Standard MIDI Dateien (*.mid)|*.mid|Alle Dateien (*.*)|*.*",
                Title = "Speicherort für MIDI-Datei wählen"
            };
            if (!string.IsNullOrEmpty(_midiOutputTextBox.Text))
            {
                try
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_midiOutputTextBox.Text);
                    dlg.FileName = Path.GetFileName(_midiOutputTextBox.Text);
                }
                catch { }
            }
            if (dlg.ShowDialog() == true)
            {
                _midiOutputTextBox.Text = dlg.FileName;
            }
        }

        private void ConvertAudioToMidi()
        {
            string inFile = _midiInputTextBox.Text.Trim();
            string outFile = _midiOutputTextBox.Text.Trim();

            if (string.IsNullOrEmpty(inFile) || !File.Exists(inFile))
            {
                MessageBox.Show(this, "Bitte wählen Sie eine gültige Eingangs-Audiodatei aus.", "Datei fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(outFile))
            {
                string dir = Path.GetDirectoryName(inFile);
                outFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(inFile) + ".mid");
                _midiOutputTextBox.Text = outFile;
            }

            _btnConvertMidi.IsEnabled = false;
            _midiLogTextBox.Clear();
            _midiLogTextBox.AppendText(string.Format("[{0}] Starte Audio zu MIDI Analyse...\n", DateTime.Now.ToString("HH:mm:ss")));
            _midiLogTextBox.AppendText(string.Format("Audio: {0}\n", inFile));
            _midiLogTextBox.AppendText(string.Format("MIDI : {0}\n", outFile));

            var opts = new AudioToMidiOptions
            {
                EnergyThresholdDb = _midiThresholdSlider != null ? _midiThresholdSlider.Value : -42.0,
                MinNoteDurationSec = _midiMinDurationSlider != null ? _midiMinDurationSlider.Value : 0.08,
                TempoBpm = _midiBpmSlider != null ? (int)Math.Round(_midiBpmSlider.Value) : 120,
                AddDrums = _midiAddDrumsCheckBox != null && _midiAddDrumsCheckBox.IsChecked == true,
                AddBass = _midiAddBassCheckBox != null && _midiAddBassCheckBox.IsChecked == true,
                AddChords = _midiAddChordsCheckBox != null && _midiAddChordsCheckBox.IsChecked == true,
                PreferAiCliIfAvailable = _midiUseCliCheckBox != null && _midiUseCliCheckBox.IsChecked == true,
                YinThreshold = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.YinThreshold : 0.18,
                WindowSize = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.WindowSize : 2048,
                HopSize = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.HopSize : 512,
                MedianFilterSize = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.MedianFilterSize : 5,
                MinPitchHz = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.MinPitchHz : 55.0,
                MaxPitchHz = _lastPreAnalysis != null ? _lastPreAnalysis.RecommendedOptions.MaxPitchHz : 1760.0
            };

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = AudioToMidiConverter.Convert(inFile, outFile, opts, msg =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        _midiLogTextBox.AppendText(string.Format("[{0}] {1}\n", DateTime.Now.ToString("HH:mm:ss"), msg));
                        _midiLogTextBox.ScrollToEnd();
                    });
                });

                Dispatcher.InvokeAsync(() =>
                {
                    _btnConvertMidi.IsEnabled = true;
                    if (result.Success)
                    {
                        _midiLogTextBox.AppendText(string.Format("\n=== ERFOLG: {0} Noten exportiert ({1}) ===\n", result.NoteCount, result.MethodUsed));
                        if (result.Notes != null && result.Notes.Count > 0)
                        {
                            _midiLogTextBox.AppendText("Erste erkannte Noten:\n");
                            int showCount = Math.Min(12, result.Notes.Count);
                            for (int i = 0; i < showCount; i++)
                            {
                                var n = result.Notes[i];
                                _midiLogTextBox.AppendText(string.Format("  {0,2}. Note: {1,-4} (MIDI {2,3}) | Zeit: {3:0.00}s - {4:0.00}s | Vel: {5}\n",
                                    i + 1, n.NoteName, n.NoteNumber, n.StartTimeSec, n.StartTimeSec + n.DurationSec, n.Velocity));
                            }
                            if (result.Notes.Count > showCount)
                            {
                                _midiLogTextBox.AppendText(string.Format("  ... und {0} weitere Noten.\n", result.Notes.Count - showCount));
                            }
                        }
                        _midiLogTextBox.AppendText("\n💡 Tipp: Sie können diese MIDI-Datei im Reiter '🔄 Konverter' mit dem integrierten Synthesizer in WAV/MP3 umwandeln.\n");
                    }
                    else
                    {
                        _midiLogTextBox.AppendText(string.Format("\n[FEHLER] {0}\n", result.ErrorMessage));
                        MessageBox.Show(this, "Fehler bei Konvertierung: " + result.ErrorMessage, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });
        }


        private void UpdateMidiCliStatusBadge()
        {
            if (_midiCliStatusText == null) return;
            string cli = AudioToMidiConverter.FindExternalMidiCli();
            if (!string.IsNullOrEmpty(cli))
            {
                _midiCliStatusText.Text = "✓ Aktiv: " + Path.GetFileName(cli);
                _midiCliStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                if (_btnMidiDownloadCli != null) _btnMidiDownloadCli.Content = "⚙️ Downloads & Codecs";
            }
            else
            {
                _midiCliStatusText.Text = "ℹ Standard (YIN aktiv)";
                _midiCliStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                if (_btnMidiDownloadCli != null) _btnMidiDownloadCli.Content = "📥 KI / Codecs verwalten";
            }
        }

        public void OpenInConverter(string filePath)
        {
            SwitchToConverterTab();
            if (_converterTab != null)
            {
                _converterTab.SetInputFile(filePath);
            }
        }


        private void RunPreAnalysis(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            _midiLogTextBox.Clear();
            _midiLogTextBox.AppendText(string.Format("[{0}] Automatische Voranalyse gestartet...\n", DateTime.Now.ToString("HH:mm:ss")));
            _midiLogTextBox.AppendText(string.Format("Datei: {0}\n\n", Path.GetFileName(filePath)));

            _btnConvertMidi.IsEnabled = false;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var analysis = AudioToMidiConverter.PreAnalyze(filePath, msg =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        _midiLogTextBox.AppendText(string.Format("[{0}] {1}\n", DateTime.Now.ToString("HH:mm:ss"), msg));
                        _midiLogTextBox.ScrollToEnd();
                    });
                });

                Dispatcher.InvokeAsync(() =>
                {
                    _btnConvertMidi.IsEnabled = true;

                    if (analysis.Success && analysis.RecommendedOptions != null)
                    {
                        _lastPreAnalysis = analysis;
                        var opts = analysis.RecommendedOptions;

                        // Auto-set sliders to recommended values
                        if (_midiThresholdSlider != null)
                            _midiThresholdSlider.Value = opts.EnergyThresholdDb;
                        if (_midiMinDurationSlider != null)
                            _midiMinDurationSlider.Value = opts.MinNoteDurationSec;
                        if (_midiBpmSlider != null)
                            _midiBpmSlider.Value = opts.TempoBpm;

                        _midiLogTextBox.AppendText("\n=== Parameter automatisch optimiert ===\n");
                        _midiLogTextBox.AppendText("Die Slider wurden auf die empfohlenen Werte gesetzt.\n");
                        _midiLogTextBox.AppendText("Sie können die Werte manuell anpassen und dann konvertieren.\n");
                    }
                    else
                    {
                        _midiLogTextBox.AppendText(string.Format("\n[Warnung] Voranalyse: {0}\n", analysis.ErrorMessage ?? "unbekannter Fehler"));
                        _midiLogTextBox.AppendText("Standardparameter werden verwendet.\n");
                    }
                    _midiLogTextBox.ScrollToEnd();
                });
            });
        }

        private void SelectArrangeInputFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audiodateien (*.wav;*.mp3;*.ogg;*.flac;*.aac)|*.wav;*.mp3;*.ogg;*.flac;*.aac|WAV Dateien (*.wav)|*.wav|Alle Dateien (*.*)|*.*",
                Title = "Audiodatei für Bearbeitung auswählen"
            };
            if (dlg.ShowDialog() == true)
            {
                _arrangeInputTextBox.Text = dlg.FileName;
                string dir = Path.GetDirectoryName(dlg.FileName);
                string baseName = Path.GetFileNameWithoutExtension(dlg.FileName);
                _arrangeOutputTextBox.Text = Path.Combine(dir, baseName + "_arranged.wav");
            }
        }

        private void SelectArrangeOutputFile()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "WAV Dateien (*.wav)|*.wav|MP3 Dateien (*.mp3)|*.mp3|FLAC Dateien (*.flac)|*.flac|Alle Dateien (*.*)|*.*",
                Title = "Speicherort für bearbeitete Audiodatei wählen"
            };
            if (!string.IsNullOrEmpty(_arrangeOutputTextBox.Text))
            {
                try
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_arrangeOutputTextBox.Text);
                    dlg.FileName = Path.GetFileName(_arrangeOutputTextBox.Text);
                }
                catch { }
            }
            if (dlg.ShowDialog() == true)
            {
                _arrangeOutputTextBox.Text = dlg.FileName;
            }
        }

        private void UpdateCompressorVisibility()
        {
            if (_arrangeCompressCheckBox == null) return;
            bool isChecked = _arrangeCompressCheckBox.IsChecked == true;

            // Find the compressor controls in the visual tree
            var effectsStack = _arrangeCompressCheckBox.Parent as StackPanel;
            if (effectsStack != null)
            {
                foreach (var child in effectsStack.Children)
                {
                    Grid grid = child as Grid;
                    if (grid != null && grid.Margin.Left == 20)
                    {
                        // This is the compressor ratio header
                        bool foundRatio = false;
                        foreach (var gridChild in grid.Children)
                        {
                            TextBlock tb = gridChild as TextBlock;
                            if (tb != null && tb.Text == "Kompressions-Verhältnis:")
                            {
                                foundRatio = true;
                                break;
                            }
                        }
                        if (foundRatio)
                        {
                            grid.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        Slider slider = child as Slider;
                        if (slider != null && slider.Margin.Left == 20 && slider == _arrangeCompressRatioSlider)
                        {
                            slider.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void UpdateReverbVisibility()
        {
            if (_arrangeReverbCheckBox == null) return;
            bool isChecked = _arrangeReverbCheckBox.IsChecked == true;

            // Find the reverb controls in the visual tree
            var effectsStack = _arrangeReverbCheckBox.Parent as StackPanel;
            if (effectsStack != null)
            {
                foreach (var child in effectsStack.Children)
                {
                    Grid grid = child as Grid;
                    if (grid != null && grid.Margin.Left == 20)
                    {
                        // This is the reverb amount header
                        bool foundReverb = false;
                        foreach (var gridChild in grid.Children)
                        {
                            TextBlock tb = gridChild as TextBlock;
                            if (tb != null && tb.Text == "Hall-Intensität:")
                            {
                                foundReverb = true;
                                break;
                            }
                        }
                        if (foundReverb)
                        {
                            grid.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        Slider slider = child as Slider;
                        if (slider != null && slider.Margin.Left == 20 && slider == _arrangeReverbAmountSlider)
                        {
                            slider.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void ProcessArrangeAudio()
        {
            string inFile = _arrangeInputTextBox.Text.Trim();
            string outFile = _arrangeOutputTextBox.Text.Trim();

            if (string.IsNullOrEmpty(inFile) || !File.Exists(inFile))
            {
                MessageBox.Show(this, "Bitte wählen Sie eine gültige Eingangs-Audiodatei aus.", "Datei fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(outFile))
            {
                string dir = Path.GetDirectoryName(inFile);
                outFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(inFile) + "_arranged.wav");
                _arrangeOutputTextBox.Text = outFile;
            }

            _btnProcessArrange.IsEnabled = false;
            _arrangeLogTextBox.Clear();
            _arrangeLogTextBox.AppendText(string.Format("[{0}] Starte professionelle Audio-Bearbeitung...\n", DateTime.Now.ToString("HH:mm:ss")));
            _arrangeLogTextBox.AppendText(string.Format("Eingabe: {0}\n", inFile));
            _arrangeLogTextBox.AppendText(string.Format("Ausgabe: {0}\n\n", outFile));

            // Collect settings
            double bassGain = _eqBassSlider != null ? _eqBassSlider.Value : 0.0;
            double midGain = _eqMidSlider != null ? _eqMidSlider.Value : 0.0;
            double trebleGain = _eqTrebleSlider != null ? _eqTrebleSlider.Value : 0.0;
            double masterVolume = _masterVolumeSlider != null ? _masterVolumeSlider.Value : 0.0;
            bool normalize = _arrangeNormalizeCheckBox != null && _arrangeNormalizeCheckBox.IsChecked == true;
            bool compress = _arrangeCompressCheckBox != null && _arrangeCompressCheckBox.IsChecked == true;
            double compressRatio = _arrangeCompressRatioSlider != null ? _arrangeCompressRatioSlider.Value : 4.0;
            bool reverb = _arrangeReverbCheckBox != null && _arrangeReverbCheckBox.IsChecked == true;
            double reverbAmount = _arrangeReverbAmountSlider != null ? _arrangeReverbAmountSlider.Value : 30.0;

            _arrangeLogTextBox.AppendText("=== Einstellungen ===\n");
            _arrangeLogTextBox.AppendText(string.Format("Bass: {0:+0;-0;0} dB, Mitten: {1:+0;-0;0} dB, Höhen: {2:+0;-0;0} dB\n", bassGain, midGain, trebleGain));
            _arrangeLogTextBox.AppendText(string.Format("Master-Lautstärke: {0:+0;-0;0} dB\n", masterVolume));
            _arrangeLogTextBox.AppendText(string.Format("Normalisierung: {0}\n", normalize ? "Ja" : "Nein"));
            _arrangeLogTextBox.AppendText(string.Format("Kompressor: {0}", compress ? string.Format("Ja ({0:0.0}:1)\n", compressRatio) : "Nein\n"));
            _arrangeLogTextBox.AppendText(string.Format("Hall-Effekt: {0}\n\n", reverb ? string.Format("Ja ({0:0}%)\n", reverbAmount) : "Nein\n"));

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        _arrangeLogTextBox.AppendText(string.Format("[{0}] Lade Audiodatei...\n", DateTime.Now.ToString("HH:mm:ss")));
                        _arrangeLogTextBox.ScrollToEnd();
                    });

                    // Use AudioConverterService to process the file
                    var processor = new AudioArrangeProcessor();
                    var result = processor.ProcessAudio(inFile, outFile, new AudioArrangeOptions
                    {
                        BassGainDb = bassGain,
                        MidGainDb = midGain,
                        TrebleGainDb = trebleGain,
                        MasterVolumeDb = masterVolume,
                        Normalize = normalize,
                        Compress = compress,
                        CompressionRatio = compressRatio,
                        Reverb = reverb,
                        ReverbAmount = reverbAmount / 100.0
                    }, msg =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            _arrangeLogTextBox.AppendText(string.Format("[{0}] {1}\n", DateTime.Now.ToString("HH:mm:ss"), msg));
                            _arrangeLogTextBox.ScrollToEnd();
                        });
                    });

                    Dispatcher.InvokeAsync(() =>
                    {
                        _btnProcessArrange.IsEnabled = true;
                        if (result.Success)
                        {
                            _arrangeLogTextBox.AppendText(string.Format("\n✅ ERFOLG: Datei erfolgreich bearbeitet!\n"));
                            _arrangeLogTextBox.AppendText(string.Format("Ausgabedatei: {0}\n", outFile));

                            var fileInfo = new FileInfo(outFile);
                            if (fileInfo.Exists)
                            {
                                _arrangeLogTextBox.AppendText(string.Format("Dateigröße: {0:0.0} MB\n", fileInfo.Length / (1024.0 * 1024.0)));
                            }

                            MessageBox.Show(this, "Audio wurde erfolgreich bearbeitet!\n\n" + outFile, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _arrangeLogTextBox.AppendText(string.Format("\n❌ FEHLER: {0}\n", result.ErrorMessage));
                            MessageBox.Show(this, "Fehler bei der Bearbeitung:\n\n" + result.ErrorMessage, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        _arrangeLogTextBox.ScrollToEnd();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        _btnProcessArrange.IsEnabled = true;
                        _arrangeLogTextBox.AppendText(string.Format("\n❌ AUSNAHME: {0}\n", ex.Message));
                        MessageBox.Show(this, "Fehler:\n\n" + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }
    }
}
