using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public class MainFormSimpleTabs : Form
    {
        private TabControl tabControl;
        
        public MainFormSimpleTabs()
        {
            Console.WriteLine("[MainFormSimpleTabs] ðŸ—ï¸ INICIADO");
            
            // Configurar formulario
            this.Size = new Size(1100, 700);
            this.Text = "SlskDown - TEST DE PESTAÃ‘AS";
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Crear TabControl
            Console.WriteLine("[MainFormSimpleTabs] ðŸ“‘ Creando TabControl...");
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // PestaÃ±a 1: Buscar
            var searchTab = new TabPage("ðŸ” Buscar")
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };
            searchTab.Controls.Add(new Label 
            { 
                Text = "PESTAÃ‘A DE BÃšSQUEDA - FUNCIONA", 
                ForeColor = Color.White,
                Font = new Font("Arial", 20),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            
            // PestaÃ±a 2: Descargas
            var downloadsTab = new TabPage("ðŸ“¥ Descargas")
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };
            downloadsTab.Controls.Add(new Label 
            { 
                Text = "PESTAÃ‘A DE DESCARGAS - FUNCIONA", 
                ForeColor = Color.White,
                Font = new Font("Arial", 20),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            
            // PestaÃ±a 3: Auto-BÃºsqueda
            var authorTab = new TabPage("ðŸ“š Auto-BÃºsqueda")
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };
            authorTab.Controls.Add(new Label 
            { 
                Text = "PESTAÃ‘A DE BÃšSQUEDA POR AUTOR - FUNCIONA", 
                ForeColor = Color.White,
                Font = new Font("Arial", 20),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            
            // Agregar pestaÃ±as
            Console.WriteLine("[MainFormSimpleTabs] ðŸ“‹ Agregando pestaÃ±as...");
            tabControl.TabPages.Add(searchTab);
            tabControl.TabPages.Add(downloadsTab);
            tabControl.TabPages.Add(authorTab);
            
            // Agregar TabControl al formulario
            Console.WriteLine("[MainFormSimpleTabs] ðŸ–¼ï¸ Agregando TabControl al formulario...");
            this.Controls.Add(tabControl);
            
            Console.WriteLine("[MainFormSimpleTabs] âœ… COMPLETADO");
        }
    }
}

