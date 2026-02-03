using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public class TestForm : Form
    {
        public TestForm()
        {
            this.Text = "TEST - SlskDown v3.1";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.DarkBlue;
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            
            var label = new Label
            {
                Text = "SlskDown v3.1 - Funciona!",
                ForeColor = Color.White,
                Font = new Font("Arial", 16),
                AutoSize = true,
                Location = new Point(200, 200)
            };
            this.Controls.Add(label);
            
            var button = new Button
            {
                Text = "Cerrar",
                Location = new Point(350, 300),
                Size = new Size(100, 30),
                BackColor = Color.White
            };
            button.Click += (s, e) => this.Close();
            this.Controls.Add(button);
        }
    }
}

