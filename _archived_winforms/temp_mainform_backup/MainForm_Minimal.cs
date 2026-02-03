using System;
using System.Windows.Forms;

namespace SlskDown
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            this.Text = "SlskDown - Test Minimal";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            var label = new Label
            {
                Text = "Aplicación funcionando correctamente",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Arial", 16)
            };
            
            this.Controls.Add(label);
        }
    }
}
