using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona actualizaciones de UI de forma centralizada y thread-safe
    /// </summary>
    public class UIManager
    {
        private readonly Control mainControl;
        
        // Callbacks
        public Action<string> OnLog { get; set; }
        
        public UIManager(Control control)
        {
            mainControl = control ?? throw new ArgumentNullException(nameof(control));
        }
        
        /// <summary>
        /// Ejecuta una acción en el thread de UI de forma segura
        /// </summary>
        public void SafeInvoke(Action action)
        {
            if (action == null) return;
            
            try
            {
                // Verificar que el control no sea null y no esté destruido
                if (mainControl == null || mainControl.IsDisposed)
                    return;
                    
                if (mainControl.InvokeRequired)
                {
                    mainControl.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // Control ya fue destruido, ignorar
            }
            catch (InvalidOperationException)
            {
                // Control no válido, ignorar
            }
            catch (NullReferenceException ex)
            {
                // Log más detallado para NullReferenceException
                OnLog?.Invoke($"Error en SafeInvoke (NullReference): {ex.Message}\nStack: {ex.StackTrace?.Substring(0, Math.Min(200, ex.StackTrace?.Length ?? 0))}");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error en SafeInvoke: {ex.GetType().Name} - {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualiza un ListViewItem de forma thread-safe
        /// </summary>
        public void UpdateListViewItem(ListViewItem item, int columnIndex, string text, Color? foreColor = null)
        {
            if (item == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    if (columnIndex >= 0 && columnIndex < item.SubItems.Count)
                    {
                        item.SubItems[columnIndex].Text = text;
                        
                        if (foreColor.HasValue)
                        {
                            item.ForeColor = foreColor.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando ListView: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Actualiza múltiples columnas de un ListViewItem
        /// </summary>
        public void UpdateListViewItem(ListViewItem item, Dictionary<int, string> updates, Color? foreColor = null)
        {
            if (item == null || updates == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    foreach (var kvp in updates)
                    {
                        if (kvp.Key >= 0 && kvp.Key < item.SubItems.Count)
                        {
                            item.SubItems[kvp.Key].Text = kvp.Value;
                        }
                    }
                    
                    if (foreColor.HasValue)
                    {
                        item.ForeColor = foreColor.Value;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando ListView: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Agrega items a un ListView con BeginUpdate/EndUpdate
        /// </summary>
        public void AddListViewItems(ListView listView, ListViewItem[] items)
        {
            if (listView == null || items == null || items.Length == 0) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    listView.BeginUpdate();
                    listView.Items.AddRange(items);
                }
                finally
                {
                    listView.EndUpdate();
                }
            });
        }
        
        /// <summary>
        /// Limpia un ListView de forma eficiente
        /// </summary>
        public void ClearListView(ListView listView)
        {
            if (listView == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    listView.BeginUpdate();
                    listView.Items.Clear();
                }
                finally
                {
                    listView.EndUpdate();
                }
            });
        }
        
        /// <summary>
        /// Actualiza el texto de un Label
        /// </summary>
        public void UpdateLabel(Label label, string text)
        {
            if (label == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    label.Text = text;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando Label: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Actualiza una ProgressBar
        /// </summary>
        public void UpdateProgressBar(ProgressBar progressBar, int value, int maximum = 100)
        {
            if (progressBar == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    progressBar.Maximum = maximum;
                    progressBar.Value = Math.Min(Math.Max(0, value), maximum);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando ProgressBar: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Actualiza el estado de un botón
        /// </summary>
        public void UpdateButton(Button button, bool enabled, string text = null)
        {
            if (button == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    button.Enabled = enabled;
                    if (text != null)
                    {
                        button.Text = text;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando Button: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Aplica tema oscuro a un Form
        /// </summary>
        public void ApplyDarkTheme(Form form)
        {
            if (form == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    form.BackColor = Color.FromArgb(45, 45, 48);
                    form.ForeColor = Color.White;
                    
                    ApplyDarkThemeToControls(form.Controls);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error aplicando tema oscuro: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Aplica tema oscuro recursivamente a controles
        /// </summary>
        private void ApplyDarkThemeToControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                // Colores base
                if (!(control is Button || control is LinkLabel))
                {
                    control.BackColor = Color.FromArgb(45, 45, 48);
                    control.ForeColor = Color.White;
                }
                
                // Controles específicos
                if (control is TextBox textBox)
                {
                    textBox.BackColor = Color.FromArgb(30, 30, 30);
                    textBox.ForeColor = Color.White;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is ListView listView)
                {
                    listView.BackColor = Color.FromArgb(30, 30, 30);
                    listView.ForeColor = Color.White;
                }
                else if (control is Button button)
                {
                    button.BackColor = Color.FromArgb(60, 60, 60);
                    button.ForeColor = Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.BackColor = Color.FromArgb(30, 30, 30);
                    comboBox.ForeColor = Color.White;
                    comboBox.FlatStyle = FlatStyle.Flat;
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.FlatStyle = FlatStyle.Flat;
                }
                
                // Recursivo para controles contenedores
                if (control.Controls.Count > 0)
                {
                    ApplyDarkThemeToControls(control.Controls);
                }
            }
        }
        
        /// <summary>
        /// Muestra un mensaje de error thread-safe
        /// </summary>
        public void ShowError(string message, string title = "Error")
        {
            SafeInvoke(() =>
            {
                try
                {
                    MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error mostrando mensaje: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Muestra un mensaje de información thread-safe
        /// </summary>
        public void ShowInfo(string message, string title = "Información")
        {
            SafeInvoke(() =>
            {
                try
                {
                    MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error mostrando mensaje: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Muestra un diálogo de confirmación thread-safe
        /// </summary>
        public bool ShowConfirmation(string message, string title = "Confirmación")
        {
            bool result = false;
            
            SafeInvoke(() =>
            {
                try
                {
                    result = MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error mostrando confirmación: {ex.Message}");
                }
            });
            
            return result;
        }
        
        /// <summary>
        /// Actualiza el título de la ventana
        /// </summary>
        public void UpdateWindowTitle(Form form, string title)
        {
            if (form == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    form.Text = title;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando título: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Actualiza el icono de la bandeja del sistema
        /// </summary>
        public void UpdateNotifyIcon(NotifyIcon notifyIcon, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (notifyIcon == null) return;
            
            SafeInvoke(() =>
            {
                try
                {
                    notifyIcon.ShowBalloonTip(3000, "SlskDown", text, icon);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Error actualizando NotifyIcon: {ex.Message}");
                }
            });
        }
    }
}
