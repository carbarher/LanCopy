using System;
using System.Windows.Forms;
using System.IO;

class TestMinimo
{
    [STAThread]
    static void Main_DISABLED()
    {
        try
        {
            File.WriteAllText("test_log.txt", "Test iniciado\n");
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var form = new Form();
            form.Text = "Test OK";
            form.Size = new System.Drawing.Size(400, 300);
            form.BackColor = System.Drawing.Color.DarkGreen;
            
            var label = new Label();
            label.Text = "✅ .NET 8 funciona correctamente";
            label.Dock = DockStyle.Fill;
            label.ForeColor = System.Drawing.Color.White;
            label.Font = new System.Drawing.Font("Arial", 16);
            label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            
            form.Controls.Add(label);
            
            File.AppendAllText("test_log.txt", "Form creado, mostrando...\n");
            
            Application.Run(form);
            
            File.AppendAllText("test_log.txt", "Aplicación cerrada\n");
        }
        catch (Exception ex)
        {
            File.WriteAllText("test_error.txt", ex.ToString());
            MessageBox.Show(ex.ToString(), "Error");
        }
    }
}
