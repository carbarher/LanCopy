using System;
using System.Windows.Forms;

class TestApp
{
    [STAThread]
    static void Main_DISABLED()
    {
        try
        {
            MessageBox.Show("Test OK - .NET funciona correctamente", "Test", MessageBoxButtons.OK);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
