using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            this.Text = "SlskDown - TEST SIMPLE";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 18);
            
            var label = new Label
            {
                Text = "APLICACIÓN FUNCIONANDO",
                ForeColor = Color.White,
                BackColor = Color.Red,
                Font = new Font("Arial", 20, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(200, 100)
            };
            
            var button = new Button
            {
                Text = "BOTÓN DE PRUEBA",
                ForeColor = Color.White,
                BackColor = Color.Blue,
                Font = new Font("Arial", 14, FontStyle.Bold),
                Size = new Size(200, 50),
                Location = new Point(200, 200)
            };
            button.Click += (s, e) => MessageBox.Show("¡Funciona!");
            
            this.Controls.Add(label);
            this.Controls.Add(button);
        }
    }
}
