using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            // UI update timer for clock and stats
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void InitializeWindow()
        {
            Title = "ER Audio Loopback Recorder";
            Width = 780;
            Height = 780;
            MinWidth = 680;
            MinHeight = 650;
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
            Grid.SetColumn(_statusPillBorder, 1);
            headerGrid.Children.Add(_statusPillBorder);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // --- MAIN CONTENT (SCROLLABLE) ---
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16)
            };

            var contentStack = new StackPanel();

            // 1. Device Selection Card
            var devGrid = new Grid();
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _deviceComboBox = new ComboBox
            {
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
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
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
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

            scrollViewer.Content = contentStack;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

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

            var btnShow = CreateStyledButton("📂 Explorer", Color.FromRgb(51, 65, 85), Color.FromRgb(71, 85, 105), 28);
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
