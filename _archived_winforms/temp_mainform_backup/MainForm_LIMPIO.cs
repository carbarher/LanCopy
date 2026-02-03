using System;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class MainForm : Form
    {
        public MainForm()
        {
            // LOG INMEDIATO
            try
            {
                System.IO.File.WriteAllText(@"c:\p2p\slskdown_debug.txt", 
                    $"[{DateTime.Now:HH:mm:ss.fff}] âœ… CONSTRUCTOR INICIADO\n");
            }
            catch { }
            
            Console.WriteLine("âœ… MainForm - Constructor iniciado");
            
            // ConfiguraciÃ³n bÃ¡sica de la ventana
            this.Text = "SlskDown - Funcionando";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            
            // Label de Ã©xito
            var successLabel = new Label
            {
                Text = "ðŸŽ‰ Â¡APLICACIÃ“N FUNCIONANDO!\n\n" +
                       "El problema de congelamiento estÃ¡ resuelto.\n\n" +
                       "Ahora podemos restaurar las funcionalidades paso a paso.",
                Location = new Point(50, 50),
                Size = new Size(900, 200),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.LimeGreen,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            this.Controls.Add(successLabel);
            
            Console.WriteLine("âœ… MainForm - Constructor completado");
            
            try
            {
                System.IO.File.AppendAllText(@"c:\p2p\slskdown_debug.txt", 
                    $"[{DateTime.Now:HH:mm:ss.fff}] âœ… CONSTRUCTOR COMPLETADO\n");
            }
            catch { }
        }
    }
}

