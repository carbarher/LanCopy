using System;
using System.Windows.Forms;

class TestMinimal2
{
    [STAThread]
    static void Main_DISABLED()
    {
        try
        {
            System.IO.File.WriteAllText("test_minimal.txt", "Test iniciado\n");
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            System.IO.File.AppendAllText("test_minimal.txt", "Application configurado\n");
            
            var form = new Form
            {
                Text = "Test Minimal",
                Width = 400,
                Height = 300,
                StartPosition = FormStartPosition.CenterScreen
            };
            
            var label = new Label
            {
                Text = "Si ves esto, WinForms funciona!",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Arial", 16)
            };
            
            form.Controls.Add(label);
            
            System.IO.File.AppendAllText("test_minimal.txt", "Form creado, iniciando Application.Run\n");
            
            Application.Run(form);
            
            System.IO.File.AppendAllText("test_minimal.txt", "Application.Run finalizado\n");
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("test_minimal_error.txt", ex.ToString());
            MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
