using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // SISTEMA DE TEMAS
    // ═══════════════════════════════════════════════════════════════
    
    public class Theme
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        
        public string BackgroundColor { get; set; }
        public string ForegroundColor { get; set; }
        public string AccentColor { get; set; }
        public string HighlightColor { get; set; }
        public string BorderColor { get; set; }
        public string ErrorColor { get; set; }
        public string SuccessColor { get; set; }
        public string WarningColor { get; set; }
        
        public string FontFamily { get; set; }
        public float FontSize { get; set; }
        
        public Dictionary<string, string> CustomColors { get; set; } = new Dictionary<string, string>();
        
        public Color GetBackgroundColor() => ColorTranslator.FromHtml(BackgroundColor);
        public Color GetForegroundColor() => ColorTranslator.FromHtml(ForegroundColor);
        public Color GetAccentColor() => ColorTranslator.FromHtml(AccentColor);
        public Color GetHighlightColor() => ColorTranslator.FromHtml(HighlightColor);
        public Color GetBorderColor() => ColorTranslator.FromHtml(BorderColor);
        public Color GetErrorColor() => ColorTranslator.FromHtml(ErrorColor);
        public Color GetSuccessColor() => ColorTranslator.FromHtml(SuccessColor);
        public Color GetWarningColor() => ColorTranslator.FromHtml(WarningColor);
        
        public Color GetCustomColor(string key)
        {
            return CustomColors.ContainsKey(key) 
                ? ColorTranslator.FromHtml(CustomColors[key]) 
                : Color.Gray;
        }
    }
    
    public class ThemeManager
    {
        private Theme currentTheme;
        private readonly string themesDirectory;
        
        public ThemeManager(string themesDirectory)
        {
            this.themesDirectory = themesDirectory;
            
            if (!Directory.Exists(themesDirectory))
                Directory.CreateDirectory(themesDirectory);
            
            // Crear tema por defecto si no existe
            CreateDefaultThemes();
        }
        
        private void CreateDefaultThemes()
        {
            // Tema oscuro moderno
            var darkTheme = new Theme
            {
                Name = "Dark Modern",
                Author = "SlskDown Team",
                Version = "1.0",
                BackgroundColor = "#1E1E1E",
                ForegroundColor = "#FFFFFF",
                AccentColor = "#0078D4",
                HighlightColor = "#264F78",
                BorderColor = "#3F3F46",
                ErrorColor = "#F44336",
                SuccessColor = "#4CAF50",
                WarningColor = "#FF9800",
                FontFamily = "Segoe UI",
                FontSize = 9f
            };
            
            // Tema claro
            var lightTheme = new Theme
            {
                Name = "Light Modern",
                Author = "SlskDown Team",
                Version = "1.0",
                BackgroundColor = "#FFFFFF",
                ForegroundColor = "#000000",
                AccentColor = "#0078D4",
                HighlightColor = "#CCE4F7",
                BorderColor = "#CCCCCC",
                ErrorColor = "#D32F2F",
                SuccessColor = "#388E3C",
                WarningColor = "#F57C00",
                FontFamily = "Segoe UI",
                FontSize = 9f
            };
            
            SaveTheme(darkTheme);
            SaveTheme(lightTheme);
        }
        
        public void SaveTheme(Theme theme)
        {
            try
            {
                var filename = $"{theme.Name.Replace(" ", "_")}.json";
                var path = Path.Combine(themesDirectory, filename);
                var json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando tema: {ex.Message}");
            }
        }
        
        public Theme LoadTheme(string name)
        {
            try
            {
                var filename = $"{name.Replace(" ", "_")}.json";
                var path = Path.Combine(themesDirectory, filename);
                
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Theme>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando tema: {ex.Message}");
            }
            
            return null;
        }
        
        public List<string> GetAvailableThemes()
        {
            var themes = new List<string>();
            
            try
            {
                var files = Directory.GetFiles(themesDirectory, "*.json");
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file).Replace("_", " ");
                    themes.Add(name);
                }
            }
            catch { }
            
            return themes;
        }
        
        public void ApplyTheme(Control control, Theme theme)
        {
            if (control == null || theme == null)
                return;
            
            currentTheme = theme;
            
            // Aplicar colores
            control.BackColor = theme.GetBackgroundColor();
            control.ForeColor = theme.GetForegroundColor();
            
            // Aplicar fuente
            if (!string.IsNullOrEmpty(theme.FontFamily))
            {
                try
                {
                    control.Font = new Font(theme.FontFamily, theme.FontSize);
                }
                catch { }
            }
            
            // Aplicar recursivamente a controles hijos
            foreach (Control child in control.Controls)
            {
                ApplyThemeToControl(child, theme);
            }
        }
        
        private void ApplyThemeToControl(Control control, Theme theme)
        {
            // Aplicar colores base
            control.BackColor = theme.GetBackgroundColor();
            control.ForeColor = theme.GetForegroundColor();
            
            // Aplicar colores específicos según tipo
            if (control is Button)
            {
                control.BackColor = theme.GetAccentColor();
                control.ForeColor = Color.White;
            }
            else if (control is TextBox || control is ComboBox)
            {
                control.BackColor = Color.FromArgb(
                    Math.Min(theme.GetBackgroundColor().R + 20, 255),
                    Math.Min(theme.GetBackgroundColor().G + 20, 255),
                    Math.Min(theme.GetBackgroundColor().B + 20, 255)
                );
            }
            else if (control is ListView || control is DataGridView)
            {
                control.BackColor = theme.GetBackgroundColor();
                control.ForeColor = theme.GetForegroundColor();
            }
            
            // Aplicar fuente
            if (!string.IsNullOrEmpty(theme.FontFamily))
            {
                try
                {
                    control.Font = new Font(theme.FontFamily, theme.FontSize);
                }
                catch { }
            }
            
            // Recursivo
            foreach (Control child in control.Controls)
            {
                ApplyThemeToControl(child, theme);
            }
        }
        
        public Theme CurrentTheme => currentTheme;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // ATAJOS DE TECLADO
    // ═══════════════════════════════════════════════════════════════
    
    public class KeyboardShortcut
    {
        public Keys Key { get; set; }
        public string Description { get; set; }
        public Action Action { get; set; }
    }
    
    public class KeyboardShortcutManager
    {
        private readonly Dictionary<Keys, KeyboardShortcut> shortcuts = new Dictionary<Keys, KeyboardShortcut>();
        
        public void Register(Keys key, string description, Action action)
        {
            shortcuts[key] = new KeyboardShortcut
            {
                Key = key,
                Description = description,
                Action = action
            };
        }
        
        public bool ProcessKey(Keys keyData)
        {
            if (shortcuts.ContainsKey(keyData))
            {
                try
                {
                    shortcuts[keyData].Action?.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error ejecutando atajo: {ex.Message}");
                }
            }
            
            return false;
        }
        
        public List<KeyboardShortcut> GetAllShortcuts()
        {
            return new List<KeyboardShortcut>(shortcuts.Values);
        }
        
        public void Clear()
        {
            shortcuts.Clear();
        }
        
        public void RegisterDefaultShortcuts(
            Action focusSearch,
            Action newSearchTab,
            Action closeTab,
            Action showDownloads,
            Action showSettings,
            Action[] switchToTab)
        {
            // Búsqueda
            Register(Keys.Control | Keys.F, "Enfocar búsqueda", focusSearch);
            
            // Tabs
            Register(Keys.Control | Keys.T, "Nueva pestaña de búsqueda", newSearchTab);
            Register(Keys.Control | Keys.W, "Cerrar pestaña", closeTab);
            
            // Navegación
            Register(Keys.Control | Keys.D, "Mostrar descargas", showDownloads);
            Register(Keys.Control | Keys.Oemcomma, "Mostrar configuración", showSettings);
            
            // Cambiar a tabs (Ctrl+1 a Ctrl+9)
            for (int i = 0; i < Math.Min(switchToTab.Length, 9); i++)
            {
                var index = i;
                Register(Keys.Control | (Keys.D1 + i), $"Cambiar a pestaña {i + 1}", switchToTab[index]);
            }
            
            // Acciones rápidas
            Register(Keys.F5, "Actualizar", () => { });
            Register(Keys.Escape, "Cancelar operación actual", () => { });
            Register(Keys.Control | Keys.A, "Seleccionar todo", () => { });
        }
    }
}
