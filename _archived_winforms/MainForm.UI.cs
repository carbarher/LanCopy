using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial Class para UI y controles
    /// Contiene: creación de tabs, controles, temas, eventos UI
    /// </summary>
    public partial class MainForm : Form
    {
        #region UI Controls - Búsqueda
        
        private ComboBox cmbSearch;
        private Button btnSearch;
        private Button btnCancelSearch;
        private ListView lvResults;
        private Label lblResultCount;
        private TextBox txtMinFileSize;
        private CheckBox chkFilterSpanish;
        private CheckBox chkAutoDownload;
        
        #endregion
        
        #region UI Controls - Descargas
        
        private ListView lvDownloads;
        private Button btnPauseResume;
        private Button btnClearCompleted;
        private Button btnRetryFailed;
        private Button btnOpenDownloadFolder;
        private ProgressBar progressBar;
        private Label lblProgressInfo;
        private Label lblDownloadSpeed;
        
        #endregion
        
        #region UI Controls - Autores
        
        private ListView lvAutoAuthors;
        private TextBox txtAutoLog;
        private Label lblPurgeProgress;
        private Button btnStartAuto;
        private Button btnStopAuto;
        private Button btnLoadAuthors;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Label lblAuthorCount;
        
        #endregion
        
        #region UI Controls - Configuración
        
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtDownloadPath;
        private NumericUpDown numMaxDownloads;
        private NumericUpDown numSearchTimeout;
        private CheckBox chkDarkMode;
        private CheckBox chkAutoReconnect;
        private Button btnSaveConfig;
        
        #endregion
        
        #region UI Controls - Dashboard
        
        private Panel pnlDashboard;
        private Label lblConnectionStatus;
        private Label lblHealthStatus;
        private Label lblTotalSearches;
        private Label lblTotalDownloads;
        private Label lblSuccessRate;
        private Label lblAverageSpeed;
        private ProgressBar pbCpuUsage;
        private ProgressBar pbMemoryUsage;
        
        #endregion
        
        #region Theme Management
        
        /// <summary>
        /// Aplica tema oscuro a todos los controles
        /// </summary>
        private void ApplyDarkTheme()
        {
            var darkBg = Color.FromArgb(18, 18, 18);
            var darkControl = Color.FromArgb(30, 30, 30);
            var darkBorder = Color.FromArgb(45, 45, 45);
            var lightText = Color.White;
            var accentBlue = Color.FromArgb(100, 200, 255);
            
            // Form principal
            this.BackColor = darkBg;
            this.ForeColor = lightText;
            
            // Aplicar a todos los controles recursivamente
            ApplyDarkThemeRecursive(this.Controls, darkBg, darkControl, lightText);
        }
        
        private void ApplyDarkThemeRecursive(Control.ControlCollection controls, Color bg, Color controlBg, Color fg)
        {
            foreach (Control control in controls)
            {
                if (control is TextBox || control is ComboBox || control is NumericUpDown)
                {
                    control.BackColor = controlBg;
                    control.ForeColor = fg;
                }
                else if (control is ListView lv)
                {
                    lv.BackColor = controlBg;
                    lv.ForeColor = fg;
                }
                else if (control is Button btn)
                {
                    btn.BackColor = Color.FromArgb(45, 45, 45);
                    btn.ForeColor = fg;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
                }
                else if (control is Panel || control is GroupBox)
                {
                    control.BackColor = bg;
                    control.ForeColor = fg;
                }
                
                // Recursivo para controles anidados
                if (control.HasChildren)
                {
                    ApplyDarkThemeRecursive(control.Controls, bg, controlBg, fg);
                }
            }
        }
        
        #endregion
        
        #region ListView Custom Drawing
        
        private void ListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 45)), e.Bounds);
            e.Graphics.DrawString(e.Header.Text, e.Font, Brushes.White, e.Bounds.X + 5, e.Bounds.Y + 5);
        }
        
        private void ListView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
            {
                e.DrawDefault = true;
                return;
            }
            
            // Determinar color de fondo
            Color bgColor;
            if (e.Item.Selected)
            {
                bgColor = listView.Focused ? Color.FromArgb(60, 120, 180) : Color.FromArgb(50, 50, 50);
            }
            else if ((e.State & ListViewItemStates.Hot) != 0)
            {
                // Hover effect
                bgColor = Color.FromArgb(40, 40, 45);
            }
            else
            {
                bgColor = e.ItemIndex % 2 == 0 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(35, 35, 35);
            }
            
            // Dibujar fondo completo del item
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            
            // Dibujar borde de selección si está seleccionado
            if (e.Item.Selected && listView.Focused)
            {
                e.DrawFocusRectangle();
            }
        }
        
        private void ListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
            {
                e.DrawDefault = true;
                return;
            }
            
            // Determinar color de fondo (mismo que DrawItem)
            Color bgColor;
            if (e.Item.Selected)
            {
                bgColor = listView.Focused ? Color.FromArgb(60, 120, 180) : Color.FromArgb(50, 50, 50);
            }
            else if ((e.ItemState & ListViewItemStates.Hot) != 0)
            {
                // Hover effect
                bgColor = Color.FromArgb(40, 40, 45);
            }
            else
            {
                bgColor = e.ItemIndex % 2 == 0 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(35, 35, 35);
            }
            
            // Dibujar fondo del subitem
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, e.SubItem.Bounds);
            }
            
            // Determinar color de texto
            var textColor = e.Item.ForeColor != Color.Empty ? e.Item.ForeColor : Color.White;
            
            // Dibujar texto con padding
            var textBounds = new Rectangle(
                e.SubItem.Bounds.X + 4,
                e.SubItem.Bounds.Y,
                e.SubItem.Bounds.Width - 8,
                e.SubItem.Bounds.Height
            );
            
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                e.Item.Font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
            );
        }
        
        #endregion
        
        #region UI Helpers
        
        /// <summary>
        /// Actualiza un control de forma thread-safe
        /// </summary>
        private void SafeUpdateControl(Control control, Action<Control> updateAction)
        {
            if (control == null || !control.IsHandleCreated)
                return;
                
            if (control.InvokeRequired)
            {
                control.BeginInvoke(new Action(() => SafeUpdateControl(control, updateAction)));
                return;
            }
            
            try
            {
                updateAction(control);
            }
            catch (ObjectDisposedException)
            {
                // Control ya fue disposed
            }
            catch (InvalidOperationException)
            {
                // Handle no creado
            }
        }
        
        /// <summary>
        /// Muestra un mensaje de forma thread-safe
        /// </summary>
        private void SafeShowMessage(string message, string title, MessageBoxIcon icon)
        {
            SafeInvoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
            });
        }
        
        /// <summary>
        /// Actualiza la barra de progreso de forma thread-safe
        /// </summary>
        private void SafeUpdateProgress(ProgressBar progressBar, int value, int maximum = 100)
        {
            SafeUpdateControl(progressBar, pb =>
            {
                pb.Maximum = maximum;
                pb.Value = Math.Min(Math.Max(0, value), maximum);
            });
        }
        
        /// <summary>
        /// Actualiza un label de forma thread-safe
        /// </summary>
        private void SafeUpdateLabel(Label label, string text)
        {
            SafeUpdateControl(label, lbl => lbl.Text = text);
        }
        
        #endregion
        
        #region Event Handlers - UI
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Manejar minimización a bandeja del sistema si está configurado
            if (this.WindowState == FormWindowState.Minimized && chkMinimizeToTray?.Checked == true)
            {
                this.Hide();
                notifyIcon?.ShowBalloonTip(2000, "SlskDown", "Minimizado a bandeja del sistema", ToolTipIcon.Info);
            }
        }
        
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Confirmación antes de cerrar si hay descargas activas
            if (downloadQueue?.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Hay {downloadQueue.Count} descargas en cola.\n\n¿Deseas cerrar la aplicación?",
                    "Confirmar cierre",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }
        
        #endregion
    }
}
