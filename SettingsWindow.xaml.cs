using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Boundless
{
    public partial class SettingsWindow : Window
    {
        private AppData _appData;
        private MainWindow _mainWindow;

        public SettingsWindow(AppData data, MainWindow mainWindow)
        {
            InitializeComponent();
            _appData = data;
            _mainWindow = mainWindow;
            LoadUI();
        }

        private void LoadUI()
        {
            HotkeysPanel.Children.Clear();
            AddHotkeyRow("穿透模式", _appData.Hotkeys.Ghost, new HotkeyConfig { Modifiers = 7, Vk = 0x71, Display = "Ctrl + Alt + Shift + F2" }, h => _appData.Hotkeys.Ghost = h);
            AddHotkeyRow("沉浸模式", _appData.Hotkeys.Immerse, new HotkeyConfig { Modifiers = 7, Vk = 0x70, Display = "Ctrl + Alt + Shift + F1" }, h => _appData.Hotkeys.Immerse = h);
            AddHotkeyRow("上一P", _appData.Hotkeys.Prev, new HotkeyConfig { Modifiers = 6, Vk = 0xC0, Display = "Ctrl + Shift + ~" }, h => _appData.Hotkeys.Prev = h);
            AddHotkeyRow("回退", _appData.Hotkeys.Rewind, new HotkeyConfig { Modifiers = 6, Vk = 0x31, Display = "Ctrl + Shift + 1" }, h => _appData.Hotkeys.Rewind = h);
            AddHotkeyRow("播放/暂停", _appData.Hotkeys.Play, new HotkeyConfig { Modifiers = 6, Vk = 0x32, Display = "Ctrl + Shift + 2" }, h => _appData.Hotkeys.Play = h);
            AddHotkeyRow("快进", _appData.Hotkeys.Forward, new HotkeyConfig { Modifiers = 6, Vk = 0x33, Display = "Ctrl + Shift + 3" }, h => _appData.Hotkeys.Forward = h);
            AddHotkeyRow("下一P", _appData.Hotkeys.Next, new HotkeyConfig { Modifiers = 6, Vk = 0x34, Display = "Ctrl + Shift + 4" }, h => _appData.Hotkeys.Next = h);
            AddHotkeyRow("显示/隐藏", _appData.Hotkeys.Toggle, new HotkeyConfig { Modifiers = 7, Vk = 0x44, Display = "Ctrl + Alt + Shift + D" }, h => _appData.Hotkeys.Toggle = h);
        }

        private void AddHotkeyRow(string labelText, HotkeyConfig current, HotkeyConfig defaultConfig, System.Action<HotkeyConfig> saveAction)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            
            var label = new TextBlock { Text = labelText, Width = 80, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            
            var textBox = new TextBox { Width = 240, Height = 25, IsReadOnly = true, Text = current.Display, Tag = current, VerticalContentAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), Foreground = Brushes.White, BorderBrush = Brushes.Gray };
            
            textBox.GotFocus += (s, e) => { textBox.Text = "请按下快捷键..."; textBox.BorderBrush = Brushes.DodgerBlue; };
            textBox.LostFocus += (s, e) => { textBox.Text = ((HotkeyConfig)textBox.Tag).Display; textBox.BorderBrush = Brushes.Gray; };
            
            // 【核心：按键拦截与 Esc 解绑】
            textBox.PreviewKeyDown += (s, e) =>
            {
                e.Handled = true;
                Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
                if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;

                if (key == Key.Escape) {
                    textBox.Tag = new HotkeyConfig { Modifiers = 0, Vk = 0, Display = "[未指定]" };
                    textBox.Text = "[未指定]";
                    Keyboard.ClearFocus();
                    return;
                }

                ModifierKeys mods = Keyboard.Modifiers;
                uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                string modStr = "";
                if ((mods & ModifierKeys.Control) == ModifierKeys.Control) modStr += "Ctrl + ";
                if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift) modStr += "Shift + ";
                if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt) modStr += "Alt + ";
                string keyStr;
                if (key == Key.OemTilde) keyStr = "~";
                else if (key >= Key.D0 && key <= Key.D9) keyStr = key.ToString().Replace("D", "");
                else keyStr = key.ToString();

                var newConfig = new HotkeyConfig { Modifiers = (uint)mods, Vk = vk, Display = modStr + keyStr };
                textBox.Tag = newConfig;
                textBox.Text = newConfig.Display;
                Keyboard.ClearFocus();
            };

            var resetBtn = new Button { Content = "↺ 重置", Width = 50, Height = 25, Margin = new Thickness(10, 0, 0, 0), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            resetBtn.Click += (s, e) => { textBox.Tag = defaultConfig; textBox.Text = defaultConfig.Display; };

            textBox.TextChanged += (s, e) => saveAction((HotkeyConfig)textBox.Tag);

            panel.Children.Add(label); panel.Children.Add(textBox); panel.Children.Add(resetBtn);
            HotkeysPanel.Children.Add(panel);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SaveDatabase();
            _mainWindow.ApplyHotkeys(); // 通知主窗口重新注册热键
            this.Close();
        }
    }
}