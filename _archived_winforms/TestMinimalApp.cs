using System;
using System.Windows.Forms;

// Test minimal para verificar que WinForms funciona
public class TestMinimalApp
{
    [STAThread]
    public static void MainTest()
    {
        MessageBox.Show("¡WinForms funciona!", "Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
