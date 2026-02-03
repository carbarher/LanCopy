using System;
using System.IO;
using System.Windows.Forms;

namespace SlskDown
{
    static class Program
    {
        private static volatile bool isExiting;

        private static bool IsIgnorableWinFormsShutdownListViewException(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var message = ex.Message ?? string.Empty;
            var stack = ex.StackTrace ?? string.Empty;
            var asText = ex.ToString() ?? string.Empty;
            var combined = string.Concat(message, "\n", stack, "\n", asText);

            // Bug conocido de WinForms: ListView lanza ArgumentException/InvalidArgument al destruirse
            // con selección/índices desincronizados (suele ocurrir al cerrar la app, pero puede
            // aparecer en otros momentos si el handle se recrea).
            var looksLikeInvalidIndexArgument =
                (
                    (
                    combined.Contains("InvalidArgument", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("no es válido", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("no es valido", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("ArgumentException", StringComparison.OrdinalIgnoreCase)
                    ) &&
                combined.Contains("index", StringComparison.OrdinalIgnoreCase)
                );

            if (looksLikeInvalidIndexArgument &&
                (
                    combined.Contains("System.Windows.Forms.ListView", StringComparison.OrdinalIgnoreCase) ||
                    (ex.TargetSite?.DeclaringType?.FullName?.Contains("System.Windows.Forms.ListView", StringComparison.OrdinalIgnoreCase) ?? false)
                ))
            {
                return true;
            }

            var isIndexError =
                combined.Contains("InvalidArgument", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("no es válido", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("no es valido", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("ArgumentException", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("Actual value was", StringComparison.OrdinalIgnoreCase);

            var looksLikeListViewShutdownBug =
                combined.Contains("System.Windows.Forms.ListView", StringComparison.OrdinalIgnoreCase) &&
                (
                    combined.Contains("SelectedListViewItemCollection", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("ListViewItemCollection.get_Item", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("SelectedListViewItemCollection.CopyTo", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("OnHandleDestroyed", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("Control.WmDestroy", StringComparison.OrdinalIgnoreCase)
                );

            if (looksLikeListViewShutdownBug)
            {
                return true;
            }

            if (isIndexError &&
                combined.Contains("index", StringComparison.OrdinalIgnoreCase) &&
                combined.Contains("System.Windows.Forms.ListView", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ex.InnerException != null)
            {
                return IsIgnorableWinFormsShutdownListViewException(ex.InnerException);
            }

            return false;
        }

        [STAThread]
        static void Main()
        {
            var timestamp = DateTime.Now.ToString("HHmmss");
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"program_start_{timestamp}.txt");
            MainForm mainForm = null;
            
            try
            {
                System.IO.File.WriteAllText(logPath, $"=== PROGRAM.MAIN INICIADO === {DateTime.Now:HH:mm:ss.fff}\n");
                System.IO.File.AppendAllText(logPath, "Dentro del try principal\n");
                
                System.IO.File.AppendAllText(logPath, "EnableVisualStyles...\n");
                Application.EnableVisualStyles();
                
                System.IO.File.AppendAllText(logPath, "SetCompatibleTextRenderingDefault...\n");
                Application.SetCompatibleTextRenderingDefault(false);
                
                System.IO.File.AppendAllText(logPath, "Application configurado OK\n");
                
                System.IO.File.AppendAllText(logPath, "Configurando manejadores de excepción...\n");

                Application.ThreadException += (s, e) =>
                {
                    if (isExiting)
                    {
                        return;
                    }

                    if (IsIgnorableWinFormsShutdownListViewException(e.Exception))
                    {
                        return;
                    }

                    var errorMsg = $"Error de UI:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
                    
                    // Agregar información de la excepción interna si existe
                    if (e.Exception.InnerException != null)
                    {
                        errorMsg += $"\n\nInner Exception:\n{e.Exception.InnerException.Message}\n\nInner Stack Trace:\n{e.Exception.InnerException.StackTrace}";
                    }
                    
                    Console.WriteLine(errorMsg);
                    
                    // Guardar en archivo
                    try
                    {
                        System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt"), errorMsg);
                    }
                    catch (Exception writeEx)
                    {
                        Console.WriteLine($"Failed to write error log: {writeEx.Message}");
                    }
                    
                    MessageBox.Show(errorMsg, "Error de UI - SlskDown", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    if (ex != null)
                    {
                        if (isExiting)
                        {
                            return;
                        }

                        if (IsIgnorableWinFormsShutdownListViewException(ex))
                        {
                            return;
                        }

                        var errorMsg = $"Error no controlado:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                        Console.WriteLine(errorMsg);
                        
                        // Guardar en archivo
                        try
                        {
                            System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt"), errorMsg);
                        }
                        catch (Exception writeEx)
                        {
                            Console.WriteLine($"Failed to write error log: {writeEx.Message}");
                        }
                        
                        MessageBox.Show(errorMsg, "Error Fatal - SlskDown", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                
                System.IO.File.AppendAllText(logPath, "Creando MainForm...\n");
                try
                {
                    mainForm = new MainForm();
                    System.IO.File.AppendAllText(logPath, "MainForm creado OK\n");
                    System.IO.File.AppendAllText(logPath, $"MainForm handle: {mainForm.Handle}\n");
                    System.IO.File.AppendAllText(logPath, $"MainForm visible: {mainForm.Visible}\n");

                    Application.ApplicationExit += (_, __) => { isExiting = true; };
                    mainForm.FormClosing += (_, __) => { isExiting = true; };
                }
                catch (Exception createEx)
                {
                    var errorMsg = $"Error al crear MainForm:\n\n{createEx.Message}\n\n{createEx.StackTrace}";
                    System.IO.File.AppendAllText(logPath, $"ERROR: {errorMsg}\n");
                    System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mainform_error.txt"), errorMsg);
                    MessageBox.Show(errorMsg, "Error Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Agregar handlers para detectar cierre prematuro
                mainForm.Load += (s, e) => System.IO.File.AppendAllText(logPath, "🎯 MainForm.Load DISPARADO\n");
                mainForm.Shown += (s, e) => System.IO.File.AppendAllText(logPath, "🎯 MainForm.Shown DISPARADO\n");
                mainForm.Activated += (s, e) => System.IO.File.AppendAllText(logPath, "🎯 MainForm.Activated DISPARADO\n");
                mainForm.FormClosing += (s, e) => System.IO.File.AppendAllText(logPath, $"🚪 MainForm.FormClosing DISPARADO: Reason={e.CloseReason}\n");
                mainForm.FormClosed += (s, e) => System.IO.File.AppendAllText(logPath, $"🚪 MainForm.FormClosed DISPARADO: Reason={e.CloseReason}\n");
                
                System.IO.File.AppendAllText(logPath, "Forzando creación del handle...\n");
                try
                {
                    var handle = mainForm.Handle; // Forzar creación del handle
                    System.IO.File.AppendAllText(logPath, $"Handle creado: {handle}\n");
                }
                catch (Exception handleEx)
                {
                    var errorMsg = $"ERROR AL CREAR HANDLE:\n{handleEx.Message}\n\nStack Trace:\n{handleEx.StackTrace}";
                    System.IO.File.AppendAllText(logPath, $"ERROR: {errorMsg}\n");
                    System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle_error.txt"), errorMsg);
                    MessageBox.Show(errorMsg, "Error al crear Handle", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw;
                }
                
                System.IO.File.AppendAllText(logPath, "Iniciando Application.Run...\n");
                System.IO.File.AppendAllText(logPath, $"MainForm.IsDisposed antes de Run: {mainForm.IsDisposed}\n");
                System.IO.File.AppendAllText(logPath, $"MainForm.IsHandleCreated antes de Run: {mainForm.IsHandleCreated}\n");
                System.IO.File.AppendAllText(logPath, $"MainForm.Visible antes de Run: {mainForm.Visible}\n");
                
                try
                {
                    Application.Run(mainForm);
                    System.IO.File.AppendAllText(logPath, "Application.Run finalizado normalmente\n");
                }
                catch (Exception runEx)
                {
                    var errorMsg = $"EXCEPCIÓN EN Application.Run:\n{runEx.Message}\n\nStack Trace:\n{runEx.StackTrace}";
                    System.IO.File.AppendAllText(logPath, $"ERROR: {errorMsg}\n");
                    System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_run_error.txt"), errorMsg);
                    MessageBox.Show(errorMsg, "Error en Application.Run", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw;
                }
                
                System.IO.File.AppendAllText(logPath, $"MainForm.IsDisposed después de Run: {mainForm?.IsDisposed}\n");
            }
            catch (Exception ex)
            {
                var errorMsg = $"ERROR FATAL EN MAIN:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                Console.WriteLine(errorMsg);
                System.IO.File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fatal_error.txt"), errorMsg);
                MessageBox.Show(errorMsg, "Error Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
    }
}
