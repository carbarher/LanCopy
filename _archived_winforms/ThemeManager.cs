using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Tema de la aplicaciÃ³n
    /// </summary>
    public class AppTheme
    {
        public string Name { get; set; } = "Dark";
        public Color BackgroundColor { get; set; } = Color.FromArgb(30, 30, 30);
        public Color ForegroundColor { get; set; } = Color.White;
        public Color PrimaryColor { get; set; } = Color.FromArgb(0, 122, 204);
        public Color SecondaryColor { get; set; } = Color.FromArgb(45, 45, 45);
        public Color AccentColor { get; set; } = Color.FromArgb(0, 153, 255);
        public Color ButtonColor { get; set; } = Color.FromArgb(60, 60, 60);
        public Color ButtonHoverColor { get; set; } = Color.FromArgb(80, 80, 80);
        public Color TextBoxColor { get; set; } = Color.FromArgb(40, 40, 40);
        public Color BorderColor { get; set; } = Color.FromArgb(60, 60, 60);
        public Color SuccessColor { get; set; } = Color.FromArgb(0, 200, 81);
        public Color WarningColor { get; set; } = Color.FromArgb(255, 193, 7);
        public Color ErrorColor { get; set; } = Color.FromArgb(244, 67, 54);
        public string FontName { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 10f;
    }

    /// <summary>
    /// Gestor de temas de la aplicaciÃ³n
    /// </summary>
    public class ThemeManager
    {
        private readonly string _themesFile = "themes.json";
        private readonly string _currentThemeFile = "current_theme.txt";
        private Dictionary<string, AppTheme> _themes = new Dictionary<string, AppTheme>();
        private AppTheme _currentTheme;

        public AppTheme CurrentTheme => _currentTheme;

        public ThemeManager()
        {
            LoadThemes();
            LoadCurrentTheme();
        }

        /// <summary>
        /// Adjunta manejadores para aplicar tema a controles agregados dinámicamente
        /// </summary>
        private void AttachDynamicTheming(Control parent)
        {
            if (parent == null) return;

            parent.ControlAdded -= Parent_ControlAdded; // evitar doble registro
            parent.ControlAdded += Parent_ControlAdded;

            foreach (Control child in parent.Controls)
            {
                // Recursivo para todos los contenedores
                if (child.Controls.Count > 0)
                {
                    AttachDynamicTheming(child);
                }
            }
        }

        private void Parent_ControlAdded(object sender, ControlEventArgs e)
        {
            try
            {
                // Aplicar tema al control recién agregado y a sus hijos
                ApplyThemeToControls(new Control.ControlCollection(e.Control));
                // La línea anterior no es válida; aplicar directamente al control y luego a sus hijos
            }
            catch { }

            try
            {
                // Fallback correcto
                if (e?.Control != null)
                {
                    // Aplicar al control
                    try
                    {
                        // Emular lógica principal
                        if (e.Control is Panel || e.Control is GroupBox || e.Control is TabControl || e.Control is TabPage || e.Control is UserControl)
                        {
                            e.Control.BackColor = _currentTheme.SecondaryColor;
                            e.Control.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is Button btn)
                        {
                            btn.BackColor = _currentTheme.ButtonColor;
                            btn.ForeColor = _currentTheme.ForegroundColor;
                            btn.FlatStyle = FlatStyle.Flat;
                            btn.FlatAppearance.BorderColor = _currentTheme.BorderColor;
                        }
                        else if (e.Control is TextBox || e.Control is ComboBox || e.Control is NumericUpDown)
                        {
                            e.Control.BackColor = _currentTheme.TextBoxColor;
                            e.Control.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is ListView lv)
                        {
                            lv.BackColor = _currentTheme.BackgroundColor;
                            lv.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is ListBox || e.Control is CheckedListBox)
                        {
                            e.Control.BackColor = _currentTheme.BackgroundColor;
                            e.Control.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is Label || e.Control is CheckBox || e.Control is RadioButton)
                        {
                            e.Control.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is LinkLabel link)
                        {
                            link.LinkColor = _currentTheme.AccentColor;
                            link.ActiveLinkColor = _currentTheme.AccentColor;
                            link.VisitedLinkColor = _currentTheme.AccentColor;
                            link.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is RichTextBox rtb)
                        {
                            rtb.BackColor = _currentTheme.TextBoxColor;
                            rtb.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is TreeView tv)
                        {
                            tv.BackColor = _currentTheme.BackgroundColor;
                            tv.ForeColor = _currentTheme.ForegroundColor;
                        }
                        else if (e.Control is DataGridView dgv)
                        {
                            dgv.BackgroundColor = _currentTheme.BackgroundColor;
                            dgv.GridColor = _currentTheme.BorderColor;
                            dgv.ForeColor = _currentTheme.ForegroundColor;
                            dgv.DefaultCellStyle.BackColor = _currentTheme.SecondaryColor;
                            dgv.DefaultCellStyle.ForeColor = _currentTheme.ForegroundColor;
                            dgv.ColumnHeadersDefaultCellStyle.BackColor = _currentTheme.SecondaryColor;
                            dgv.ColumnHeadersDefaultCellStyle.ForeColor = _currentTheme.ForegroundColor;
                            dgv.EnableHeadersVisualStyles = false;
                        }

                        // Fallback si quedó negro
                        if (e.Control.ForeColor == Color.Black || e.Control.ForeColor == SystemColors.ControlText)
                        {
                            e.Control.ForeColor = _currentTheme.ForegroundColor;
                        }
                    }
                    catch { }

                    // Aplicar a hijos
                    if (e.Control.Controls.Count > 0)
                    {
                        ApplyThemeToControls(e.Control.Controls);
                        AttachDynamicTheming(e.Control);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Carga los temas predefinidos
        /// </summary>
        private void LoadThemes()
        {
            // Tema Dark (por defecto)
            _themes["Dark"] = new AppTheme
            {
                Name = "Dark",
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForegroundColor = Color.White,
                PrimaryColor = Color.FromArgb(0, 122, 204),
                SecondaryColor = Color.FromArgb(45, 45, 45),
                AccentColor = Color.FromArgb(0, 153, 255),
                ButtonColor = Color.FromArgb(60, 60, 60),
                ButtonHoverColor = Color.FromArgb(80, 80, 80),
                TextBoxColor = Color.FromArgb(40, 40, 40),
                BorderColor = Color.FromArgb(60, 60, 60),
                SuccessColor = Color.FromArgb(0, 200, 81),
                WarningColor = Color.FromArgb(255, 193, 7),
                ErrorColor = Color.FromArgb(244, 67, 54)
            };

            // Tema Light
            _themes["Light"] = new AppTheme
            {
                Name = "Light",
                BackgroundColor = Color.FromArgb(245, 245, 245),
                ForegroundColor = Color.Black,
                PrimaryColor = Color.FromArgb(0, 122, 204),
                SecondaryColor = Color.FromArgb(230, 230, 230),
                AccentColor = Color.FromArgb(0, 153, 255),
                ButtonColor = Color.FromArgb(225, 225, 225),
                ButtonHoverColor = Color.FromArgb(200, 200, 200),
                TextBoxColor = Color.White,
                BorderColor = Color.FromArgb(200, 200, 200),
                SuccessColor = Color.FromArgb(76, 175, 80),
                WarningColor = Color.FromArgb(255, 152, 0),
                ErrorColor = Color.FromArgb(244, 67, 54)
            };

            // Tema Dracula
            _themes["Dracula"] = new AppTheme
            {
                Name = "Dracula",
                BackgroundColor = Color.FromArgb(40, 42, 54),
                ForegroundColor = Color.FromArgb(248, 248, 242),
                PrimaryColor = Color.FromArgb(189, 147, 249),
                SecondaryColor = Color.FromArgb(68, 71, 90),
                AccentColor = Color.FromArgb(255, 121, 198),
                ButtonColor = Color.FromArgb(68, 71, 90),
                ButtonHoverColor = Color.FromArgb(98, 114, 164),
                TextBoxColor = Color.FromArgb(68, 71, 90),
                BorderColor = Color.FromArgb(98, 114, 164),
                SuccessColor = Color.FromArgb(80, 250, 123),
                WarningColor = Color.FromArgb(241, 250, 140),
                ErrorColor = Color.FromArgb(255, 85, 85)
            };

            // Tema Monokai
            _themes["Monokai"] = new AppTheme
            {
                Name = "Monokai",
                BackgroundColor = Color.FromArgb(39, 40, 34),
                ForegroundColor = Color.FromArgb(248, 248, 242),
                PrimaryColor = Color.FromArgb(102, 217, 239),
                SecondaryColor = Color.FromArgb(73, 72, 62),
                AccentColor = Color.FromArgb(249, 38, 114),
                ButtonColor = Color.FromArgb(73, 72, 62),
                ButtonHoverColor = Color.FromArgb(117, 113, 94),
                TextBoxColor = Color.FromArgb(73, 72, 62),
                BorderColor = Color.FromArgb(117, 113, 94),
                SuccessColor = Color.FromArgb(166, 226, 46),
                WarningColor = Color.FromArgb(253, 151, 31),
                ErrorColor = Color.FromArgb(249, 38, 114)
            };

            // Tema Nord
            _themes["Nord"] = new AppTheme
            {
                Name = "Nord",
                BackgroundColor = Color.FromArgb(46, 52, 64),
                ForegroundColor = Color.FromArgb(236, 239, 244),
                PrimaryColor = Color.FromArgb(136, 192, 208),
                SecondaryColor = Color.FromArgb(59, 66, 82),
                AccentColor = Color.FromArgb(129, 161, 193),
                ButtonColor = Color.FromArgb(59, 66, 82),
                ButtonHoverColor = Color.FromArgb(76, 86, 106),
                TextBoxColor = Color.FromArgb(59, 66, 82),
                BorderColor = Color.FromArgb(76, 86, 106),
                SuccessColor = Color.FromArgb(163, 190, 140),
                WarningColor = Color.FromArgb(235, 203, 139),
                ErrorColor = Color.FromArgb(191, 97, 106)
            };

            // Tema Solarized Dark
            _themes["Solarized Dark"] = new AppTheme
            {
                Name = "Solarized Dark",
                BackgroundColor = Color.FromArgb(0, 43, 54),
                ForegroundColor = Color.FromArgb(131, 148, 150),
                PrimaryColor = Color.FromArgb(38, 139, 210),
                SecondaryColor = Color.FromArgb(7, 54, 66),
                AccentColor = Color.FromArgb(42, 161, 152),
                ButtonColor = Color.FromArgb(7, 54, 66),
                ButtonHoverColor = Color.FromArgb(88, 110, 117),
                TextBoxColor = Color.FromArgb(7, 54, 66),
                BorderColor = Color.FromArgb(88, 110, 117),
                SuccessColor = Color.FromArgb(133, 153, 0),
                WarningColor = Color.FromArgb(181, 137, 0),
                ErrorColor = Color.FromArgb(220, 50, 47)
            };

            // Cargar temas personalizados si existen
            if (File.Exists(_themesFile))
            {
                try
                {
                    var json = File.ReadAllText(_themesFile);
                    var customThemes = JsonSerializer.Deserialize<Dictionary<string, AppTheme>>(json);
                    if (customThemes != null)
                    {
                        foreach (var kvp in customThemes)
                        {
                            _themes[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cargando temas personalizados: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Carga el tema actual
        /// </summary>
        private void LoadCurrentTheme()
        {
            string themeName = "Dark";
            
            if (File.Exists(_currentThemeFile))
            {
                try
                {
                    themeName = File.ReadAllText(_currentThemeFile).Trim();
                }
                catch { }
            }

            _currentTheme = _themes.ContainsKey(themeName) ? _themes[themeName] : _themes["Dark"];
        }

        /// <summary>
        /// Aplica un tema a un formulario
        /// </summary>
        public void ApplyTheme(Form form)
        {
            if (form == null || _currentTheme == null)
                return;

            form.BackColor = _currentTheme.BackgroundColor;
            form.ForeColor = _currentTheme.ForegroundColor;
            form.Font = new Font(_currentTheme.FontName, _currentTheme.FontSize);

            ApplyThemeToControls(form.Controls);
            AttachDynamicTheming(form);
        }

        /// <summary>
        /// Aplica el tema a todos los controles recursivamente
        /// </summary>
        private void ApplyThemeToControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                // Respetar controles marcados para no aplicar tema
                if (control.Tag is string tag && (tag == "SkipTheme" || tag == "KeepColor"))
                {
                    // Aún así aplicar a hijos
                    if (control.Controls.Count > 0)
                    {
                        ApplyThemeToControls(control.Controls);
                    }
                    continue;
                }
                // Paneles y contenedores (incluye UserControl)
                if (control is Panel || control is GroupBox || control is TabControl || control is TabPage || control is UserControl)
                {
                    control.BackColor = _currentTheme.SecondaryColor;
                    control.ForeColor = _currentTheme.ForegroundColor;
                }
                // Botones
                else if (control is Button button)
                {
                    button.BackColor = _currentTheme.ButtonColor;
                    button.ForeColor = _currentTheme.ForegroundColor;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = _currentTheme.BorderColor;
                }
                // TextBox, ComboBox, NumericUpDown
                else if (control is TextBox || control is ComboBox || control is NumericUpDown)
                {
                    control.BackColor = _currentTheme.TextBoxColor;
                    control.ForeColor = _currentTheme.ForegroundColor;
                }
                // ListView
                else if (control is ListView listView)
                {
                    listView.BackColor = _currentTheme.BackgroundColor;
                    listView.ForeColor = _currentTheme.ForegroundColor;
                }
                // ListBox / CheckedListBox
                else if (control is ListBox || control is CheckedListBox)
                {
                    control.BackColor = _currentTheme.BackgroundColor;
                    control.ForeColor = _currentTheme.ForegroundColor;
                }
                // Label
                else if (control is Label)
                {
                    control.ForeColor = _currentTheme.ForegroundColor;
                }
                // CheckBox, RadioButton
                else if (control is CheckBox || control is RadioButton)
                {
                    control.ForeColor = _currentTheme.ForegroundColor;
                }
                // LinkLabel
                else if (control is LinkLabel link)
                {
                    link.LinkColor = _currentTheme.AccentColor;
                    link.ActiveLinkColor = _currentTheme.AccentColor;
                    link.VisitedLinkColor = _currentTheme.AccentColor;
                    link.ForeColor = _currentTheme.ForegroundColor;
                }
                // RichTextBox
                else if (control is RichTextBox rtb)
                {
                    rtb.BackColor = _currentTheme.TextBoxColor;
                    rtb.ForeColor = _currentTheme.ForegroundColor;
                }
                // TreeView
                else if (control is TreeView tv)
                {
                    tv.BackColor = _currentTheme.BackgroundColor;
                    tv.ForeColor = _currentTheme.ForegroundColor;
                }
                // DataGridView
                else if (control is DataGridView dgv)
                {
                    dgv.BackgroundColor = _currentTheme.BackgroundColor;
                    dgv.GridColor = _currentTheme.BorderColor;
                    dgv.ForeColor = _currentTheme.ForegroundColor;
                    dgv.DefaultCellStyle.BackColor = _currentTheme.SecondaryColor;
                    dgv.DefaultCellStyle.ForeColor = _currentTheme.ForegroundColor;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = _currentTheme.SecondaryColor;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = _currentTheme.ForegroundColor;
                    dgv.EnableHeadersVisualStyles = false;
                }
                // ToolStrips y menús
                else if (control is ToolStrip ts)
                {
                    ts.BackColor = _currentTheme.SecondaryColor;
                    ts.ForeColor = _currentTheme.ForegroundColor;
                    foreach (ToolStripItem item in ts.Items)
                    {
                        item.ForeColor = _currentTheme.ForegroundColor;
                    }
                }
                else if (control is ContextMenuStrip cms)
                {
                    cms.BackColor = _currentTheme.SecondaryColor;
                    cms.ForeColor = _currentTheme.ForegroundColor;
                    foreach (ToolStripItem item in cms.Items)
                    {
                        item.ForeColor = _currentTheme.ForegroundColor;
                    }
                }
                else if (control is MenuStrip ms)
                {
                    ms.BackColor = _currentTheme.SecondaryColor;
                    ms.ForeColor = _currentTheme.ForegroundColor;
                    foreach (ToolStripItem item in ms.Items)
                    {
                        item.ForeColor = _currentTheme.ForegroundColor;
                    }
                }
                else
                {
                    // Fallback: si el control quedó con texto negro por defecto, forzar color del tema
                    try
                    {
                        if (control.ForeColor == Color.Black || control.ForeColor == SystemColors.ControlText)
                        {
                            control.ForeColor = _currentTheme.ForegroundColor;
                        }
                    }
                    catch { }
                }

                // Aplicar recursivamente a controles hijos
                if (control.Controls.Count > 0)
                {
                    ApplyThemeToControls(control.Controls);
                }
            }
        }

        /// <summary>
        /// Cambia el tema actual
        /// </summary>
        public void SetTheme(string themeName)
        {
            if (_themes.ContainsKey(themeName))
            {
                _currentTheme = _themes[themeName];
                
                try
                {
                    File.WriteAllText(_currentThemeFile, themeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error guardando tema actual: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Obtiene la lista de temas disponibles
        /// </summary>
        public List<string> GetAvailableThemes()
        {
            return new List<string>(_themes.Keys);
        }

        /// <summary>
        /// Guarda un tema personalizado
        /// </summary>
        public void SaveCustomTheme(AppTheme theme)
        {
            _themes[theme.Name] = theme;
            
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_themes, options);
                File.WriteAllText(_themesFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando tema personalizado: {ex.Message}");
            }
        }
    }
}

