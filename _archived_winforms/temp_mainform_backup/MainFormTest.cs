using System;
using System.Windows.Forms;

namespace SlskDown
{
    public class MainFormTest : Form
    {
        public MainFormTest()
        {
            // Escribir INMEDIATAMENTE
            try
            {
                System.IO.File.WriteAllText(@"c:\p2p\mainformtest_debug.txt", 
                    $"[{DateTime.Now:HH:mm:ss.fff}] MainFormTest CONSTRUCTOR INICIADO\n");
            }
            catch { }
            
            Console.WriteLine("MainFormTest - Constructor iniciado");
            
            this.Text = "SlskDown - Test";
            this.Size = new System.Drawing.Size(800, 600);
            
            var label = new Label
            {
                Text = "MainForm Test - Si ves esto, el problema estÃ¡ en los campos de MainForm",
                Location = new System.Drawing.Point(50, 50),
                Size = new System.Drawing.Size(700, 100),
                Font = new System.Drawing.Font("Arial", 14)
            };
            
            this.Controls.Add(label);
            
            Console.WriteLine("MainFormTest - Constructor completado");
            
            try
            {
                System.IO.File.AppendAllText(@"c:\p2p\mainformtest_debug.txt", 
                    $"[{DateTime.Now:HH:mm:ss.fff}] MainFormTest CONSTRUCTOR COMPLETADO\n");
            }
            catch { }
        }
    }
}

