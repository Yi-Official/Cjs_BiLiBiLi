using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Boundless
{
    public class ThemeStyles
    {
        public bool EnableAcrylic { get; set; } = false;
        public double AcrylicOpacity { get; set; } = 0.8;
        public double ButtonCornerRadius { get; set; } = 0;
        public double PanelCornerRadius { get; set; } = 8;
        public double WindowCornerRadius { get; set; } = 0;
        public double ButtonOpacity { get; set; } = 1.0;
        public double PanelOpacity { get; set; } = 1.0;
        public bool EnableButtonShadow { get; set; } = false;
        public bool EnablePanelShadow { get; set; } = false;
        public double ButtonBorderThickness { get; set; } = 0;
        public double PanelBorderThickness { get; set; } = 1;
        public string ButtonFontWeight { get; set; } = "Normal";
        public double ButtonFontSize { get; set; } = 12;
    }

    public class ThemeColors
    {
        public string TopBarBackground { get; set; } = "#CC000000";
        public string ButtonBackground { get; set; } = "#444444";
        public string ButtonForeground { get; set; } = "#FFFFFF";
        public string ButtonHover { get; set; } = "#555555";
        public string ButtonBorder { get; set; } = "#666666";
        public string PanelBackground { get; set; } = "#E61E1E1E";
        public string BorderColor { get; set; } = "#555555";
        public string InputBackground { get; set; } = "#333333";
        public string InputForeground { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#0078D7";
        public string DangerColor { get; set; } = "#800000";
        public string SuccessColor { get; set; } = "#107C10";
        public string TextPrimary { get; set; } = "#FFFFFF";
        public string TextSecondary { get; set; } = "#AAAAAA";
        public string WindowBackground { get; set; } = "#80000000";
        public string AcrylicTintColor { get; set; } = "#99000000";
    }

    public class WebStyles
    {
        public List<string> HideElements { get; set; } = new List<string>();
        public string CustomCSS { get; set; } = "";
    }

    public class ThemeData
    {
        public string Name { get; set; } = "默认主题";
        public string Author { get; set; } = "系统";
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; } = "澪一无界默认主题";
        public ThemeColors Colors { get; set; } = new ThemeColors();
        public ThemeStyles Styles { get; set; } = new ThemeStyles();
        public WebStyles WebStyles { get; set; } = new WebStyles();
    }

    public static class ThemeManager
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttribData
        {
            public WindowCompositionAttrib Attrib;
            public IntPtr pvData;
            public int cbData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private enum WindowCompositionAttrib
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        private const string ThemesDir = "themes";
        public static ThemeData CurrentTheme { get; private set; } = new ThemeData();
        public static Dictionary<string, ThemeData> AvailableThemes { get; private set; } = new Dictionary<string, ThemeData>();

        public static event Action<ThemeData>? ThemeChanged;

        public static void Initialize()
        {
            LoadAllThemes();
        }

        public static void LoadAllThemes()
        {
            AvailableThemes.Clear();
            AvailableThemes["默认主题"] = new ThemeData();

            if (!Directory.Exists(ThemesDir))
            {
                Directory.CreateDirectory(ThemesDir);
            }

            if (Directory.GetFiles(ThemesDir, "*.json").Length == 0)
            {
                CreateDefaultThemes();
            }

            foreach (string themeFile in Directory.GetFiles(ThemesDir, "*.json"))
            {
                try
                {
                    string themeName = Path.GetFileNameWithoutExtension(themeFile);
                    string json = File.ReadAllText(themeFile);
                    var theme = JsonSerializer.Deserialize<ThemeData>(json);
                    if (theme != null)
                    {
                        theme.Name = themeName;
                        if (!AvailableThemes.ContainsKey(themeName))
                        {
                            AvailableThemes[themeName] = theme;
                        }
                    }
                }
                catch { }
            }
        }

        private static void CreateDefaultThemes()
        {
            CreateTheme("暗夜紫", new ThemeData
            {
                Name = "暗夜紫",
                Author = "系统",
                Description = "深邃优雅的紫色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#CC2D1B4E",
                    ButtonBackground = "#4C1D95",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#5B21B6",
                    ButtonBorder = "#7C3AED",
                    PanelBackground = "#F01E1B4E",
                    BorderColor = "#7C3AED",
                    AccentColor = "#A78BFA",
                    DangerColor = "#DC2626",
                    SuccessColor = "#16A34A",
                    WindowBackground = "#CC1E1B4E"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 6,
                    PanelCornerRadius = 12
                }
            });

            CreateTheme("海洋蓝", new ThemeData
            {
                Name = "海洋蓝",
                Author = "系统",
                Description = "清新明亮的蓝色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#CC0C4A6E",
                    ButtonBackground = "#0284C7",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#0EA5E9",
                    ButtonBorder = "#38BDF8",
                    PanelBackground = "#F00C4A6E",
                    BorderColor = "#38BDF8",
                    AccentColor = "#7DD3FC",
                    DangerColor = "#DC2626",
                    SuccessColor = "#16A34A",
                    WindowBackground = "#CC0C4A6E"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 6,
                    PanelCornerRadius = 12
                }
            });

            CreateTheme("棉花糖", new ThemeData
            {
                Name = "棉花糖",
                Author = "系统",
                Description = "柔和梦幻的粉紫色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#E6FFCCE2",
                    ButtonBackground = "#FFCCEF",
                    ButtonForeground = "#8B5A6B",
                    ButtonHover = "#FFCCFC",
                    ButtonBorder = "#F6CCFF",
                    PanelBackground = "#F5F0FF",
                    BorderColor = "#DCCCFF",
                    AccentColor = "#E6A8D4",
                    DangerColor = "#FF6B8A",
                    SuccessColor = "#7DD3A8",
                    WindowBackground = "#E6F6CCFF",
                    InputBackground = "#FFFFFF",
                    InputForeground = "#6B5A7A",
                    TextPrimary = "#5A4A5A",
                    TextSecondary = "#8A7A8A"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 12,
                    PanelCornerRadius = 20,
                    ButtonOpacity = 0.95,
                    PanelOpacity = 0.98,
                    ButtonBorderThickness = 1.5,
                    ButtonFontWeight = "Medium"
                }
            });

            CreateTheme("磨砂玻璃", new ThemeData
            {
                Name = "磨砂玻璃",
                Author = "系统",
                Description = "半透明磨砂玻璃效果",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#80000000",
                    ButtonBackground = "#40FFFFFF",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#60FFFFFF",
                    ButtonBorder = "#50FFFFFF",
                    PanelBackground = "#D01A1A1A",
                    BorderColor = "#50FFFFFF",
                    AccentColor = "#60A5FA",
                    DangerColor = "#F87171",
                    SuccessColor = "#4ADE80",
                    AcrylicTintColor = "#80000000"
                },
                Styles = new ThemeStyles
                {
                    EnableAcrylic = true,
                    AcrylicOpacity = 0.6,
                    ButtonCornerRadius = 10,
                    PanelCornerRadius = 20,
                    ButtonOpacity = 0.85,
                    PanelOpacity = 0.85,
                    EnableButtonShadow = false,
                    EnablePanelShadow = false,
                    ButtonBorderThickness = 1
                }
            });

            CreateTheme("简约现代", new ThemeData
            {
                Name = "简约现代",
                Author = "系统",
                Description = "简洁现代的灰色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#E62D2D2D",
                    ButtonBackground = "#404040",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#525252",
                    ButtonBorder = "#737373",
                    PanelBackground = "#F0353535",
                    BorderColor = "#525252",
                    AccentColor = "#60A5FA",
                    DangerColor = "#EF4444",
                    SuccessColor = "#22C55E",
                    WindowBackground = "#E62D2D2D"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 8,
                    PanelCornerRadius = 16,
                    ButtonBorderThickness = 1,
                    ButtonFontWeight = "Medium"
                }
            });

            CreateTheme("薄荷绿", new ThemeData
            {
                Name = "薄荷绿",
                Author = "系统",
                Description = "清新自然的绿色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#CC064E3B",
                    ButtonBackground = "#059669",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#10B981",
                    ButtonBorder = "#34D399",
                    PanelBackground = "#F0064E3B",
                    BorderColor = "#34D399",
                    AccentColor = "#6EE7B7",
                    DangerColor = "#DC2626",
                    SuccessColor = "#16A34A",
                    WindowBackground = "#CC064E3B"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 6,
                    PanelCornerRadius = 12
                }
            });

            CreateTheme("暖橙", new ThemeData
            {
                Name = "暖橙",
                Author = "系统",
                Description = "温暖活力的橙色调",
                Colors = new ThemeColors
                {
                    TopBarBackground = "#CC7C2D12",
                    ButtonBackground = "#EA580C",
                    ButtonForeground = "#FFFFFF",
                    ButtonHover = "#F97316",
                    ButtonBorder = "#FB923C",
                    PanelBackground = "#F07C2D12",
                    BorderColor = "#FB923C",
                    AccentColor = "#FDBA74",
                    DangerColor = "#DC2626",
                    SuccessColor = "#16A34A",
                    WindowBackground = "#CC7C2D12"
                },
                Styles = new ThemeStyles
                {
                    ButtonCornerRadius = 6,
                    PanelCornerRadius = 12
                }
            });
        }

        public static void CreateTheme(string themeName, ThemeData theme)
        {
            theme.Name = themeName;
            string themeFile = Path.Combine(ThemesDir, $"{themeName}.json");
            string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(themeFile, json);
        }

        public static bool ApplyTheme(string themeName)
        {
            if (AvailableThemes.TryGetValue(themeName, out var theme))
            {
                CurrentTheme = theme;
                ThemeChanged?.Invoke(theme);
                return true;
            }
            return false;
        }

        public static void EnableAcrylic(Window window, bool enable, string tintColor, double opacity)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var accent = new AccentPolicy();
            if (enable)
            {
                accent.AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
                accent.AccentFlags = 2;
                var color = HexToColor(tintColor);
                accent.GradientColor = (int)((uint)(color.A * opacity) << 24 | (uint)color.B << 16 | (uint)color.G << 8 | (uint)color.R);
            }
            else
            {
                accent.AccentState = AccentState.ACCENT_DISABLED;
            }

            var data = new WindowCompositionAttribData
            {
                Attrib = WindowCompositionAttrib.WCA_ACCENT_POLICY,
                pvData = Marshal.AllocHGlobal(Marshal.SizeOf(accent)),
                cbData = Marshal.SizeOf(accent)
            };

            Marshal.StructureToPtr(accent, data.pvData, false);
            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(data.pvData);
        }

        public static Color HexToColor(string hex)
        {
            try
            {
                hex = hex.Replace("#", "");
                if (hex.Length == 8)
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
                else if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Colors.Black;
        }

        public static SolidColorBrush GetBrush(string hex)
        {
            return new SolidColorBrush(HexToColor(hex));
        }

        public static CornerRadius GetCornerRadius(double radius)
        {
            return new CornerRadius(radius);
        }

        public static Thickness GetThickness(double value)
        {
            return new Thickness(value);
        }

        public static FontWeight GetFontWeight(string weight)
        {
            return weight switch
            {
                "Thin" => FontWeights.Thin,
                "ExtraLight" => FontWeights.ExtraLight,
                "Light" => FontWeights.Light,
                "Medium" => FontWeights.Medium,
                "SemiBold" => FontWeights.SemiBold,
                "Bold" => FontWeights.Bold,
                "ExtraBold" => FontWeights.ExtraBold,
                "Black" => FontWeights.Black,
                _ => FontWeights.Normal
            };
        }
    }
}
