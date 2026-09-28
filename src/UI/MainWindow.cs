using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        // Navigation
        private Button _tabBtnRecord;
        private Button _tabBtnConvert;
        private Button _tabBtnAnalyze;
        private Button _tabBtnSettings;

        private Grid _viewRecord;
        private Grid _viewConvert;
        private Grid _viewAnalyze;
        private Grid _viewSettings;

        // Header
        private TextBlock _statusPillText;
        private Border _statusPillBorder;
        private Button _langToggleButton;

        // Tab 1: Recorder Controls
        private ComboBox _deviceTypeComboBox;
        private ComboBox _deviceComboBox;
        private Button _refreshDevicesButton;
        private TextBlock _deviceFormatText;
        private VuMeterControl _vuMeter;
        private TextBlock _audioActivityBadge;
        private TextBlock _timerText;
        private TextBlock _fileSizeText;
        private TextBlock _formatInfoText;
        private Button _btnRecord;
        private Button _btnPause;
        private Button _btnOpenFolder;
        private ListBox _historyListBox;

        // Tab 2: Converter Controls
        private TextBox _convInputTextBox;
        private Button _convBrowseInputBtn;
        private ComboBox _convFormatComboBox;
        private ComboBox _convBitrateComboBox;
        private Button _convStartBtn;
        private TextBox _convLogTextBox;

        // Tab 3: Analyzer Controls
        private TextBox _analyzeFileTextBox;
        private Button _analyzeBrowseBtn;
        private Button _analyzeStartBtn;
        private TextBlock _anaDurationVal;
        private TextBlock _anaFormatVal;
        private TextBlock _anaPeakVal;
        private TextBlock _anaRmsVal;
        private TextBlock _anaClipVal;
        private TextBlock _anaSilenceVal;
        private TextBlock _anaDynVal;

        // Tab 4: Settings Controls
        private TextBox _outputDirTextBox;
        private Button _browseDirButton;
        private TextBox _prefixTextBox;
        private ComboBox _formatComboBox;
        private TextBlock _ffmpegStatusText;

        private string _outputDirectory;
        private DispatcherTimer _uiTimer;
        private TimeSpan _currentDuration;
        public MainWindow()
        {
            _engine = new WasapiLoopbackEngine();
            _player = new SimpleAudioPlayer();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            string defaultRecDir = Path.Combine(appData, "ER-Audio-Studio");
            if (!Directory.Exists(defaultRecDir))
            {
                try { Directory.CreateDirectory(defaultRecDir); } catch { }
            }
            _outputDirectory = defaultRecDir;

            InitializeWindow();
            BuildUi();
            HookEvents();
            LoadAudioDevices();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void InitializeWindow()
        {
            Title = "ER Audio Studio";
            Width = 840;
            Height = 840;
            MinWidth = 760;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // #0F172A

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(baseDir, "..", "assets", "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = BitmapFrame.Create(new Uri(Path.GetFullPath(iconPath)));
                }
            }
            catch { }

            Loaded += (s, e) =>
            {
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    int darkMode = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
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
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Top Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Navigation Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: Active Tab Content
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Status Footer

            // --- 0. TOP HEADER ---
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 14, 24, 14)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brandStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logoPath = Path.Combine(baseDir, "..", "assets", "logo.png");
                if (File.Exists(logoPath))
                {
                    var logoImg = new Image
                    {
                        Source = new BitmapImage(new Uri(Path.GetFullPath(logoPath))),
                        Width = 44,
                        Height = 44,
                        Margin = new Thickness(0, 0, 14, 0)
                    };
                    brandStack.Children.Add(logoImg);
                }
            }
            catch { }

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleText = new TextBlock
            {
                Text = Loc.Get("AppTitle"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            var subText = new TextBlock
            {
                Text = Loc.Get("AppSubtitle"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(titleText);
            textStack.Children.Add(subText);
            brandStack.Children.Add(textStack);
            headerGrid.Children.Add(brandStack);

            var rightHeaderStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _statusPillBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 12, 0)
            };
            _statusPillText = new TextBlock
            {
                Text = Loc.Get("StatusReady"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            _statusPillBorder.Child = _statusPillText;
            rightHeaderStack.Children.Add(_statusPillBorder);

            _langToggleButton = CreateStyledButton(Loc.Current == ErAudioTool.UI.Language.Deutsch ? "🇩🇪 DE" : "🇬🇧 EN", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 32);
            _langToggleButton.Click += (s, e) =>
            {
                Loc.Current = Loc.Current == ErAudioTool.UI.Language.Deutsch ? ErAudioTool.UI.Language.English : ErAudioTool.UI.Language.Deutsch;
                _langToggleButton.Content = Loc.Current == ErAudioTool.UI.Language.Deutsch ? "🇩🇪 DE" : "🇬🇧 EN";
                UpdateLocalizedUi();
            };
            rightHeaderStack.Children.Add(_langToggleButton);

            Grid.SetColumn(rightHeaderStack, 1);
            headerGrid.Children.Add(rightHeaderStack);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // --- 1. NAVIGATION TAB BAR ---
            var navBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 10, 24, 10)
            };

            var navStack = new StackPanel { Orientation = Orientation.Horizontal };
            _tabBtnRecord = CreateTabButton(Loc.Get("TabRecord"), true);
            _tabBtnConvert = CreateTabButton(Loc.Get("TabConvert"), false);
            _tabBtnAnalyze = CreateTabButton(Loc.Get("TabAnalyze"), false);
            _tabBtnSettings = CreateTabButton(Loc.Get("TabSettings"), false);

            _tabBtnRecord.Click += (s, e) => SwitchTab(0);
            _tabBtnConvert.Click += (s, e) => SwitchTab(1);
            _tabBtnAnalyze.Click += (s, e) => SwitchTab(2);
            _tabBtnSettings.Click += (s, e) => SwitchTab(3);

            navStack.Children.Add(_tabBtnRecord);
            navStack.Children.Add(_tabBtnConvert);
            navStack.Children.Add(_tabBtnAnalyze);
            navStack.Children.Add(_tabBtnSettings);

            navBorder.Child = navStack;
            Grid.SetRow(navBorder, 1);
            rootGrid.Children.Add(navBorder);

            // --- 2. ACTIVE TAB CONTENTS ---
            var viewsHost = new Grid();
            _viewRecord = BuildRecorderView();
            _viewConvert = BuildConverterView();
            _viewAnalyze = BuildAnalyzerView();
            _viewSettings = BuildSettingsView();

            viewsHost.Children.Add(_viewRecord);
            viewsHost.Children.Add(_viewConvert);
            viewsHost.Children.Add(_viewAnalyze);
            viewsHost.Children.Add(_viewSettings);

            SwitchTab(0);

            Grid.SetRow(viewsHost, 2);
            rootGrid.Children.Add(viewsHost);

            // --- 3. STATUS FOOTER ---
            var footerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 8, 24, 8)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var versionInfo = new TextBlock
            {
                Text = "ER-Audio-Studio v2.0 • Native Windows CoreAudio (WASAPI) Engine",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            footerGrid.Children.Add(versionInfo);

            var keyHint = new TextBlock
            {
                Text = "[LEERTASTE] Aufnahme Start/Stop • 100% Lokal & Sicher",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            Grid.SetColumn(keyHint, 1);
            footerGrid.Children.Add(keyHint);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 3);
            rootGrid.Children.Add(footerBorder);

            Content = rootGrid;

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Space && !IsAnyTextBoxFocused())
                {
                    BtnRecord_Click(this, null);
                    e.Handled = true;
                }
            };
        }
        private bool IsAnyTextBoxFocused()
        {
            return (_outputDirTextBox != null && _outputDirTextBox.IsFocused) ||
                   (_prefixTextBox != null && _prefixTextBox.IsFocused) ||
                   (_convInputTextBox != null && _convInputTextBox.IsFocused) ||
                   (_analyzeFileTextBox != null && _analyzeFileTextBox.IsFocused);
        }

        private Button CreateTabButton(string title, bool isActive)
        {
            var btn = new Button
            {
                Content = title,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(16, 0, 16, 0)
            };

            Color bg = isActive ? Color.FromRgb(59, 130, 246) : Color.FromRgb(30, 41, 59);
            Color hover = isActive ? Color.FromRgb(37, 99, 235) : Color.FromRgb(51, 65, 85);

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
            factory.SetValue(Border.PaddingProperty, new Thickness(14, 0, 14, 0));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentPresenter);

            var triggerIsMouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            triggerIsMouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "border"));

            template.Triggers.Add(triggerIsMouseOver);
            template.VisualTree = factory;
            btn.Template = template;

            return btn;
        }

        private void SwitchTab(int tabIndex)
        {
            _viewRecord.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            _viewConvert.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            _viewAnalyze.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            _viewSettings.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

            UpdateTabButtonState(_tabBtnRecord, tabIndex == 0);
            UpdateTabButtonState(_tabBtnConvert, tabIndex == 1);
            UpdateTabButtonState(_tabBtnAnalyze, tabIndex == 2);
            UpdateTabButtonState(_tabBtnSettings, tabIndex == 3);
        }

        private void UpdateTabButtonState(Button btn, bool isActive)
        {
            btn.Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btn.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
            Color bg = isActive ? Color.FromRgb(59, 130, 246) : Color.FromRgb(30, 41, 59);
            Color hover = isActive ? Color.FromRgb(37, 99, 235) : Color.FromRgb(51, 65, 85);
            ApplyButtonColor(btn, bg, hover);
        }
        // ================= TAB 1: RECORDER VIEW =================
        private Grid BuildRecorderView()
        {
            var grid = new Grid();
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16)
            };
            var contentStack = new StackPanel();

            // 1. Device Selection Card
            var devTypeGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            devTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            devTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var typeLabel = new TextBlock
            {
                Text = "Quellenart:",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            devTypeGrid.Children.Add(typeLabel);

            _deviceTypeComboBox = new ComboBox
            {
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _deviceTypeComboBox.Items.Add(Loc.Get("DeviceTypeRender"));
            _deviceTypeComboBox.Items.Add(Loc.Get("DeviceTypeCapture"));
            _deviceTypeComboBox.SelectedIndex = 0;
            _deviceTypeComboBox.SelectionChanged += (s, e) => LoadAudioDevices();
            Grid.SetColumn(_deviceTypeComboBox, 1);
            devTypeGrid.Children.Add(_deviceTypeComboBox);

            var devGrid = new Grid();
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _deviceComboBox = new ComboBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _deviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
            devGrid.Children.Add(_deviceComboBox);

            _refreshDevicesButton = CreateStyledButton(Loc.Get("RefreshDevices"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            _refreshDevicesButton.Click += (s, e) => LoadAudioDevices();
            Grid.SetColumn(_refreshDevicesButton, 1);
            devGrid.Children.Add(_refreshDevicesButton);

            var devStack = new StackPanel();
            devStack.Children.Add(devTypeGrid);
            devStack.Children.Add(devGrid);

            _deviceFormatText = new TextBlock
            {
                Text = "Format: Ermittle Gerätedaten...",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            devStack.Children.Add(_deviceFormatText);
            contentStack.Children.Add(CreateCard(Loc.Get("DeviceSection"), devStack));

            // 2. VU-Meter Card
            var meterStack = new StackPanel();
            _vuMeter = new VuMeterControl { Height = 58 };
            meterStack.Children.Add(_vuMeter);

            var meterFooter = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            meterFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            meterFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var meterDesc = new TextBlock
            {
                Text = "Stereo-Echtzeit-Pegel (L/R) in Dezibel (dBFS)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            meterFooter.Children.Add(meterDesc);

            _audioActivityBadge = new TextBlock
            {
                Text = Loc.Get("AudioActive"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            Grid.SetColumn(_audioActivityBadge, 1);
            meterFooter.Children.Add(_audioActivityBadge);

            meterStack.Children.Add(meterFooter);
            contentStack.Children.Add(CreateCard(Loc.Get("LiveMeter"), meterStack));

            // 3. Recording Hero Card
            var heroStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            _timerText = new TextBlock
            {
                Text = "00:00:00.0",
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
            heroStack.Children.Add(_timerText);

            var statsGrid = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };

            _fileSizeText = new TextBlock
            {
                Text = "0.0 MB",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 16, 0)
            };
            statsGrid.Children.Add(_fileSizeText);

            _formatInfoText = new TextBlock
            {
                Text = "WAV 16-Bit PCM • 48 kHz",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            statsGrid.Children.Add(_formatInfoText);
            heroStack.Children.Add(statsGrid);

            var btnBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _btnRecord = CreateHeroButton(Loc.Get("StartRecord"), Color.FromRgb(225, 29, 72), Color.FromRgb(190, 18, 60), 50, 200);
            _btnRecord.Click += BtnRecord_Click;
            btnBar.Children.Add(_btnRecord);

            _btnPause = CreateHeroButton(Loc.Get("PauseRecord"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 50, 120);
            _btnPause.Margin = new Thickness(10, 0, 0, 0);
            _btnPause.IsEnabled = false;
            _btnPause.Click += BtnPause_Click;
            btnBar.Children.Add(_btnPause);

            _btnOpenFolder = CreateHeroButton(Loc.Get("Explorer"), Color.FromRgb(30, 41, 59), Color.FromRgb(51, 65, 85), 50, 120);
            _btnOpenFolder.Margin = new Thickness(10, 0, 0, 0);
            _btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(_outputDirectory)) Process.Start("explorer.exe", _outputDirectory);
            };
            btnBar.Children.Add(_btnOpenFolder);

            heroStack.Children.Add(btnBar);
            contentStack.Children.Add(CreateCard("AUFNAHME-STEUERUNG", heroStack));

            // 4. History Card
            var historyStack = new StackPanel();
            _historyListBox = new ListBox
            {
                Height = 160,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Foreground = Brushes.White,
                Padding = new Thickness(6)
            };
            historyStack.Children.Add(_historyListBox);
            contentStack.Children.Add(CreateCard(Loc.Get("HistorySection"), historyStack));

            scrollViewer.Content = contentStack;
            grid.Children.Add(scrollViewer);
            return grid;
        }
        // ================= TAB 2: CONVERTER VIEW =================
        private Grid BuildConverterView()
        {
            var grid = new Grid();
            var scrollViewer = new ScrollViewer { Padding = new Thickness(24, 16, 24, 16) };
            var stack = new StackPanel();

            var optStack = new StackPanel();
            var fileGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _convInputTextBox = new TextBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 10, 0)
            };
            fileGrid.Children.Add(_convInputTextBox);

            _convBrowseInputBtn = CreateStyledButton(Loc.Get("SelectFile"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            _convBrowseInputBtn.Click += (s, e) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Audiodatei zur Konvertierung auswählen",
                    Filter = "Audiodateien (*.wav;*.mp3;*.flac;*.m4a;*.ogg)|*.wav;*.mp3;*.flac;*.m4a;*.ogg|Alle Dateien (*.*)|*.*"
                };
                if (ofd.ShowDialog() == true)
                {
                    _convInputTextBox.Text = ofd.FileName;
                }
            };
            Grid.SetColumn(_convBrowseInputBtn, 1);
            fileGrid.Children.Add(_convBrowseInputBtn);
            optStack.Children.Add(fileGrid);

            var comboGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var fmtStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            fmtStack.Children.Add(new TextBlock { Text = Loc.Get("TargetFormat"), Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            _convFormatComboBox = new ComboBox { Height = 32 };
            _convFormatComboBox.Items.Add("MP3");
            _convFormatComboBox.Items.Add("WAV");
            _convFormatComboBox.Items.Add("FLAC");
            _convFormatComboBox.Items.Add("OGG");
            _convFormatComboBox.Items.Add("AAC / M4A");
            _convFormatComboBox.SelectedIndex = 0;
            fmtStack.Children.Add(_convFormatComboBox);
            comboGrid.Children.Add(fmtStack);

            var bitStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            bitStack.Children.Add(new TextBlock { Text = Loc.Get("Bitrate"), Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            _convBitrateComboBox = new ComboBox { Height = 32 };
            _convBitrateComboBox.Items.Add("320 kbps (High Quality)");
            _convBitrateComboBox.Items.Add("256 kbps");
            _convBitrateComboBox.Items.Add("192 kbps");
            _convBitrateComboBox.Items.Add("128 kbps (Kompakt)");
            _convBitrateComboBox.SelectedIndex = 0;
            bitStack.Children.Add(_convBitrateComboBox);
            Grid.SetColumn(bitStack, 1);
            comboGrid.Children.Add(bitStack);

            optStack.Children.Add(comboGrid);

            _convStartBtn = CreateHeroButton(Loc.Get("StartConvert"), Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 44, 220);
            _convStartBtn.Click += ConvStartBtn_Click;
            optStack.Children.Add(_convStartBtn);

            stack.Children.Add(CreateCard(Loc.Get("ConvertSection"), optStack));

            var logStack = new StackPanel();
            _convLogTextBox = new TextBox
            {
                Height = 180,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };
            string ffmpegFound = AudioConverterService.FindFfmpeg();
            _convLogTextBox.Text = ffmpegFound != null
                ? string.Format("System bereit.\nFFmpeg gefunden: {0}\n", ffmpegFound)
                : "Systemhinweis: FFmpeg ist nicht im Standard-Pfad vorhanden.\nLegen Sie ffmpeg.exe im Programmordner ab, um MP3/FLAC/OGG Konvertierungen zu aktivieren.\n";

            logStack.Children.Add(_convLogTextBox);
            stack.Children.Add(CreateCard(Loc.Get("ConvertLog"), logStack));

            scrollViewer.Content = stack;
            grid.Children.Add(scrollViewer);
            return grid;
        }

        private async void ConvStartBtn_Click(object sender, RoutedEventArgs e)
        {
            string inFile = _convInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(inFile) || !File.Exists(inFile))
            {
                MessageBox.Show("Bitte wählen Sie zuerst eine gültige Audiodatei aus.", "Datei fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fmtStr = _convFormatComboBox.SelectedItem.ToString().Split(' ')[0].ToLowerInvariant();
            int bitrate = 320;
            if (_convBitrateComboBox.SelectedIndex == 1) bitrate = 256;
            else if (_convBitrateComboBox.SelectedIndex == 2) bitrate = 192;
            else if (_convBitrateComboBox.SelectedIndex == 3) bitrate = 128;

            string outDir = Path.GetDirectoryName(inFile);
            string baseName = Path.GetFileNameWithoutExtension(inFile);
            string outFile = Path.Combine(outDir, string.Format("{0}_converted.{1}", baseName, fmtStr == "aac" ? "m4a" : fmtStr));

            _convStartBtn.IsEnabled = false;
            _statusPillText.Text = Loc.Get("StatusConverting");
            _convLogTextBox.AppendText(string.Format("\nStarte Konvertierung von '{0}' zu {1} ({2}k)...\n", Path.GetFileName(inFile), fmtStr.ToUpper(), bitrate));

            bool success = await Task.Run(() =>
            {
                return AudioConverterService.ConvertAudio(inFile, outFile, fmtStr, bitrate, (msg) =>
                {
                    Dispatcher.InvokeAsync(() => _convLogTextBox.AppendText(msg + "\n"));
                });
            });

            _convStartBtn.IsEnabled = true;
            _statusPillText.Text = Loc.Get("StatusReady");

            if (success)
            {
                _convLogTextBox.AppendText(string.Format("✅ Erfolgreich gespeichert: {0}\n", outFile));
                _convLogTextBox.ScrollToEnd();
            }
        }
        // ================= TAB 3: ANALYZER VIEW =================
        private Grid BuildAnalyzerView()
        {
            var grid = new Grid();
            var scrollViewer = new ScrollViewer { Padding = new Thickness(24, 16, 24, 16) };
            var stack = new StackPanel();

            var chooseStack = new StackPanel();
            var fileGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _analyzeFileTextBox = new TextBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 10, 0)
            };
            fileGrid.Children.Add(_analyzeFileTextBox);

            _analyzeBrowseBtn = CreateStyledButton(Loc.Get("SelectFile"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            _analyzeBrowseBtn.Click += (s, e) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "WAV-Audiodatei zur Analyse auswählen",
                    Filter = "WAV Audio (*.wav)|*.wav|Alle Dateien (*.*)|*.*"
                };
                if (ofd.ShowDialog() == true)
                {
                    _analyzeFileTextBox.Text = ofd.FileName;
                    RunAnalysis(ofd.FileName);
                }
            };
            Grid.SetColumn(_analyzeBrowseBtn, 1);
            fileGrid.Children.Add(_analyzeBrowseBtn);
            chooseStack.Children.Add(fileGrid);

            _analyzeStartBtn = CreateHeroButton("📊 Datei detailliert analysieren", Color.FromRgb(6, 182, 212), Color.FromRgb(14, 165, 233), 40, 240);
            _analyzeStartBtn.Click += (s, e) =>
            {
                string file = _analyzeFileTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(file) && File.Exists(file)) RunAnalysis(file);
            };
            chooseStack.Children.Add(_analyzeStartBtn);

            stack.Children.Add(CreateCard(Loc.Get("AnalyzeSection"), chooseStack));

            var metricsGrid = new Grid();
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 7; i++)
            {
                metricsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            }

            int row = 0;
            _anaDurationVal = AddMetricRow(metricsGrid, row++, "Gesamtdauer:", "-");
            _anaFormatVal = AddMetricRow(metricsGrid, row++, "Abtastrate & Format:", "-");
            _anaPeakVal = AddMetricRow(metricsGrid, row++, Loc.Get("PeakLoudness"), "-");
            _anaRmsVal = AddMetricRow(metricsGrid, row++, Loc.Get("RmsLoudness"), "-");
            _anaClipVal = AddMetricRow(metricsGrid, row++, Loc.Get("ClippedSamples"), "-");
            _anaSilenceVal = AddMetricRow(metricsGrid, row++, Loc.Get("SilencePart"), "-");
            _anaDynVal = AddMetricRow(metricsGrid, row++, Loc.Get("DynamicRange"), "-");

            stack.Children.Add(CreateCard("AKUSTISCHE KENNZAHLEN", metricsGrid));

            scrollViewer.Content = stack;
            grid.Children.Add(scrollViewer);
            return grid;
        }

        private TextBlock AddMetricRow(Grid g, int row, string label, string defaultVal)
        {
            var lbl = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            g.Children.Add(lbl);

            var val = new TextBlock
            {
                Text = defaultVal,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            Grid.SetRow(val, row);
            Grid.SetColumn(val, 1);
            g.Children.Add(val);

            return val;
        }

        private async void RunAnalysis(string wavPath)
        {
            _anaDurationVal.Text = "Berechne...";
            _anaFormatVal.Text = "Lese Chunks...";
            _anaPeakVal.Text = "...";
            _anaRmsVal.Text = "...";
            _anaClipVal.Text = "...";
            _anaSilenceVal.Text = "...";
            _anaDynVal.Text = "...";

            var res = await Task.Run(() => AudioAnalyzer.AnalyzeWavFile(wavPath));

            if (!res.IsValid)
            {
                MessageBox.Show(res.ErrorMessage ?? "Konnte Datei nicht analysieren.", "Analyse-Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _anaDurationVal.Text = string.Format("{0:00}:{1:00}.{2:000}", (int)res.Duration.TotalMinutes, res.Duration.Seconds, res.Duration.Milliseconds);
            _anaFormatVal.Text = string.Format("{0:N0} Hz • {1}-Kanal • {2}-Bit", res.SampleRate, res.Channels, res.BitsPerSample);
            _anaPeakVal.Text = string.Format("{0:0.00} dBFS (L: {1:0.00}, R: {2:0.00})", res.PeakDbfs, res.PeakLevelLeft, res.PeakLevelRight);
            _anaRmsVal.Text = string.Format("{0:0.00} dBFS", res.RmsDbfs);

            if (res.ClippedSamples == 0)
            {
                _anaClipVal.Text = "Keine Übersteuerungen erkannt (0 Samples) ✅";
                _anaClipVal.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                _anaClipVal.Text = string.Format("{0:N0} Samples übersteuert / geclippt ⚠️", res.ClippedSamples);
                _anaClipVal.Foreground = new SolidColorBrush(Color.FromRgb(244, 63, 94));
            }

            _anaSilenceVal.Text = string.Format("{0:0.0}% Stille (< -60 dBFS)", res.SilencePercentage);
            _anaDynVal.Text = res.DynamicRangeDescription ?? "-";
        }
        // ================= TAB 4: SETTINGS VIEW =================
        private Grid BuildSettingsView()
        {
            var grid = new Grid();
            var scrollViewer = new ScrollViewer { Padding = new Thickness(24, 16, 24, 16) };
            var stack = new StackPanel();

            var setStack = new StackPanel();

            setStack.Children.Add(new TextBlock { Text = Loc.Get("OutputDir"), Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            var dirGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _outputDirTextBox = new TextBox
            {
                Text = _outputDirectory,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 10, 0)
            };
            dirGrid.Children.Add(_outputDirTextBox);

            _browseDirButton = CreateStyledButton(Loc.Get("Browse"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 36);
            _browseDirButton.Click += BrowseDirButton_Click;
            Grid.SetColumn(_browseDirButton, 1);
            dirGrid.Children.Add(_browseDirButton);
            setStack.Children.Add(dirGrid);

            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pfxStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            pfxStack.Children.Add(new TextBlock { Text = Loc.Get("FilePrefix"), Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            _prefixTextBox = new TextBox
            {
                Text = "Studio_Record",
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0)
            };
            pfxStack.Children.Add(_prefixTextBox);
            row2.Children.Add(pfxStack);

            var fmtStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            fmtStack.Children.Add(new TextBlock { Text = "Standard-Aufnahmeformat:", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            _formatComboBox = new ComboBox { Height = 36 };
            _formatComboBox.Items.Add("WAV 16-Bit PCM (Verlustfrei, hohe Kompatibilität)");
            _formatComboBox.Items.Add("WAV 32-Bit IEEE Float (Studiostandard, maximaler Headroom)");
            _formatComboBox.SelectedIndex = 0;
            fmtStack.Children.Add(_formatComboBox);
            Grid.SetColumn(fmtStack, 1);
            row2.Children.Add(fmtStack);

            setStack.Children.Add(row2);

            _ffmpegStatusText = new TextBlock
            {
                Text = AudioConverterService.FindFfmpeg() != null ? "✅ Externe Audio-Tools: FFmpeg gefunden und aktiv" : "ℹ️ Externe Audio-Tools: FFmpeg nicht gefunden (WAV ist nativ aktiv)",
                FontSize = 12,
                Foreground = AudioConverterService.FindFfmpeg() != null ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            setStack.Children.Add(_ffmpegStatusText);

            stack.Children.Add(CreateCard(Loc.Get("SettingsSection"), setStack));

            scrollViewer.Content = stack;
            grid.Children.Add(scrollViewer);
            return grid;
        }

        private void UpdateLocalizedUi()
        {
            _tabBtnRecord.Content = Loc.Get("TabRecord");
            _tabBtnConvert.Content = Loc.Get("TabConvert");
            _tabBtnAnalyze.Content = Loc.Get("TabAnalyze");
            _tabBtnSettings.Content = Loc.Get("TabSettings");
            _refreshDevicesButton.Content = Loc.Get("RefreshDevices");
            _statusPillText.Text = _engine.State == RecordingState.Recording ? Loc.Get("StatusRecording") :
                                   _engine.State == RecordingState.Paused ? Loc.Get("StatusPaused") : Loc.Get("StatusReady");
        }
        // ================= HELPERS & EVENTS =================
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

        private Button CreateHeroButton(string text, Color normalColor, Color hoverColor, double height, double width)
        {
            var btn = new Button
            {
                Content = text,
                Height = height,
                Width = width,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(normalColor));

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
                        _audioActivityBadge.Text = Loc.Get("AudioActive");
                        _audioActivityBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    }
                    else
                    {
                        _audioActivityBadge.Text = Loc.Get("AudioSilent");
                        _audioActivityBadge.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    }
                });
            };

            _engine.StatsUpdated += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _currentDuration = e.Elapsed;
                    double mb = (double)e.BytesWritten / (1024 * 1024);
                    _fileSizeText.Text = string.Format("{0:0.0} MB", mb);
                });
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
                    MessageBox.Show(ex.Message, "Audio-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateUiForState(RecordingState.Idle);
                });
            };

            _engine.StateChanged += (s, state) =>
            {
                Dispatcher.InvokeAsync(() => UpdateUiForState(state));
            };
        }

        private void LoadAudioDevices()
        {
            _deviceComboBox.Items.Clear();

            bool isMic = _deviceTypeComboBox != null && _deviceTypeComboBox.SelectedIndex == 1;
            var devices = isMic ? AudioDeviceEnumerator.GetCaptureDevices() : AudioDeviceEnumerator.GetRenderDevices();

            foreach (var d in devices)
            {
                _deviceComboBox.Items.Add(d);
            }

            if (_deviceComboBox.Items.Count > 0)
            {
                _deviceComboBox.SelectedIndex = 0;
            }
            else
            {
                _deviceFormatText.Text = "Keine passenden Audiogeräte gefunden.";
            }
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dev = _deviceComboBox.SelectedItem as AudioDeviceInfo;
            if (dev != null)
            {
                _deviceFormatText.Text = string.Format("Gerät: {0} • Standardformat: {1}", dev.Name, dev.FormatDescription);
                try { _engine.SetDevice(dev); } catch { }
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_engine.State == RecordingState.Recording)
            {
                _timerText.Text = string.Format("{0:00}:{1:00}:{2:00}.{3:0}", (int)_currentDuration.TotalHours, _currentDuration.Minutes, _currentDuration.Seconds, _currentDuration.Milliseconds / 100);
            }
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.State == RecordingState.Idle)
            {
                var dev = _deviceComboBox.SelectedItem as AudioDeviceInfo;
                if (dev == null)
                {
                    MessageBox.Show("Bitte wählen Sie zuerst ein Audiogerät aus.", "Kein Gerät", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(_outputDirectory))
                {
                    try { Directory.CreateDirectory(_outputDirectory); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Zielverzeichnis konnte nicht erstellt werden: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                string prefix = _prefixTextBox.Text.Trim();
                if (string.IsNullOrEmpty(prefix)) prefix = "Recording";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = string.Format("{0}_{1}.wav", prefix, timestamp);
                string fullPath = Path.Combine(_outputDirectory, fileName);

                WavOutputFormat fmt = _formatComboBox.SelectedIndex == 1 ? WavOutputFormat.Float32 : WavOutputFormat.Pcm16;

                try
                {
                    _currentDuration = TimeSpan.Zero;
                    _timerText.Text = "00:00:00.0";
                    _fileSizeText.Text = "0.0 MB";

                    _engine.StartRecording(dev, fullPath, fmt);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Start der Aufnahme fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (_engine.State == RecordingState.Recording || _engine.State == RecordingState.Paused)
            {
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

        private void UpdateUiForState(RecordingState state)
        {
            switch (state)
            {
                case RecordingState.Idle:
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    _statusPillText.Text = Loc.Get("StatusReady");
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                    _btnRecord.Content = Loc.Get("StartRecord");
                    ApplyButtonColor(_btnRecord, Color.FromRgb(225, 29, 72), Color.FromRgb(190, 18, 60));

                    _btnPause.IsEnabled = false;
                    _btnPause.Content = Loc.Get("PauseRecord");

                    _deviceComboBox.IsEnabled = true;
                    _deviceTypeComboBox.IsEnabled = true;
                    _refreshDevicesButton.IsEnabled = true;
                    break;

                case RecordingState.Recording:
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(60, 225, 29, 72));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 29, 72));
                    _statusPillText.Text = Loc.Get("StatusRecording");
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(244, 63, 94));

                    _btnRecord.Content = Loc.Get("StopRecord");
                    ApplyButtonColor(_btnRecord, Color.FromRgb(71, 85, 105), Color.FromRgb(100, 116, 139));

                    _btnPause.IsEnabled = true;
                    _btnPause.Content = Loc.Get("PauseRecord");

                    _deviceComboBox.IsEnabled = false;
                    _deviceTypeComboBox.IsEnabled = false;
                    _refreshDevicesButton.IsEnabled = false;
                    break;

                case RecordingState.Paused:
                    _statusPillBorder.Background = new SolidColorBrush(Color.FromArgb(60, 234, 179, 8));
                    _statusPillBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                    _statusPillText.Text = Loc.Get("StatusPaused");
                    _statusPillText.Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21));

                    _btnPause.Content = Loc.Get("ResumeRecord");
                    break;
            }
        }

        private void ApplyButtonColor(Button btn, Color normalColor, Color hoverColor)
        {
            var template = btn.Template;
            if (template != null)
            {
                var border = template.FindName("border", btn) as Border;
                if (border != null)
                {
                    border.Background = new SolidColorBrush(normalColor);
                }
            }
        }

        private void BrowseDirButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Wählen Sie das Zielverzeichnis für Aufnahmen:";
                dlg.SelectedPath = _outputDirectory;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputDirectory = dlg.SelectedPath;
                    _outputDirTextBox.Text = _outputDirectory;
                }
            }
        }

        private void AddRecordingToHistory(string filePath, TimeSpan duration, long fileSize)
        {
            string fileName = Path.GetFileName(filePath);
            string durStr = string.Format("{0:00}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
            double mb = (double)fileSize / (1024 * 1024);
            string sizeStr = string.Format("{0:0.0} MB", mb);
            string timeStr = DateTime.Now.ToString("HH:mm:ss");

            var item = new RecordingItem
            {
                FilePath = filePath,
                FileName = fileName,
                Timestamp = timeStr,
                Duration = durStr,
                FileSize = sizeStr
            };
            _recordingsList.Insert(0, item);

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

            var btnPlay = CreateStyledButton(Loc.Get("Play"), Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 28);
            btnPlay.Margin = new Thickness(0, 0, 6, 0);
            btnPlay.Click += (s, ev) =>
            {
                if (_player.IsPlaying && _player.CurrentFile == filePath)
                {
                    _player.Stop();
                    btnPlay.Content = Loc.Get("Play");
                }
                else
                {
                    _player.Play(filePath);
                    btnPlay.Content = Loc.Get("StopPlay");
                }
            };
            _player.PlaybackStopped += (s, ev) =>
            {
                Dispatcher.InvokeAsync(() => btnPlay.Content = Loc.Get("Play"));
            };
            actionsStack.Children.Add(btnPlay);

            var btnAna = CreateStyledButton(Loc.Get("AnalyzeFile"), Color.FromRgb(6, 182, 212), Color.FromRgb(14, 165, 233), 28);
            btnAna.Margin = new Thickness(0, 0, 6, 0);
            btnAna.Click += (s, ev) =>
            {
                _analyzeFileTextBox.Text = filePath;
                SwitchTab(2);
                RunAnalysis(filePath);
            };
            actionsStack.Children.Add(btnAna);

            var btnShow = CreateStyledButton(Loc.Get("Explorer"), Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 28);
            btnShow.Click += (s, ev) =>
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath));
                }
            };
            actionsStack.Children.Add(btnShow);

            Grid.SetColumn(actionsStack, 1);
            grid.Children.Add(actionsStack);

            _historyListBox.Items.Insert(0, grid);
        }
    }
}
