using System;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para operaciones de UI thread-safe
    /// </summary>
    public static class UIHelpers
    {
        /// <summary>
        /// Ejecuta una acción en el UI thread de forma segura (no bloqueante)
        /// </summary>
        public static void SafeBeginInvoke(Control control, Action action)
        {
            if (control == null || action == null)
                return;

            try
            {
                if (control.InvokeRequired)
                {
                    control.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // Control ya fue disposed, ignorar
            }
            catch (InvalidOperationException)
            {
                // Handle ya no es válido, ignorar
            }
        }

        /// <summary>
        /// Ejecuta una acción en el UI thread de forma segura (bloqueante)
        /// ADVERTENCIA: Usar solo cuando sea absolutamente necesario esperar el resultado
        /// </summary>
        public static void SafeInvoke(Control control, Action action)
        {
            if (control == null || action == null)
                return;

            try
            {
                if (control.InvokeRequired)
                {
                    control.Invoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // Control ya fue disposed, ignorar
            }
            catch (InvalidOperationException)
            {
                // Handle ya no es válido, ignorar
            }
        }

        /// <summary>
        /// Actualiza un ListViewItem de forma thread-safe
        /// </summary>
        public static void UpdateListViewItem(ListView listView, ListViewItem item, int subItemIndex, string text, Color? foreColor = null)
        {
            SafeBeginInvoke(listView, () =>
            {
                if (item != null && subItemIndex < item.SubItems.Count)
                {
                    item.SubItems[subItemIndex].Text = text;
                    if (foreColor.HasValue)
                    {
                        item.ForeColor = foreColor.Value;
                    }
                }
            });
        }

        /// <summary>
        /// Agrega un item a un ListView de forma thread-safe
        /// </summary>
        public static void AddListViewItem(ListView listView, ListViewItem item)
        {
            SafeBeginInvoke(listView, () =>
            {
                listView.Items.Add(item);
            });
        }

        /// <summary>
        /// Remueve un item de un ListView de forma thread-safe
        /// </summary>
        public static void RemoveListViewItem(ListView listView, ListViewItem item)
        {
            SafeBeginInvoke(listView, () =>
            {
                if (listView.Items.Contains(item))
                {
                    listView.Items.Remove(item);
                }
            });
        }

        /// <summary>
        /// Limpia un ListView de forma thread-safe
        /// </summary>
        public static void ClearListView(ListView listView)
        {
            SafeBeginInvoke(listView, () =>
            {
                listView.Items.Clear();
            });
        }

        /// <summary>
        /// Actualiza el texto de un Label de forma thread-safe
        /// </summary>
        public static void UpdateLabel(Label label, string text)
        {
            SafeBeginInvoke(label, () =>
            {
                label.Text = text;
            });
        }

        /// <summary>
        /// Actualiza una ProgressBar de forma thread-safe
        /// </summary>
        public static void UpdateProgressBar(ProgressBar progressBar, int value, int maximum = 100)
        {
            SafeBeginInvoke(progressBar, () =>
            {
                progressBar.Maximum = maximum;
                progressBar.Value = Math.Min(Math.Max(0, value), maximum);
            });
        }

        /// <summary>
        /// Muestra un MessageBox de forma thread-safe
        /// </summary>
        public static DialogResult ShowMessageBox(Control owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            DialogResult result = DialogResult.None;
            
            SafeInvoke(owner, () =>
            {
                result = MessageBox.Show(text, caption, buttons, icon);
            });
            
            return result;
        }

        /// <summary>
        /// Aplica tema oscuro a un control y sus hijos
        /// </summary>
        public static void ApplyDarkTheme(Control control)
        {
            var darkBackground = Color.FromArgb(30, 30, 30);
            var darkForeground = Color.FromArgb(220, 220, 220);
            var darkControlBackground = Color.FromArgb(45, 45, 45);

            control.BackColor = darkBackground;
            control.ForeColor = darkForeground;

            if (control is TextBox || control is ComboBox || control is NumericUpDown)
            {
                control.BackColor = darkControlBackground;
            }

            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
                button.BackColor = darkControlBackground;
            }

            if (control is ListView listView)
            {
                listView.BackColor = darkControlBackground;
                listView.ForeColor = darkForeground;
            }

            // Aplicar recursivamente a controles hijos
            foreach (Control child in control.Controls)
            {
                ApplyDarkTheme(child);
            }
        }

        /// <summary>
        /// Crea un ListViewItem con estilo oscuro
        /// </summary>
        public static ListViewItem CreateDarkListViewItem(string text, params string[] subItems)
        {
            var item = new ListViewItem(text);
            item.BackColor = Color.FromArgb(45, 45, 45);
            item.ForeColor = Color.White;

            foreach (var subItem in subItems)
            {
                item.SubItems.Add(subItem);
            }

            return item;
        }
    }
}
