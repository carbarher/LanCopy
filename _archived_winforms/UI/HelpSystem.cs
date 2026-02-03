using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    public class HelpSystem
    {
        private static Dictionary<string, HelpContent> helpTopics = new Dictionary<string, HelpContent>();
        
        static HelpSystem()
        {
            InitializeHelpTopics();
        }
        
        private static void InitializeHelpTopics()
        {
            // Búsquedas
            helpTopics["search"] = new HelpContent
            {
                Title = "Sistema de Búsquedas",
                Content = @"**Búsquedas Avanzadas**

• **Operadores de búsqueda:**
  - Usa comillas para frases exactas: ""pink floyd""
  - Usa - para excluir: rock -metal
  - Usa ext: para extensión: ext:flac
  - Usa size: para tamaño: size:>100MB

• **Filtros guardados:**
  - Guarda búsquedas complejas con Ctrl+S
  - Aplica filtros desde la paleta de comandos (Ctrl+Shift+P)

• **Historial:**
  - Autocompletado inteligente mientras escribes
  - Ver historial completo con Ctrl+H

• **Caché:**
  - Las búsquedas se cachean por 5 minutos
  - Reduce tiempo de respuesta en 90%",
                Shortcuts = new[]
                {
                    "Ctrl+F - Nueva búsqueda",
                    "Ctrl+T - Nueva pestaña",
                    "Ctrl+W - Cerrar pestaña",
                    "Ctrl+S - Guardar filtro",
                    "Ctrl+H - Ver historial"
                }
            };
            
            // Descargas
            helpTopics["downloads"] = new HelpContent
            {
                Title = "Sistema de Descargas",
                Content = @"**Gestión de Descargas**

• **Prioridades:**
  - Critical: Descarga inmediata
  - High: Alta prioridad
  - Normal: Prioridad estándar
  - Low: Baja prioridad
  - Paused: Pausado

• **Retry Inteligente:**
  - Backoff exponencial: 1, 2, 5, 10, 30, 60 minutos
  - Máximo 6 intentos automáticos
  - Fuentes alternativas automáticas

• **Verificación:**
  - Checksums MD5 automáticos
  - Reintento si falla verificación

• **Balanceo:**
  - Máximo 2 descargas por usuario
  - Distribución equitativa automática",
                Shortcuts = new[]
                {
                    "Ctrl+D - Ver descargas",
                    "Delete - Eliminar de cola",
                    "Ctrl+P - Cambiar prioridad",
                    "Ctrl+R - Reintentar"
                }
            };
            
            // Estadísticas
            helpTopics["stats"] = new HelpContent
            {
                Title = "📊 Dashboard de Estadísticas",
                Content = @"**Visualización de Datos**

• **Heatmap de Actividad:**
  - Muestra actividad por hora y día
  - Identifica patrones de uso

• **Top 10 Usuarios:**
  - Usuarios más frecuentes
  - Tasa de éxito por usuario
  - Velocidad promedio

• **Top 10 Tipos de Archivo:**
  - Extensiones más descargadas
  - Tamaño total por tipo

• **Salud de Red:**
  - Packet loss en tiempo real
  - Latencia promedio
  - Estado: Excellent/Good/Fair/Poor

• **Métricas p50/p95/p99:**
  - Percentiles de rendimiento
  - Optimización basada en datos",
                Shortcuts = new[]
                {
                    "Ctrl+Shift+S - Abrir dashboard",
                    "F5 - Actualizar datos"
                }
            };
            
            // Configuración
            helpTopics["config"] = new HelpContent
            {
                Title = "Configuración Avanzada",
                Content = @"**Panel de Configuración**

• **Red y Protocolo:**
  - Timeouts granulares por operación
  - Rate limiting (evita bans)
  - Monitor de salud de red
  - Logger de protocolo (debug)

• **Búsquedas:**
  - Caché con TTL configurable
  - Historial y autocompletado
  - Filtros guardados
  - Virtual scrolling

• **Descargas:**
  - Sistema de prioridades
  - Retry inteligente
  - Verificación de integridad
  - Balanceo de carga

• **Seguridad:**
  - Modo privado/invisible
  - Bloqueo de IPs
  - Cifrado RSA de mensajes
  - Filtros anti-spam",
                Shortcuts = new[]
                {
                    "Ctrl+, - Abrir configuración"
                }
            };
            
            // Plugins y Temas
            helpTopics["extensions"] = new HelpContent
            {
                Title = "🔌 Plugins y Temas",
                Content = @"**Extensibilidad**

• **Sistema de Plugins:**
  - Carga dinámica de DLLs
  - Event Bus para comunicación
  - API completa disponible
  - Ejemplo: AutoResponder incluido

• **Temas:**
  - JSON personalizable
  - Dark Modern, Light, High Contrast
  - Crear temas personalizados
  - Aplicación en caliente

• **Atajos de Teclado:**
  - 50+ atajos predefinidos
  - Personalización completa
  - Comandos rápidos (Ctrl+Shift+P)",
                Shortcuts = new[]
                {
                    "Ctrl+Shift+P - Paleta de comandos",
                    "F1 - Ayuda contextual"
                }
            };
            
            // Backup
            helpTopics["backup"] = new HelpContent
            {
                Title = "Sistema de Backup",
                Content = @"**Backup Automático**

• **Características:**
  - Backup automático de configuración
  - Máximo 10 versiones guardadas
  - Timestamp en cada backup
  - Limpieza automática de antiguos

• **Restauración:**
  - Lista de backups disponibles
  - Backup pre-restauración automático
  - Recuperación completa

• **Ubicación:**
  - Carpeta: data/backups/
  - Formato: archivo.json.YYYYMMDD_HHMMSS.bak",
                Shortcuts = new[]
                {
                    "Crear backup desde paleta de comandos"
                }
            };
            
            // Atajos
            helpTopics["shortcuts"] = new HelpContent
            {
                Title = "Atajos de Teclado",
                Content = @"**Atajos Principales**

**General:**
• Ctrl+Shift+P - Paleta de comandos
• F1 - Ayuda contextual
• Ctrl+, - Configuración
• F5 - Actualizar

**Búsquedas:**
• Ctrl+F - Nueva búsqueda
• Ctrl+T - Nueva pestaña
• Ctrl+W - Cerrar pestaña
• Ctrl+S - Guardar filtro
• Ctrl+H - Historial

**Descargas:**
• Ctrl+D - Ver descargas
• Ctrl+P - Cambiar prioridad
• Ctrl+R - Reintentar
• Delete - Eliminar

**Estadísticas:**
• Ctrl+Shift+S - Dashboard

**UI:**
• Ctrl+Shift+L - Guardar layout
• Ctrl+1 a Ctrl+9 - Cambiar pestaña",
                Shortcuts = new string[0]
            };
        }
        
        public static void ShowHelp(string topic, Form parent = null)
        {
            if (!helpTopics.ContainsKey(topic))
                topic = "search"; // Por defecto
            
            var helpForm = new HelpForm(helpTopics[topic]);
            if (parent != null)
                helpForm.ShowDialog(parent);
            else
                helpForm.Show();
        }
        
        public static void ShowContextualHelp(Control activeControl, Form parent = null)
        {
            string topic = "search";
            
            // Determinar tema según el control activo
            if (activeControl != null)
            {
                var name = activeControl.Name?.ToLower() ?? "";
                if (name.Contains("search")) topic = "search";
                else if (name.Contains("download")) topic = "downloads";
                else if (name.Contains("stat")) topic = "stats";
                else if (name.Contains("config")) topic = "config";
                else if (name.Contains("plugin") || name.Contains("theme")) topic = "extensions";
            }
            
            ShowHelp(topic, parent);
        }
        
        public static void ShowAllShortcuts(Form parent = null)
        {
            ShowHelp("shortcuts", parent);
        }
    }
    
    public class HelpContent
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string[] Shortcuts { get; set; }
    }
    
    public class HelpForm : Form
    {
        public HelpForm(HelpContent content)
        {
            InitializeComponent(content);
        }
        
        private void InitializeComponent(HelpContent content)
        {
            this.Text = "Ayuda - SlskDown";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // Título
            var lblTitle = new Label
            {
                Text = content.Title,
                Location = new Point(20, 20),
                Size = new Size(660, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            this.Controls.Add(lblTitle);
            
            // Contenido
            var txtContent = new RichTextBox
            {
                Location = new Point(20, 70),
                Size = new Size(660, 380),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Text = content.Content
            };
            this.Controls.Add(txtContent);
            
            // Atajos
            if (content.Shortcuts != null && content.Shortcuts.Length > 0)
            {
                var lblShortcuts = new Label
                {
                    Text = "Atajos de Teclado:",
                    Location = new Point(20, 460),
                    Size = new Size(660, 25),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold)
                };
                this.Controls.Add(lblShortcuts);
                
                var txtShortcuts = new TextBox
                {
                    Location = new Point(20, 490),
                    Size = new Size(660, 60),
                    BackColor = Color.FromArgb(40, 40, 40),
                    ForeColor = Color.LightGray,
                    Font = new Font("Consolas", 9F),
                    ReadOnly = true,
                    Multiline = true,
                    BorderStyle = BorderStyle.None,
                    Text = string.Join("\r\n", content.Shortcuts)
                };
                this.Controls.Add(txtShortcuts);
            }
            
            // Botón cerrar
            var btnClose = new Button
            {
                Text = "Cerrar",
                Location = new Point(590, 520),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }
    }
}
