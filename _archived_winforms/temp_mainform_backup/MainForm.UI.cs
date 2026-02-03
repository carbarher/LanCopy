using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial class para creación de UI
    /// </summary>
    public partial class MainForm
    {
        // Esta partial class contendrá todos los métodos CreateXXXTab
        // Los métodos se moverán aquí gradualmente para mejor organización
        
        /// <summary>
        /// Inicializa los componentes de UI base
        /// </summary>
        private void InitializeUIComponents()
        {
            // Configuración base del formulario
            this.Text = "SlskDown - Cliente Soulseek";
            this.Size = new Size(1100, 700);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
        }
        
        /// <summary>
        /// Crea el TabControl principal
        /// </summary>
        private TabControl CreateMainTabControl()
        {
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                ItemSize = new Size(120, 40),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.FlatButtons
            };
            
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += TabControl_DrawItem;
            
            return tabControl;
        }
        
        // TabControl_DrawItem movido a MainForm.cs principal
        
        /// <summary>
        /// Crea un panel estándar para tabs
        /// </summary>
        private Panel CreateStandardPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.FromArgb(18, 18, 18)
            };
        }
        
        /// <summary>
        /// Crea un botón estándar con estilo
        /// </summary>
        private Button CreateStyledButton(string text, int width = 120, int height = 35)
        {
            return new Button
            {
                Text = text,
                Size = new Size(width, height),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }
        
        /// <summary>
        /// Crea un label estándar
        /// </summary>
        private Label CreateStyledLabel(string text, int fontSize = 10)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", fontSize),
                AutoSize = true
            };
        }
        
        /// <summary>
        /// Crea un TextBox estándar
        /// </summary>
        private TextBox CreateStyledTextBox(int width = 200, int height = 25)
        {
            return new TextBox
            {
                Size = new Size(width, height),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
        }
    }
}
