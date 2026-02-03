using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Dark-themed file dialog wrappers.
    /// Uses Windows API to enable dark mode for OpenFileDialog and SaveFileDialog.
    /// </summary>
    public static class DarkFileDialog
    {
        [DllImport("uxtheme.dll", EntryPoint = "#138", CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
        private static extern void AllowDarkModeForApp(bool allow);

        private const int DARK_MODE = 2;

        /// <summary>
        /// Shows an OpenFileDialog with dark mode enabled.
        /// </summary>
        public static string ShowOpenDialog(string title = "Abrir archivo",
                                           string filter = "Todos los archivos (*.*)|*.*",
                                           string initialDirectory = null,
                                           bool multiselect = false)
        {
            try
            {
                // Enable dark mode
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch { }

                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    dialog.Multiselect = multiselect;
                    
                    if (!string.IsNullOrWhiteSpace(initialDirectory))
                    {
                        dialog.InitialDirectory = initialDirectory;
                    }

                    try
                    {
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            return dialog.FileName;
                        }
                    }
                    finally
                    {
                        try { SetPreferredAppMode(0); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el diálogo de archivo:\n{ex.Message}",
                                   "Error",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Shows an OpenFileDialog with dark mode enabled (overload with owner).
        /// </summary>
        public static string ShowOpenDialog(IWin32Window owner,
                                           string title = "Abrir archivo",
                                           string filter = "Todos los archivos (*.*)|*.*",
                                           string initialDirectory = null,
                                           bool multiselect = false)
        {
            try
            {
                // Enable dark mode
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch { }

                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    dialog.Multiselect = multiselect;
                    
                    if (!string.IsNullOrWhiteSpace(initialDirectory))
                    {
                        dialog.InitialDirectory = initialDirectory;
                    }

                    try
                    {
                        if (dialog.ShowDialog(owner) == DialogResult.OK)
                        {
                            return dialog.FileName;
                        }
                    }
                    finally
                    {
                        try { SetPreferredAppMode(0); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el diálogo de archivo:\n{ex.Message}",
                                   "Error",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Shows a SaveFileDialog with dark mode enabled.
        /// </summary>
        public static string ShowSaveDialog(string title = "Guardar archivo",
                                           string filter = "Todos los archivos (*.*)|*.*",
                                           string defaultFileName = null,
                                           string initialDirectory = null)
        {
            try
            {
                // Enable dark mode
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch { }

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    
                    if (!string.IsNullOrWhiteSpace(defaultFileName))
                    {
                        dialog.FileName = defaultFileName;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(initialDirectory))
                    {
                        dialog.InitialDirectory = initialDirectory;
                    }

                    try
                    {
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            return dialog.FileName;
                        }
                    }
                    finally
                    {
                        try { SetPreferredAppMode(0); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el diálogo de guardado:\n{ex.Message}",
                                   "Error",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Shows a SaveFileDialog with dark mode enabled (overload with owner).
        /// </summary>
        public static string ShowSaveDialog(IWin32Window owner,
                                           string title = "Guardar archivo",
                                           string filter = "Todos los archivos (*.*)|*.*",
                                           string defaultFileName = null,
                                           string initialDirectory = null)
        {
            try
            {
                // Enable dark mode
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch { }

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    
                    if (!string.IsNullOrWhiteSpace(defaultFileName))
                    {
                        dialog.FileName = defaultFileName;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(initialDirectory))
                    {
                        dialog.InitialDirectory = initialDirectory;
                    }

                    try
                    {
                        if (dialog.ShowDialog(owner) == DialogResult.OK)
                        {
                            return dialog.FileName;
                        }
                    }
                    finally
                    {
                        try { SetPreferredAppMode(0); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el diálogo de guardado:\n{ex.Message}",
                                   "Error",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
            }

            return null;
        }
    }
}
