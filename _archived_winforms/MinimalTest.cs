using System;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    static class MinimalTest
    {
        [STAThread]
        static void Main()
        {
            // Log inmediato
            System.IO.File.WriteAllText("minimal_test.txt", $"INICIO: {DateTime.Now}\n");
            
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                System.IO.File.AppendAllText("minimal_test.txt", "Application configurado\n");
                
                var form = new Form
                {
                    Text = "MINIMAL TEST",
                    Size = new Size(400, 300),
                    BackColor = Color.Red,
                    StartPosition = FormStartPosition.CenterScreen
                };
                
                System.IO.File.AppendAllText("minimal_test.txt", "Form creado\n");
                
                Application.Run(form);
                
                System.IO.File.AppendAllText("minimal_test.txt", "Application.Run terminado\n");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("minimal_error.txt", ex.ToString());
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
