using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Boundless
{
    public partial class ThemeWindow : Window
    {
        private AppData _appData;
        private MainWindow _mainWindow;

        public ThemeWindow(AppData data, MainWindow mainWindow)
        {
            InitializeComponent();
            _appData = data;
            _mainWindow = mainWindow;
            LoadThemes();
        }

        private void LoadThemes()
        {
            ThemeListBox.Items.Clear();
            foreach (var theme in ThemeManager.AvailableThemes.Values)
            {
                ThemeListBox.Items.Add(theme);
            }
            
            foreach (ThemeData item in ThemeListBox.Items)
            {
                if (item.Name == _appData.CurrentTheme)
                {
                    ThemeListBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void ThemeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeListBox.SelectedItem is ThemeData theme)
            {
                ThemeManager.ApplyTheme(theme.Name);
                _mainWindow.SetCurrentTheme(theme.Name);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeListBox.SelectedItem is ThemeData theme)
            {
                ThemeManager.ApplyTheme(theme.Name);
                _mainWindow.SetCurrentTheme(theme.Name);
                MessageBox.Show($"已应用主题: {theme.Name}", "主题设置", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("请先选择一个主题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.LoadAllThemes();
            LoadThemes();
            MessageBox.Show("主题列表已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string themesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes");
            if (!Directory.Exists(themesPath))
            {
                Directory.CreateDirectory(themesPath);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = themesPath,
                UseShellExecute = true
            });
        }
    }
}
