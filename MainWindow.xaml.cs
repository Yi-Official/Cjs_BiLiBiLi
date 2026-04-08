using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace Boundless
{
    public class ProfileData
    {
        public string Url { get; set; } = "https://www.bilibili.com";
        public double Width { get; set; } = 1200;
        public double Height { get; set; } = 700;
        public double Left { get; set; } = -1; 
        public double Top { get; set; } = -1;
        public double VideoTime { get; set; } = 0;
    }

    public class HotkeyConfig { public uint Modifiers { get; set; } public uint Vk { get; set; } public string Display { get; set; } = "[未指定]"; }

    public class AppHotkeys
    {
        public HotkeyConfig Ghost { get; set; } = new() { Modifiers = 7, Vk = 0x71, Display = "Ctrl + Alt + Shift + F2" };
        public HotkeyConfig Immerse { get; set; } = new() { Modifiers = 7, Vk = 0x70, Display = "Ctrl + Alt + Shift + F1" };
        public HotkeyConfig Prev { get; set; } = new() { Modifiers = 6, Vk = 0xC0, Display = "Ctrl + Shift + ~" };
        public HotkeyConfig Rewind { get; set; } = new() { Modifiers = 6, Vk = 0x31, Display = "Ctrl + Shift + 1" };
        public HotkeyConfig Play { get; set; } = new() { Modifiers = 6, Vk = 0x32, Display = "Ctrl + Shift + 2" };
        public HotkeyConfig Forward { get; set; } = new() { Modifiers = 6, Vk = 0x33, Display = "Ctrl + Shift + 3" };
        public HotkeyConfig Next { get; set; } = new() { Modifiers = 6, Vk = 0x34, Display = "Ctrl + Shift + 4" };
        public HotkeyConfig Toggle { get; set; } = new() { Modifiers = 7, Vk = 0x44, Display = "Ctrl + Alt + Shift + D" };
        public HotkeyConfig OpacityUp { get; set; } = new() { Modifiers = 7, Vk = 0x21, Display = "Ctrl + Alt + Shift + PageUp" };
        public HotkeyConfig OpacityDown { get; set; } = new() { Modifiers = 7, Vk = 0x22, Display = "Ctrl + Alt + Shift + PageDown" };
    }

    public class AppData
    {
        public string LastUser { get; set; } = "";
        public string LastGame { get; set; } = "";
        public string CurrentTheme { get; set; } = "默认主题";
        public bool BossMode { get; set; } = false;
        public string DefaultAddress { get; set; } = "https://www.bilibili.com";
        public AppHotkeys Hotkeys { get; set; } = new AppHotkeys();
        public Dictionary<string, Dictionary<string, ProfileData>> Profiles { get; set; } = new();
        // 保存窗口位置和大小
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 700;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double WindowOpacity { get; set; } = 1.0;
        public string CustomIconPath { get; set; } = "";
        public List<string> CustomAddresses { get; set; } = new();
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] private static extern int GetWindowLong(nint hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(nint hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
        [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttribData data);
        private const int GWL_EXSTYLE = -20; private const int WS_EX_TRANSPARENT = 0x00000020; private const int WS_EX_LAYERED = 0x00000080;

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttribData
        {
            public int Attrib;
            public nint pvData;
            public int cbData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private const int WCA_ACCENT_POLICY = 19;
        private const int ACCENT_DISABLED = 0;
        private const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
        private const int ACCENT_ENABLE_BLURBEHIND = 3;

        private void SetWindowTransparency(bool enable)
        {
            if (_windowHandle == nint.Zero) return;
            var accent = new AccentPolicy();
            if (enable)
            {
                accent.AccentState = ACCENT_ENABLE_TRANSPARENTGRADIENT;
                accent.GradientColor = 0x01000000; // alpha=1，几乎透明但WPF认为有实体
            }
            else
            {
                accent.AccentState = ACCENT_DISABLED;
            }
            var size = Marshal.SizeOf(accent);
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttribData
            {
                Attrib = WCA_ACCENT_POLICY,
                pvData = ptr,
                cbData = size
            };
            SetWindowCompositionAttribute(_windowHandle, ref data);
            Marshal.FreeHGlobal(ptr);
        }

        private Point _dragStartPoint;
        private bool _isImmersive = false;
        private bool _isGhostMode = false;
        private double _userOpacity = 1.0;
        private nint _windowHandle;
        private bool _opacityCssInjected = false;

        // 透明度浮层自动隐藏计时器
        private DispatcherTimer? _opacityPopupTimer;
        private DispatcherTimer? _tipsTimer;
        private int _tipsIndex = 0;
        private bool _isInitializing = true;

        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private Dictionary<int, Action> _hotkeyActions = new();

        public AppData _appData = new AppData();
        private const string ConfigPath = "boundless_data.json";
        private const string DataDir = "数据";
        
        private string _currentUser = "";
        private string _currentGame = "";
        private double _pendingLoadTime = 0; 

        public MainWindow()
        {
            InitializeComponent();
            EnsureDataDirectory();
            LoadDatabase();
            
            ThemeManager.Initialize();
            ThemeManager.ThemeChanged += OnThemeChanged;
            ThemeManager.ApplyTheme(_appData.CurrentTheme);
            
            SetupTrayIcon(); 
            InitializeBrowser();

            string u = _appData.LastUser;
            string g = _appData.LastGame;
            
            if (!string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(g) && _appData.Profiles.ContainsKey(u) && _appData.Profiles[u].ContainsKey(g))
            {
                _currentUser = u; _currentGame = g; var profile = _appData.Profiles[u][g];
                Width = profile.Width; Height = profile.Height;
                if (profile.Left >= 0 && profile.Top >= 0) { Left = profile.Left; Top = profile.Top; }
                _pendingLoadTime = profile.VideoTime; 
                BiliBrowser.Source = new Uri(profile.Url);
                AddressBar.Text = profile.Url;
            }
            else 
            {
                Width = _appData.WindowWidth;
                Height = _appData.WindowHeight;
                if (_appData.WindowLeft >= 0 && _appData.WindowTop >= 0)
                {
                    Left = _appData.WindowLeft;
                    Top = _appData.WindowTop;
                }
                BiliBrowser.Source = new Uri(_appData.DefaultAddress);
                AddressBar.Text = _appData.DefaultAddress;
            }

            _userOpacity = 1.0;
            OpacitySlider.Value = 1.0;

            StateChanged += MainWindow_StateChanged;

            // 初始化透明度浮层计时器
            _opacityPopupTimer = new DispatcherTimer();
            _opacityPopupTimer.Interval = TimeSpan.FromSeconds(1.5);
            _opacityPopupTimer.Tick += (s, ev) =>
            {
                _opacityPopupTimer.Stop();
                OpacityPopup.IsOpen = false;
            };

            _tipsTimer = new DispatcherTimer();
            _tipsTimer.Interval = TimeSpan.FromSeconds(4);
            _tipsTimer.Tick += (s, ev) =>
            {
                UpdateTipsText();
            };
            _tipsTimer.Start();
            UpdateTipsText();
            UpdateDataButtonTooltip();

            _isInitializing = false;
        }

        private void UpdateTipsText()
        {
            string ghostHotkey = $"{_appData.Hotkeys.Ghost.Display} 开启/关闭穿透";
            string opacityHotkey = $"{_appData.Hotkeys.OpacityUp.Display}/{_appData.Hotkeys.OpacityDown.Display} 调节透明度";
            string newText = _tipsIndex == 0 ? $"澪一Tips：{ghostHotkey}" : $"澪一Tips：{opacityHotkey}";

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.8, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                StatusText.Text = newText;
                _tipsIndex = _tipsIndex == 0 ? 1 : 0;
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 0.8, TimeSpan.FromMilliseconds(200));
                StatusText.BeginAnimation(System.Windows.Controls.TextBlock.OpacityProperty, fadeIn);
            };
            StatusText.BeginAnimation(System.Windows.Controls.TextBlock.OpacityProperty, fadeOut);
        }

        // ===== 地址栏：回车跳转 =====
        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string url = AddressBar.Text.Trim();
                if (string.IsNullOrEmpty(url)) return;
                // 自动补全协议
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    url = "https://" + url;
                BiliBrowser.CoreWebView2?.Navigate(url);
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        // ===== 透明度浮层显示 =====
        private void ShowOpacityOverlay(double opacity)
        {
            int pct = (int)Math.Round(opacity * 100);
            OpacityOverlayText.Text = $"透明度: {pct}%";
            OpacityPopup.IsOpen = true;
            _opacityPopupTimer?.Stop();
            _opacityPopupTimer?.Start();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon();

            try {
                if (!string.IsNullOrEmpty(_appData.CustomIconPath) && File.Exists(_appData.CustomIconPath))
                {
                    _trayIcon.Icon = new System.Drawing.Icon(_appData.CustomIconPath);
                }
                else
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (File.Exists(exePath)) {
                        _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                    if (_trayIcon.Icon == null && File.Exists("favicon.ico")) {
                        _trayIcon.Icon = new System.Drawing.Icon("favicon.ico");
                    }
                }
            } catch {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _trayIcon.Text = "澪一 无界 (运行中)";
            _trayIcon.Visible = true;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("显示/隐藏 窗口", null!, (s, e) => { ToggleVisibility(); });
            menu.Items.Add("设置全局快捷键", null!, (s, e) => { new SettingsWindow(_appData, this).Show(); });
            
            var bossModeMenu = new System.Windows.Forms.ToolStripMenuItem("老板模式");
            bossModeMenu.ToolTipText = "开启后，隐藏窗口时自动暂停视频，显示时自动继续播放";
            var bossModeInfo = new System.Windows.Forms.ToolStripMenuItem("隐藏时暂停，显示时继续");
            bossModeInfo.Enabled = false;
            var bossModeOn = new System.Windows.Forms.ToolStripMenuItem("开启");
            var bossModeOff = new System.Windows.Forms.ToolStripMenuItem("关闭");
            
            bossModeOn.Click += (s, e) => { _appData.BossMode = true; SaveConfig(); bossModeOn.Checked = true; bossModeOff.Checked = false; };
            bossModeOff.Click += (s, e) => { _appData.BossMode = false; SaveConfig(); bossModeOn.Checked = false; bossModeOff.Checked = true; };
            
            bossModeMenu.DropDownItems.Add(bossModeInfo);
            bossModeMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());
            bossModeMenu.DropDownItems.Add(bossModeOn);
            bossModeMenu.DropDownItems.Add(bossModeOff);
            
            menu.Items.Add(bossModeMenu);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("安全退出", null!, (s, e) => CloseButton_Click(null!, null!));
            
            bossModeOn.Checked = _appData.BossMode;
            bossModeOff.Checked = !_appData.BossMode;

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => { ToggleVisibility(); };
        }

        public void RefreshTrayIcon()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            SetupTrayIcon();
            UpdateWindowIcon();
        }

        public void UpdateWindowIcon()
        {
            string iconPath = !string.IsNullOrEmpty(_appData.CustomIconPath) && File.Exists(_appData.CustomIconPath)
                ? _appData.CustomIconPath
                : (File.Exists("ico/app.ico") ? "ico/app.ico" : "");

            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(Path.GetFullPath(iconPath), UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Icon = bitmap;
                    return;
                }
                catch { }
            }

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (File.Exists(exePath))
            {
                try
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(exePath, UriKind.Absolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        Icon = bitmap;
                    }
                }
                catch { }
            }
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible && WindowState != WindowState.Minimized) 
            {
                if (_appData.BossMode)
                    RunBrowserScript("document.querySelector('.bpx-player-ctrl-play')?.click();");
                WindowState = WindowState.Minimized;
            }
            else 
            {
                Visibility = Visibility.Visible;
                WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Focus();
                
                if (_appData.BossMode)
                    RunBrowserScript("document.querySelector('.bpx-player-ctrl-play')?.click();");
                
                ResizeCorner.Visibility = _isGhostMode ? Visibility.Hidden : Visibility.Visible;
                
                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => { Topmost = false; Topmost = true; Activate(); Focus(); });
                });
                System.Threading.Tasks.Task.Delay(150).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => { Activate(); Focus(); });
                });
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(HwndHook);
            ApplyHotkeys(); 
        }

        private void ApplyWindowOpacity(double opacity)
        {
            if (opacity < 1.0)
            {
                InjectOpacityCss(opacity);
                RootContainer.Background = Brushes.Transparent;
                SetWindowTransparency(true);
            }
            else
            {
                RemoveOpacityCss();
                SetWindowTransparency(false);
                if (ThemeManager.CurrentTheme != null && !ThemeManager.CurrentTheme.Styles.EnableAcrylic)
                    RootContainer.Background = ThemeManager.GetBrush(ThemeManager.CurrentTheme.Colors.WindowBackground);
            }
        }

        private async void InjectOpacityCss(double opacity)
        {
            if (BiliBrowser.CoreWebView2 == null) return;
            string js = $@"(function(){{
                var s = document.getElementById('boundless-opacity-style');
                if(!s){{ s = document.createElement('style'); s.id='boundless-opacity-style'; document.head.appendChild(s); }}
                s.innerHTML = 'html,body{{background:transparent!important;opacity:{opacity:F2}!important;}} ::-webkit-scrollbar{{display:none!important;}}';
            }})();";
            await BiliBrowser.CoreWebView2.ExecuteScriptAsync(js);
            _opacityCssInjected = true;
        }

        private async void RemoveOpacityCss()
        {
            if (BiliBrowser.CoreWebView2 == null) return;
            await BiliBrowser.CoreWebView2.ExecuteScriptAsync(
                @"(function(){var s=document.getElementById('boundless-opacity-style');if(s)s.remove();})();");
            _opacityCssInjected = false;
        }

        public void ApplyHotkeys()
        {
            for (int i = 1; i <= 10; i++) UnregisterHotKey(_windowHandle, i);
            _hotkeyActions.Clear();

            RegisterKey(1, _appData.Hotkeys.Ghost, ToggleGhostMode);
            RegisterKey(2, _appData.Hotkeys.Immerse, () => ImmersiveButton_Click(null!, null!));
            RegisterKey(3, _appData.Hotkeys.Prev, () => PrevP_Click(null!, null!));
            RegisterKey(4, _appData.Hotkeys.Rewind, () => Rewind_Click(null!, null!));
            RegisterKey(5, _appData.Hotkeys.Play, () => PlayPause_Click(null!, null!));
            RegisterKey(6, _appData.Hotkeys.Forward, () => Forward_Click(null!, null!));
            RegisterKey(7, _appData.Hotkeys.Next, () => NextP_Click(null!, null!));
            RegisterKey(8, _appData.Hotkeys.Toggle, ToggleVisibility);
            RegisterKey(9, _appData.Hotkeys.OpacityUp, () => AdjustOpacity(0.05));
            RegisterKey(10, _appData.Hotkeys.OpacityDown, () => AdjustOpacity(-0.05));
            UpdateTipsText();
        }

        private void RegisterKey(int id, HotkeyConfig config, Action action)
        {
            if (config.Vk != 0) 
            {
                RegisterHotKey(_windowHandle, id, config.Modifiers, config.Vk);
                _hotkeyActions[id] = action;
            }
        }

        private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == 0x0312 && _hotkeyActions.ContainsKey(wParam.ToInt32())) 
            { 
                _hotkeyActions[wParam.ToInt32()].Invoke(); 
                handled = true; 
            }
            return nint.Zero;
        }

        private async void RunBrowserScript(string script) { if (BiliBrowser.CoreWebView2 != null) await BiliBrowser.CoreWebView2.ExecuteScriptAsync(script); }

        private void PrevP_Click(object sender, RoutedEventArgs e) => RunBrowserScript("document.querySelector('.bpx-player-ctrl-prev')?.click();");
        private void NextP_Click(object sender, RoutedEventArgs e) => RunBrowserScript("document.querySelector('.bpx-player-ctrl-next')?.click();");
        private void PlayPause_Click(object sender, RoutedEventArgs e) => RunBrowserScript("document.querySelector('.bpx-player-ctrl-play')?.click();");
        private void Rewind_Click(object sender, RoutedEventArgs e) => RunBrowserScript("document.body.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, keyCode: 37, key: 'ArrowLeft' })); document.body.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, keyCode: 37, key: 'ArrowLeft' }));");
        private void Forward_Click(object sender, RoutedEventArgs e) => RunBrowserScript("document.body.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, keyCode: 39, key: 'ArrowRight' })); document.body.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, keyCode: 39, key: 'ArrowRight' }));");

        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        }

        private void LoadDatabase() 
        { 
            if (File.Exists(ConfigPath)) { 
                try { 
                    string json = File.ReadAllText(ConfigPath); 
                    var data = JsonSerializer.Deserialize<AppData>(json); 
                    if (data != null) {
                        _appData.LastUser = data.LastUser;
                        _appData.LastGame = data.LastGame;
                        _appData.CurrentTheme = data.CurrentTheme;
                        _appData.BossMode = data.BossMode;
                        if (!string.IsNullOrEmpty(data.DefaultAddress))
                            _appData.DefaultAddress = data.DefaultAddress;
                        _appData.Hotkeys = data.Hotkeys;
                        _appData.WindowWidth = data.WindowWidth;
                        _appData.WindowHeight = data.WindowHeight;
                        _appData.WindowLeft = data.WindowLeft;
                        _appData.WindowTop = data.WindowTop;
                        if (!string.IsNullOrEmpty(data.CustomIconPath))
                            _appData.CustomIconPath = data.CustomIconPath;
                        if (data.CustomAddresses != null)
                            _appData.CustomAddresses = data.CustomAddresses;
                        if (data.Profiles != null && data.Profiles.Count > 0) {
                            foreach(var kv in data.Profiles) {
                                SaveUserProfile(kv.Key, kv.Value);
                                _appData.Profiles[kv.Key] = kv.Value;
                            }
                            SaveConfig(); 
                        }
                    }
                } catch { } 
            } 

            if (Directory.Exists(DataDir)) {
                foreach (string file in Directory.GetFiles(DataDir, "*.json")) {
                    try {
                        string userName = Path.GetFileNameWithoutExtension(file);
                        string json = File.ReadAllText(file);
                        var profiles = JsonSerializer.Deserialize<Dictionary<string, ProfileData>>(json);
                        if (profiles != null) {
                            _appData.Profiles[userName] = profiles;
                        }
                    } catch { }
                }
            }
        }

        public void SaveConfig() 
        { 
            var configOnly = new AppData {
                LastUser = _appData.LastUser,
                LastGame = _appData.LastGame,
                CurrentTheme = _appData.CurrentTheme,
                BossMode = _appData.BossMode,
                DefaultAddress = _appData.DefaultAddress,
                CustomIconPath = _appData.CustomIconPath,
                Hotkeys = _appData.Hotkeys,
                Profiles = new Dictionary<string, Dictionary<string, ProfileData>>(),
                WindowWidth = WindowState == WindowState.Maximized ? RestoreBounds.Width : Width,
                WindowHeight = WindowState == WindowState.Maximized ? RestoreBounds.Height : Height,
                WindowLeft = WindowState == WindowState.Maximized ? RestoreBounds.Left : Left,
                WindowTop = WindowState == WindowState.Maximized ? RestoreBounds.Top : Top,
                WindowOpacity = 1.0,
                CustomAddresses = _appData.CustomAddresses
            };
            string json = JsonSerializer.Serialize(configOnly, new JsonSerializerOptions { WriteIndented = true }); 
            File.WriteAllText(ConfigPath, json); 
        }

        public void SaveUserProfile(string user, Dictionary<string, ProfileData> profiles)
        {
            EnsureDataDirectory();
            string filePath = Path.Combine(DataDir, $"{user}.json");
            string json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void DeleteUserProfile(string user)
        {
            string filePath = Path.Combine(DataDir, $"{user}.json");
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public void SaveDatabase() { SaveConfig(); }

        private void OpenDataPanel_Click(object sender, RoutedEventArgs e) { BiliBrowser.Visibility = Visibility.Hidden; DataPanel.Visibility = Visibility.Visible; RefreshAllUserLists(); TabLoad_Click(sender, e); }
        private void CloseDataPanel_Click(object sender, RoutedEventArgs e) { DataPanel.Visibility = Visibility.Hidden; BiliBrowser.Visibility = Visibility.Visible; }
        private void TabLoad_Click(object sender, RoutedEventArgs e) { ViewLoad.Visibility = Visibility.Visible; ViewSave.Visibility = Visibility.Hidden; ViewManage.Visibility = Visibility.Hidden; }
        private void TabSave_Click(object sender, RoutedEventArgs e) { ViewLoad.Visibility = Visibility.Hidden; ViewSave.Visibility = Visibility.Visible; ViewManage.Visibility = Visibility.Hidden; }
        private void TabManage_Click(object sender, RoutedEventArgs e) { ViewLoad.Visibility = Visibility.Hidden; ViewSave.Visibility = Visibility.Hidden; ViewManage.Visibility = Visibility.Visible; }

        private void RefreshAllUserLists() { 
            string? selectedLoadUser = ComboLoadUser.SelectedItem as string;
            string? selectedManageUser = ComboManageUser.SelectedItem as string;
            string? selectedSaveUser = ComboSaveUser.Text;

            var users = _appData.Profiles.Keys.ToList(); 
            ComboLoadUser.ItemsSource = users; 
            ComboManageUser.ItemsSource = users; 
            ComboSaveUser.ItemsSource = users; 
            
            ComboLoadUser.SelectedItem = selectedLoadUser;
            ComboManageUser.SelectedItem = selectedManageUser;
            ComboSaveUser.Text = selectedSaveUser;

            ComboLoadUser_SelectionChanged(null!, null!);
            ComboManageUser_SelectionChanged(null!, null!);
            ComboSaveUser_TextChanged(null!, null!);
        }

        private void ComboLoadUser_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { 
            if (ComboLoadUser.SelectedItem is string user && _appData.Profiles.ContainsKey(user)) { 
                ComboLoadGame.ItemsSource = _appData.Profiles[user].Keys.ToList(); 
                ComboLoadGame.SelectedIndex = 0; 
            } else {
                ComboLoadGame.ItemsSource = null;
            }
        }
        
        private void ComboManageUser_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { 
            if (ComboManageUser.SelectedItem is string user && _appData.Profiles.ContainsKey(user)) { 
                ListManageGames.ItemsSource = _appData.Profiles[user].Keys.ToList(); 
            } else {
                ListManageGames.ItemsSource = null;
            }
        }

        private void ComboSaveUser_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { 
            string user = ComboSaveUser.Text; 
            if (_appData.Profiles.ContainsKey(user)) { 
                ComboSaveGame.ItemsSource = _appData.Profiles[user].Keys.ToList(); 
            } else { 
                ComboSaveGame.ItemsSource = null; 
            } 
        }

        private void ExecuteLoad_Click(object sender, RoutedEventArgs e)
        {
            string? user = ComboLoadUser.SelectedItem as string;
            string? game = ComboLoadGame.SelectedItem as string;

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(game)) {
                if (!_appData.Profiles.ContainsKey(user) || !_appData.Profiles[user].ContainsKey(game)) {
                    MessageBox.Show("该数据已不存在，请刷新列表后再试。");
                    RefreshAllUserLists();
                    return;
                }

                var profile = _appData.Profiles[user][game];
                Width = profile.Width; Height = profile.Height;
                if (profile.Left >= 0 && profile.Top >= 0) { Left = profile.Left; Top = profile.Top; }
                _pendingLoadTime = profile.VideoTime;
                BiliBrowser.CoreWebView2?.Navigate(profile.Url);
                AddressBar.Text = profile.Url;
                _currentUser = user; _currentGame = game; _appData.LastUser = user; _appData.LastGame = game;
                SaveConfig();
                DataPanel.Visibility = Visibility.Hidden; BiliBrowser.Visibility = Visibility.Visible;
                UpdateDataButtonTooltip();
            }
        }

        private void UpdateDataButtonTooltip()
        {
            if (!string.IsNullOrEmpty(_appData.LastUser) && !string.IsNullOrEmpty(_appData.LastGame))
                BtnData.ToolTip = $"当前读取：{_appData.LastUser} / {_appData.LastGame}";
            else
                BtnData.ToolTip = "当前读取：未读取";
        }

        private async void ExecuteSave_Click(object sender, RoutedEventArgs e)
        {
            string user = ComboSaveUser.Text.Trim(); string game = ComboSaveGame.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(game)) { MessageBox.Show("分组和类名不能为空！"); return; }
            double currentTime = 0;
            try { if (BiliBrowser.CoreWebView2 != null) { string timeStr = await BiliBrowser.CoreWebView2.ExecuteScriptAsync("(() => { let v = document.querySelector('bwp-video') || document.querySelector('video'); return v ? v.currentTime : 0; })()"); if (double.TryParse(timeStr, out double parsedTime)) currentTime = parsedTime; } } catch { }
            var newProfile = new ProfileData { Url = BiliBrowser.Source?.ToString() ?? "https://www.bilibili.com", Width = WindowState == WindowState.Maximized ? RestoreBounds.Width : Width, Height = WindowState == WindowState.Maximized ? RestoreBounds.Height : Height, Left = WindowState == WindowState.Maximized ? RestoreBounds.Left : Left, Top = WindowState == WindowState.Maximized ? RestoreBounds.Top : Top, VideoTime = currentTime };
            if (!_appData.Profiles.ContainsKey(user)) _appData.Profiles[user] = new Dictionary<string, ProfileData>();
            _appData.Profiles[user][game] = newProfile;
            _currentUser = user; _currentGame = game; _appData.LastUser = user; _appData.LastGame = game;
            
            SaveConfig();
            SaveUserProfile(user, _appData.Profiles[user]);
            
            RefreshAllUserLists();
            MessageBox.Show($"[{user}] 的 [{game}] 数据已成功保存！"); DataPanel.Visibility = Visibility.Hidden; BiliBrowser.Visibility = Visibility.Visible;
        }

        private void ExecuteDelete_Click(object sender, RoutedEventArgs e)
        {
            string? user = ComboManageUser.SelectedItem as string; 
            string? game = ListManageGames.SelectedItem as string;
            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(game)) {
                _appData.Profiles[user].Remove(game);
                
                if (_currentUser == user && _currentGame == game)
                {
                    _currentUser = "";
                    _currentGame = "";
                    _appData.LastUser = "";
                    _appData.LastGame = "";
                }
                
                if (_appData.Profiles[user].Count == 0) {
                    _appData.Profiles.Remove(user); 
                    DeleteUserProfile(user);
                } else {
                    SaveUserProfile(user, _appData.Profiles[user]);
                }
                
                SaveConfig(); 
                RefreshAllUserLists();
                MessageBox.Show("删除成功！");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender != null) 
            {
                MessageBoxResult result = MessageBox.Show("确定要退出 澪一 无界 吗？\n(退出时会自动保存您当前的视频进度与位置)", "退出确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) return; 
            }

            if (!string.IsNullOrEmpty(_currentUser) && !string.IsNullOrEmpty(_currentGame))
            {
                if (_appData.Profiles.ContainsKey(_currentUser) && _appData.Profiles[_currentUser].ContainsKey(_currentGame))
                {
                    double currentTime = 0;
                    try { if (BiliBrowser.CoreWebView2 != null) { string timeStr = await BiliBrowser.CoreWebView2.ExecuteScriptAsync("(() => { let v = document.querySelector('bwp-video') || document.querySelector('video'); return v ? v.currentTime : 0; })()"); if (double.TryParse(timeStr, out double parsedTime)) currentTime = parsedTime; } } catch { }
                    _appData.Profiles[_currentUser][_currentGame].VideoTime = currentTime; _appData.Profiles[_currentUser][_currentGame].Url = BiliBrowser.Source?.ToString() ?? "https://www.bilibili.com";
                    double w = WindowState == WindowState.Maximized ? RestoreBounds.Width : Width; double h = WindowState == WindowState.Maximized ? RestoreBounds.Height : Height;
                    if (w > 100 && h > 100) { _appData.Profiles[_currentUser][_currentGame].Width = w; _appData.Profiles[_currentUser][_currentGame].Height = h; _appData.Profiles[_currentUser][_currentGame].Left = WindowState == WindowState.Maximized ? RestoreBounds.Left : Left; _appData.Profiles[_currentUser][_currentGame].Top = WindowState == WindowState.Maximized ? RestoreBounds.Top : Top; }
                    
                    SaveConfig();
                    SaveUserProfile(_currentUser, _appData.Profiles[_currentUser]);
                }
            }
            else
            {
                SaveConfig();
            }
            
            _trayIcon?.Dispose(); 
            for (int i = 1; i <= 10; i++) UnregisterHotKey(_windowHandle, i);
            Close();
        }

        // 透明度滚轮调节 (Ctrl + Shift + Alt + 滚轮)
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt))
            {
                double delta = e.Delta > 0 ? 0.05 : -0.05;
                AdjustOpacity(delta);
                e.Handled = true;
            }
        }

        // 透明度调节（仅在穿透模式下可用）
        private void AdjustOpacity(double delta)
        {
            if (!_isGhostMode) return;
            _userOpacity = Math.Clamp(_userOpacity + delta, 0, 1.0);
            OpacitySlider.Value = _userOpacity;
            ApplyWindowOpacity(_userOpacity);
            ShowOpacityOverlay(_userOpacity);
        }

        private void ToggleGhostMode()
        {
            _isGhostMode = !_isGhostMode; int extendedStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            if (_isGhostMode)
            {
                SetWindowLong(_windowHandle, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                TopControlBar.Visibility = Visibility.Hidden;
                ResizeCorner.Visibility = Visibility.Hidden;
                OpacityControlPanel.Visibility = Visibility.Visible;
                RootContainer.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                BiliBrowser.Margin = new Thickness(0);
                ApplyWindowOpacity(_userOpacity);
            }
            else
            {
                SetWindowLong(_windowHandle, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                TopControlBar.Visibility = Visibility.Visible;
                ResizeCorner.Visibility = Visibility.Visible;
                OpacityControlPanel.Visibility = Visibility.Collapsed;
                OpacitySlider.Value = 1.0;
                BiliBrowser.Margin = new Thickness(0, TopControlBar.Height, 12, 12);
                ApplyWindowOpacity(1.0);
                if (ThemeManager.CurrentTheme != null)
                {
                    ThemeManager.EnableAcrylic(this, ThemeManager.CurrentTheme.Styles.EnableAcrylic, ThemeManager.CurrentTheme.Colors.AcrylicTintColor, ThemeManager.CurrentTheme.Styles.AcrylicOpacity);
                    if (!ThemeManager.CurrentTheme.Styles.EnableAcrylic)
                        RootContainer.Background = ThemeManager.GetBrush(ThemeManager.CurrentTheme.Colors.WindowBackground);
                    else
                        RootContainer.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                }
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized) { RootContainer.Margin = new Thickness(6); ResizeCorner.Visibility = Visibility.Collapsed; BiliBrowser.Margin = new Thickness(0, TopControlBar.Height, 0, 0); }
            else { RootContainer.Margin = new Thickness(0); ResizeCorner.Visibility = Visibility.Visible; BiliBrowser.Margin = new Thickness(0, TopControlBar.Height, 12, 12); }
        }

        private async void InitializeBrowser()
        {
            try {
                string localRuntimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2Runtime");
                CoreWebView2Environment? env = null;
                
                if (Directory.Exists(localRuntimePath)) {
                    env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: localRuntimePath);
                }

                BiliBrowser.CoreWebView2InitializationCompleted += (s, e) => {
                    if (e.IsSuccess) {
                        BiliBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                        BiliBrowser.CoreWebView2.Settings.IsZoomControlEnabled = false;
                        BiliBrowser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);

                        Dispatcher.BeginInvoke(new Action(() => ApplyWindowOpacity(_userOpacity)));
                        
                        // 导航完成后同步地址栏
                        BiliBrowser.CoreWebView2.NavigationCompleted += async (s2, e2) => {
                            // 同步地址栏
                            Dispatcher.Invoke(() => {
                                AddressBar.Text = BiliBrowser.Source?.ToString() ?? "";
                            });

                            string hideScrollbarScript = @"
                                (function() {
                                    let styleId = 'hide-scrollbar-style';
                                    if (!document.getElementById(styleId)) {
                                        let s = document.createElement('style');
                                        s.id = styleId;
                                        s.innerHTML = '::-webkit-scrollbar { display: none !important; width: 0 !important; height: 0 !important; } body { scrollbar-width: none !important; -ms-overflow-style: none !important; }';
                                        document.head.appendChild(s);
                                    }
                                })();";
                            await BiliBrowser.CoreWebView2.ExecuteScriptAsync(hideScrollbarScript);

                            if (_opacityCssInjected)
                                InjectOpacityCss(_userOpacity);

                            if (_pendingLoadTime > 0)
                            {
                                double targetTime = _pendingLoadTime;
                                _pendingLoadTime = 0;
                                string jsScript = $@"
                                    (function() {{
                                        let target = {targetTime.ToString("F2")};
                                        let count = 0;
                                        let maxRetries = 10;
                                        function tryJump() {{
                                            let v = document.querySelector('bwp-video') || document.querySelector('video');
                                            if (v && v.readyState >= 1) {{
                                                v.muted = false;
                                                v.currentTime = target;
                                                console.log('Jumped to ' + target);
                                            }} else if (count < maxRetries) {{
                                                count++;
                                                setTimeout(tryJump, 1000);
                                            }}
                                        }}
                                        tryJump();
                                    }})();";
                                await BiliBrowser.CoreWebView2.ExecuteScriptAsync(jsScript);
                            }
                        };

                        BiliBrowser.CoreWebView2.NewWindowRequested += (s2, e2) => { e2.Handled = true; BiliBrowser.CoreWebView2.Navigate(e2.Uri); };
                    } else {
                        string msg = e.InitializationException?.Message ?? "未知错误";
                        MessageBox.Show($"浏览器引擎初始化失败: {msg}\n\n建议方案：\n1. 确保已安装 Microsoft Edge WebView2 Runtime。\n2. 或者在程序目录下放置 'WebView2Runtime' 文件夹。", "启动错误");
                    }
                };
                
                await BiliBrowser.EnsureCoreWebView2Async(env);
            } catch (Exception ex) {
                MessageBox.Show($"无法加载浏览器引擎: {ex.Message}\n\n请尝试安装 Microsoft Edge WebView2 Runtime，或者使用包含内置引擎的版本。", "启动错误");
            }
        }

        private async void ImmersiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (BiliBrowser.CoreWebView2 == null) return;
            _isImmersive = !_isImmersive;
            string jsCode = $"(function(){{ let enable = {_isImmersive.ToString().ToLower()}; let styleId = 'bl-style'; let exist = document.getElementById(styleId); if(enable){{ if(!exist){{ let s = document.createElement('style'); s.id = styleId; s.innerHTML = '#biliMainHeader, .bili-header, .right-container, #comment, .bpx-player-sending-area, .video-pod, .bili-footer, .up-info-container {{ display: none !important; }} .left-container {{ width: 100% !important; max-width: 100% !important; padding: 0 !important; margin: 0 !important; }} .player-wrap {{ height: 100vh !important; }} body {{ overflow: hidden !important; }}'; document.head.appendChild(s); }} let fs = document.querySelector('.bpx-player-ctrl-web'); if(fs && !fs.classList.contains('bpx-state-entered')) fs.click(); }} else {{ if(exist) exist.remove(); document.body.style.overflow = ''; let fs = document.querySelector('.bpx-player-ctrl-web'); if(fs && fs.classList.contains('bpx-state-entered')) fs.click(); }} }})();";
            await BiliBrowser.CoreWebView2.ExecuteScriptAsync(jsCode);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) { if (BiliBrowser.CoreWebView2 != null && BiliBrowser.CoreWebView2.CanGoBack) BiliBrowser.CoreWebView2.GoBack(); }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; return; } if (e.LeftButton == MouseButtonState.Pressed) { if (WindowState == WindowState.Maximized) _dragStartPoint = e.GetPosition(this); else DragMove(); } }
        private void Window_MouseMove(object sender, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Maximized) { Point currentPoint = e.GetPosition(this); if (Math.Abs(currentPoint.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(currentPoint.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance) { Point absoluteScreenPos = PointToScreen(currentPoint); double ratioX = currentPoint.X / ActualWidth; WindowState = WindowState.Normal; PresentationSource source = PresentationSource.FromVisual(this); if (source != null) { Point logicalPos = source.CompositionTarget.TransformFromDevice.Transform(absoluteScreenPos); Left = logicalPos.X - RestoreBounds.Width * ratioX; Top = logicalPos.Y - 15; } DragMove(); } } }
        private void TopControlBar_MouseEnter(object sender, MouseEventArgs e) { TopControlBar.Height = 35; TopControlBar.Background = ThemeManager.GetBrush(ThemeManager.CurrentTheme.Colors.TopBarBackground); TopControlContent.Visibility = Visibility.Visible; double rb = WindowState == WindowState.Maximized ? 0 : 12; BiliBrowser.Margin = new Thickness(0, 35, rb, rb); }
        private void TopControlBar_MouseLeave(object sender, MouseEventArgs e) { TopControlBar.Height = 10; TopControlBar.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); TopControlContent.Visibility = Visibility.Hidden; double rb = WindowState == WindowState.Maximized ? 0 : 12; BiliBrowser.Margin = new Thickness(0, 10, rb, rb); }
        private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e) { if (Width + e.HorizontalChange > MinWidth) Width += e.HorizontalChange; if (Height + e.VerticalChange > MinHeight) Height += e.VerticalChange; }

        // 透明度滑块事件（仅在穿透模式下可用）
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacitySlider == null || _isInitializing) return;
            if (!_isGhostMode) return;
            _userOpacity = OpacitySlider.Value;
            ApplyWindowOpacity(_userOpacity);
            ShowOpacityOverlay(_userOpacity);
        }

        private void OnThemeChanged(ThemeData theme)
        {
            ApplyThemeToUI(theme);
        }

        public void ApplyThemeToUI(ThemeData theme)
        {
            var colors = theme.Colors;
            var styles = theme.Styles;

            ThemeManager.EnableAcrylic(this, styles.EnableAcrylic, colors.AcrylicTintColor, styles.AcrylicOpacity);

            if (!styles.EnableAcrylic)
            {
                RootContainer.Background = ThemeManager.GetBrush(colors.WindowBackground);
            }
            else
            {
                RootContainer.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }

            var buttonRadius = ThemeManager.GetCornerRadius(styles.ButtonCornerRadius);
            var buttonBorderThickness = ThemeManager.GetThickness(styles.ButtonBorderThickness);
            var buttonFontWeight = ThemeManager.GetFontWeight(styles.ButtonFontWeight);
            var buttonOpacity = styles.ButtonOpacity;

            ApplyButtonStyle(BtnPrev, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnRewind, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnPlay, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnForward, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnNext, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);

            ApplyButtonStyle(BtnData, colors.AccentColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnTheme, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnImmerse, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnBack, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnMinimize, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnClose, colors.DangerColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);

            DataPanel.Background = ThemeManager.GetBrush(colors.PanelBackground);
            DataPanel.BorderBrush = ThemeManager.GetBrush(colors.BorderColor);
            DataPanel.CornerRadius = ThemeManager.GetCornerRadius(styles.PanelCornerRadius);
            DataPanel.BorderThickness = ThemeManager.GetThickness(styles.PanelBorderThickness);
            DataPanel.Opacity = styles.PanelOpacity;

            ApplyButtonStyle(BtnTabLoad, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnTabSave, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnTabManage, colors.ButtonBackground, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnClosePanel, colors.DangerColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);

            ApplyButtonStyle(BtnExecuteLoad, colors.AccentColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnExecuteSave, colors.SuccessColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);
            ApplyButtonStyle(BtnExecuteDelete, colors.DangerColor, colors.ButtonForeground, colors.ButtonBorder, buttonRadius, buttonBorderThickness, buttonFontWeight, buttonOpacity);

            TxtLoadUser.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            TxtLoadGame.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            TxtSaveUser.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            TxtSaveGame.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            TxtManageUser.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            TxtManageGame.Foreground = ThemeManager.GetBrush(colors.TextPrimary);
            StatusText.Foreground = ThemeManager.GetBrush(colors.TextPrimary);

            ListManageGames.Background = ThemeManager.GetBrush(colors.InputBackground);
            ListManageGames.Foreground = ThemeManager.GetBrush(colors.InputForeground);

            // 地址栏主题适配
            AddressBar.Foreground = ThemeManager.GetBrush(colors.InputForeground);
        }

        private void ApplyButtonStyle(System.Windows.Controls.Button btn, string bg, string fg, string border, CornerRadius radius, Thickness borderThickness, FontWeight fontWeight, double opacity)
        {
            btn.Background = ThemeManager.GetBrush(bg);
            btn.Foreground = ThemeManager.GetBrush(fg);
            btn.BorderBrush = ThemeManager.GetBrush(border);
            btn.BorderThickness = borderThickness;
            btn.FontWeight = fontWeight;
            btn.Opacity = opacity;
            btn.Tag = ThemeManager.GetBrush(ThemeManager.CurrentTheme.Colors.ButtonHover);
            btn.ApplyTemplate();
            if (btn.Template?.FindName("ButtonBorder", btn) is System.Windows.Controls.Border borderElement)
            {
                borderElement.CornerRadius = radius;
            }
        }

        private void ApplyButtonStyle(RepeatButton btn, string bg, string fg, string border, CornerRadius radius, Thickness borderThickness, FontWeight fontWeight, double opacity)
        {
            btn.Background = ThemeManager.GetBrush(bg);
            btn.Foreground = ThemeManager.GetBrush(fg);
            btn.BorderBrush = ThemeManager.GetBrush(border);
            btn.BorderThickness = borderThickness;
            btn.FontWeight = fontWeight;
            btn.Opacity = opacity;
            btn.Tag = ThemeManager.GetBrush(ThemeManager.CurrentTheme.Colors.ButtonHover);
            btn.ApplyTemplate();
            if (btn.Template?.FindName("ButtonBorder", btn) is System.Windows.Controls.Border borderElement)
            {
                borderElement.CornerRadius = radius;
            }
        }

        // 打开主题窗口
        private void OpenThemeWindow_Click(object sender, RoutedEventArgs e)
        {
            new ThemeWindow(_appData, this).Show();
        }

        public void SetCurrentTheme(string themeName)
        {
            _appData.CurrentTheme = themeName;
            ThemeManager.ApplyTheme(themeName);
            SaveConfig();
        }
    }
}
