using System;
using System.Windows.Forms;

namespace SlskDown
{
    public partial class TestMinimal : Form
    {
        public TestMinimal()
        {
            MessageBox.Show("âœ… TestMinimal Constructor INICIADO", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            this.Text = "Test Minimal";
            this.Size = new System.Drawing.Size(400, 300);
            this.BackColor = System.Drawing.Color.DarkBlue;
            
            var button = new Button
            {
                Text = "ðŸ§ª BotÃ³n de Prueba",
                Location = new System.Drawing.Point(50, 50),
                Size = new System.Drawing.Size(200, 50),
                BackColor = System.Drawing.Color.LightGreen
            };
            
            button.Click += (s, e) => {
                MessageBox.Show("âœ… BotÃ³n presionado", "TEST", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            this.Controls.Add(button);
        }
    }
}

