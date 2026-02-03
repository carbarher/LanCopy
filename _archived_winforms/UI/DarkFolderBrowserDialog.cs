using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Dark-themed folder browser dialog wrapper.
    /// Uses modern Windows folder picker with dark mode support.
    /// </summary>
    public static class DarkFolderBrowserDialog
    {
        [DllImport("uxtheme.dll", EntryPoint = "#138", CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
        private static extern void AllowDarkModeForApp(bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#133", CharSet = CharSet.Unicode)]
        private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        private const int DARK_MODE = 2;

        /// <summary>
        /// Shows a folder browser dialog with dark mode enabled.
        /// </summary>
        /// <param name="description">Description text shown in the dialog</param>
        /// <param name="selectedPath">Initially selected path</param>
        /// <param name="showNewFolderButton">Whether to show the "New Folder" button</param>
        /// <returns>Selected path if OK was clicked, null otherwise</returns>
        public static string ShowDialog(string description = "Seleccionar carpeta", 
                                       string selectedPath = null, 
                                       bool showNewFolderButton = true)
        {
            try
            {
                // Enable dark mode for the dialog
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch
                {
                    // Dark mode API not available (older Windows versions)
                    // Continue with standard dialog
                }

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = description;
                    dialog.ShowNewFolderButton = showNewFolderButton;
                    
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        dialog.SelectedPath = selectedPath;
                    }

                    // Try to apply dark mode to the dialog window
                    try
                    {
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            return dialog.SelectedPath;
                        }
                    }
                    finally
                    {
                        // Reset dark mode preference
                        try
                        {
                            SetPreferredAppMode(0);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el selector de carpetas:\n{ex.Message}", 
                                   "Error", 
                                   MessageBoxButtons.OK, 
                                   MessageBoxIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Shows a folder browser dialog with dark mode enabled (overload with owner).
        /// </summary>
        public static string ShowDialog(IWin32Window owner, 
                                       string description = "Seleccionar carpeta", 
                                       string selectedPath = null, 
                                       bool showNewFolderButton = true)
        {
            try
            {
                // Enable dark mode for the dialog
                try
                {
                    AllowDarkModeForApp(true);
                    SetPreferredAppMode(DARK_MODE);
                }
                catch
                {
                    // Dark mode API not available (older Windows versions)
                }

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = description;
                    dialog.ShowNewFolderButton = showNewFolderButton;
                    
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        dialog.SelectedPath = selectedPath;
                    }

                    try
                    {
                        if (dialog.ShowDialog(owner) == DialogResult.OK)
                        {
                            return dialog.SelectedPath;
                        }
                    }
                    finally
                    {
                        // Reset dark mode preference
                        try
                        {
                            SetPreferredAppMode(0);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // ERROR: DarkMessageBox.Show($"Error al abrir el selector de carpetas:\n{ex.Message}", 
                                   "Error", 
                                   MessageBoxButtons.OK, 
                                   MessageBoxIcon.Error);
            }

            return null;
        }
    }
}
