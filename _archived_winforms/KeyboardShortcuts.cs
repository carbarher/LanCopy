using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class KeyboardShortcutManager
    {
        private Dictionary<Keys, Action> shortcuts = new Dictionary<Keys, Action>();
        private Form mainForm;
        private TabControl tabControl;
        private Action<string> logAction;
        
        public KeyboardShortcutManager(Form form, TabControl tabs, Action<string> logger)
        {
            mainForm = form;
            tabControl = tabs;
            logAction = logger;
        }
        
        public void RegisterShortcuts()
        {
            mainForm.KeyPreview = true;
            mainForm.KeyDown += HandleKeyDown;
            
            logAction?.Invoke("⌨️ Keyboard shortcuts registrados");
        }
        
        public void RegisterShortcut(Keys key, Action action)
        {
            shortcuts[key] = action;
        }
        
        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.KeyData;
            
            if (shortcuts.ContainsKey(key))
            {
                shortcuts[key]?.Invoke();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // Atajos numéricos Ctrl+1-9
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int tabIndex = e.KeyCode - Keys.D1;
                GoToTab(tabIndex);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // F1 - Mostrar ayuda
            if (e.KeyCode == Keys.F1)
            {
                ShowShortcutHelp();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // Ctrl+W - Cerrar pestaña actual
            if (e.Control && e.KeyCode == Keys.W)
            {
                CloseCurrentTab();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // Ctrl+Tab - Siguiente pestaña
            if (e.Control && e.KeyCode == Keys.Tab && !e.Shift)
            {
                NextTab();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // Ctrl+Shift+Tab - Pestaña anterior
            if (e.Control && e.Shift && e.KeyCode == Keys.Tab)
            {
                PreviousTab();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            
            // F5 - Refrescar
            if (e.KeyCode == Keys.F5)
            {
                Refresh();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        }
        
        private void GoToTab(int index)
        {
            if (tabControl != null && index >= 0 && index < tabControl.TabCount)
            {
                tabControl.SelectedIndex = index;
                logAction?.Invoke($"⌨️ Navegado a pestaña {index + 1}");
            }
        }
        
        private void CloseCurrentTab()
        {
            if (tabControl != null && tabControl.SelectedTab != null)
            {
                // Solo cerrar pestañas que no sean principales
                string tabName = tabControl.SelectedTab.Text;
                if (!tabName.Contains("Búsqueda") && !tabName.Contains("Descargas") && 
                    !tabName.Contains("Automático") && !tabName.Contains("Configuración"))
                {
                    tabControl.TabPages.Remove(tabControl.SelectedTab);
                    logAction?.Invoke($"⌨️ Pestaña '{tabName}' cerrada");
                }
            }
        }
        
        private void NextTab()
        {
            if (tabControl != null && tabControl.TabCount > 0)
            {
                int nextIndex = (tabControl.SelectedIndex + 1) % tabControl.TabCount;
                tabControl.SelectedIndex = nextIndex;
            }
        }
        
        private void PreviousTab()
        {
            if (tabControl != null && tabControl.TabCount > 0)
            {
                int prevIndex = tabControl.SelectedIndex - 1;
                if (prevIndex < 0) prevIndex = tabControl.TabCount - 1;
                tabControl.SelectedIndex = prevIndex;
            }
        }
        
        private void Refresh()
        {
            logAction?.Invoke("⌨️ Refrescando vista actual...");
            // El refresh específico dependerá de la pestaña activa
        }
        
        public void ShowShortcutHelp()
        {
            var form = new Form
            {
                Text = "Keyboard Shortcuts - SlskDown",
                Size = new Size(600, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            
            var rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };
            
            rtb.AppendText("═══════════════════════════════════════════════════════\n");
            rtb.AppendText("           KEYBOARD SHORTCUTS - SLSKDOWN\n");
            rtb.AppendText("═══════════════════════════════════════════════════════\n\n");
            
            rtb.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.LightBlue;
            rtb.AppendText("NAVEGACIÓN\n");
            rtb.SelectionFont = new Font("Consolas", 10);
            rtb.SelectionColor = Color.White;
            rtb.AppendText("  F1                    Mostrar esta ayuda\n");
            rtb.AppendText("  Ctrl+W                Cerrar pestaña actual\n");
            rtb.AppendText("  Ctrl+Shift+T          Reabrir pestaña cerrada\n");
            rtb.AppendText("  Ctrl+Tab              Siguiente pestaña\n");
            rtb.AppendText("  Ctrl+Shift+Tab        Pestaña anterior\n");
            rtb.AppendText("  Ctrl+1-9              Ir a pestaña específica\n");
            rtb.AppendText("  F5                    Refrescar vista actual\n\n");
            
            rtb.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.LightGreen;
            rtb.AppendText("BÚSQUEDA Y DESCARGAS\n");
            rtb.SelectionFont = new Font("Consolas", 10);
            rtb.SelectionColor = Color.White;
            rtb.AppendText("  Ctrl+F                Buscar en pestaña actual\n");
            rtb.AppendText("  Ctrl+N                Nueva búsqueda\n");
            rtb.AppendText("  Ctrl+D                Ir a descargas\n");
            rtb.AppendText("  Enter                 Iniciar búsqueda/descarga\n");
            rtb.AppendText("  Delete                Eliminar/Cancelar selección\n");
            rtb.AppendText("  Alt+Enter             Propiedades del archivo\n\n");
            
            rtb.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.Orange;
            rtb.AppendText("EDICIÓN\n");
            rtb.SelectionFont = new Font("Consolas", 10);
            rtb.SelectionColor = Color.White;
            rtb.AppendText("  Ctrl+C                Copiar selección\n");
            rtb.AppendText("  Ctrl+V                Pegar\n");
            rtb.AppendText("  Ctrl+A                Seleccionar todo\n");
            rtb.AppendText("  Ctrl+Z                Deshacer\n");
            rtb.AppendText("  Ctrl+Y                Rehacer\n\n");
            
            rtb.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.Yellow;
            rtb.AppendText("SISTEMA\n");
            rtb.SelectionFont = new Font("Consolas", 10);
            rtb.SelectionColor = Color.White;
            rtb.AppendText("  Ctrl+,                Abrir configuración\n");
            rtb.AppendText("  Ctrl+Q                Salir de la aplicación\n");
            rtb.AppendText("  Escape                Cancelar/Cerrar diálogo\n");
            rtb.AppendText("  Alt+F4                Cerrar aplicación\n\n");
            
            rtb.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.Cyan;
            rtb.AppendText("CARACTERÍSTICAS NICOTINE+\n");
            rtb.SelectionFont = new Font("Consolas", 10);
            rtb.SelectionColor = Color.White;
            rtb.AppendText("  Ctrl+R                Ir a chat rooms\n");
            rtb.AppendText("  Ctrl+L                Ir a logs\n");
            rtb.AppendText("  Ctrl+U                Ir a uploads\n\n");
            
            rtb.AppendText("═══════════════════════════════════════════════════════\n");
            rtb.SelectionFont = new Font("Consolas", 9, FontStyle.Italic);
            rtb.SelectionColor = Color.Gray;
            rtb.AppendText("\nPresiona ESC o cierra esta ventana para continuar.\n");
            
            form.Controls.Add(rtb);
            
            // Cerrar con ESC
            form.KeyPreview = true;
            form.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    form.Close();
                }
            };
            
            form.ShowDialog();
        }
    }
}
