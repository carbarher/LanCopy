using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Sistema de auto-limpieza de autores sin resultados
    /// </summary>
    public partial class MainForm
    {
        private static readonly string authorAttemptsFile = @"c:\p2p\SlskDown\author_attempts.json";
        
        /// <summary>
        /// Estructura para tracking de intentos fallidos
        /// </summary>
        public struct AuthorAttemptRecord
        {
            public string Author { get; set; }
            public int FailedAttempts { get; set; }
            public DateTime LastAttempt { get; set; }
            public bool IsBlacklisted { get; set; }
            public DateTime BlacklistedAt { get; set; }
        }
        
        /// <summary>
        /// Inicializar sistema de auto-limpieza
        /// </summary>
        private void InitializeAuthorAutoCleanup()
        {
            LoadAuthorAttempts();
            Console.WriteLine($"[AuthorCleanup] ðŸ§¹ Sistema inicializado - LÃ­mite: {maxFailedAttempts} intentos");
        }
        
        /// <summary>
        /// Registrar intento de bÃºsqueda para autor
        /// </summary>
        private void RegisterAuthorAttempt(string author, bool hasResults)
        {
            try
            {
                if (string.IsNullOrEmpty(author))
                    return;
                
                author = author.ToLower().Trim();
                
                if (hasResults)
                {
                    // Resetear contador si hay resultados
                    if (authorFailedAttempts.ContainsKey(author))
                    {
                        authorFailedAttempts[author] = 0;
                        Console.WriteLine($"[AuthorCleanup] âœ… Autor '{author}' tiene resultados - contador reseteado");
                    }
                }
                else
                {
                    // Incrementar contador si no hay resultados
                    if (!authorFailedAttempts.ContainsKey(author))
                    {
                        authorFailedAttempts[author] = 0;
                    }
                    
                    authorFailedAttempts[author]++;
                    
                    Console.WriteLine($"[AuthorCleanup] âŒ Autor '{author}' sin resultados - Intentos: {authorFailedAttempts[author]}/{maxFailedAttempts}");
                    
                    // Verificar si se alcanzÃ³ el lÃ­mite
                    if (authorFailedAttempts[author] >= maxFailedAttempts)
                    {
                        RemoveAuthorFromList(author);
                    }
                }
                
                // Guardar estado actual
                SaveAuthorAttempts();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error registrando intento: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Eliminar autor de la lista por superar lÃ­mite de intentos
        /// </summary>
        private void RemoveAuthorFromList(string author)
        {
            try
            {
                if (authorsList.InvokeRequired)
                {
                    authorsList.Invoke(new Action<string>(RemoveAuthorFromList), author);
                    return;
                }
                
                // Buscar y eliminar autor de la lista
                for (int i = 0; i < authorsList.Items.Count; i++)
                {
                    if (authorsList.Items[i].ToString().ToLower().Trim() == author)
                    {
                        var removedAuthor = authorsList.Items[i].ToString();
                        authorsList.Items.RemoveAt(i);
                        
                        // Log de eliminaciÃ³n
                        if (authorSearchLog.InvokeRequired)
                        {
                            authorSearchLog.Invoke(() =>
                            {
                                authorSearchLog.AppendText($"ðŸ§¹ AUTOR ELIMINADO: '{removedAuthor}' (sin resultados por {maxFailedAttempts} intentos consecutivos)\r\n");
                                authorSearchLog.SelectionStart = authorSearchLog.Text.Length;
                                authorSearchLog.ScrollToCaret();
                            });
                        }
                        else
                        {
                            authorSearchLog.AppendText($"ðŸ§¹ AUTOR ELIMINADO: '{removedAuthor}' (sin resultados por {maxFailedAttempts} intentos consecutivos)\r\n");
                        }
                        
                        // Marcar como blacklisteado en el tracking
                        var record = new AuthorAttemptRecord
                        {
                            Author = author,
                            FailedAttempts = authorFailedAttempts[author],
                            LastAttempt = DateTime.Now,
                            IsBlacklisted = true,
                            BlacklistedAt = DateTime.Now
                        };
                        
                        SaveBlacklistedAuthor(record);
                        
                        Console.WriteLine($"[AuthorCleanup] ðŸ—‘ï¸ Autor eliminado de la lista: {removedAuthor}");
                        
                        // Guardar lista actualizada
                        SaveAuthorsList();
                        
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error eliminando autor: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cargar intentos de autores desde archivo
        /// </summary>
        private void LoadAuthorAttempts()
        {
            try
            {
                if (File.Exists(authorAttemptsFile))
                {
                    var json = File.ReadAllText(authorAttemptsFile);
                    var records = JsonSerializer.Deserialize<Dictionary<string, AuthorAttemptRecord>>(json);
                    
                    if (records != null)
                    {
                        authorFailedAttempts.Clear();
                        
                        foreach (var kvp in records)
                        {
                            if (!kvp.Value.IsBlacklisted)
                            {
                                authorFailedAttempts[kvp.Key] = kvp.Value.FailedAttempts;
                            }
                        }
                        
                        Console.WriteLine($"[AuthorCleanup] ðŸ“‚ Cargados {authorFailedAttempts.Count} registros de intentos");
                    }
                }
                else
                {
                    Console.WriteLine("[AuthorCleanup] â„¹ï¸ No existe archivo de intentos - iniciando desde cero");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error cargando intentos: {ex.Message}");
                authorFailedAttempts.Clear();
            }
        }
        
        /// <summary>
        /// Guardar intentos de autores a archivo
        /// </summary>
        private void SaveAuthorAttempts()
        {
            try
            {
                var records = new Dictionary<string, AuthorAttemptRecord>();
                
                foreach (var kvp in authorFailedAttempts)
                {
                    records[kvp.Key] = new AuthorAttemptRecord
                    {
                        Author = kvp.Key,
                        FailedAttempts = kvp.Value,
                        LastAttempt = DateTime.Now,
                        IsBlacklisted = false
                    };
                }
                
                var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(authorAttemptsFile, json);
                
                Console.WriteLine($"[AuthorCleanup] ðŸ’¾ Guardados {records.Count} registros de intentos");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error guardando intentos: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar autor blacklisteado
        /// </summary>
        private void SaveBlacklistedAuthor(AuthorAttemptRecord record)
        {
            try
            {
                var blacklistFile = @"c:\p2p\SlskDown\blacklisted_authors.json";
                Dictionary<string, AuthorAttemptRecord> blacklisted = new();
                
                if (File.Exists(blacklistFile))
                {
                    var json = File.ReadAllText(blacklistFile);
                    blacklisted = JsonSerializer.Deserialize<Dictionary<string, AuthorAttemptRecord>>(json) ?? new();
                }
                
                blacklisted[record.Author] = record;
                
                var blacklistJson = JsonSerializer.Serialize(blacklisted, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(blacklistFile, blacklistJson);
                
                Console.WriteLine($"[AuthorCleanup] ðŸš« Autor blacklisteado guardado: {record.Author}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error guardando blacklist: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resetear contador de intentos para autor especÃ­fico
        /// </summary>
        private void ResetAuthorAttempts(string author)
        {
            try
            {
                author = author.ToLower().Trim();
                
                if (authorFailedAttempts.ContainsKey(author))
                {
                    authorFailedAttempts[author] = 0;
                    SaveAuthorAttempts();
                    
                    Console.WriteLine($"[AuthorCleanup] ðŸ”„ Contador reseteado para autor: {author}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error reseteando contador: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resetear todos los contadores de intentos
        /// </summary>
        private void ResetAllAuthorAttempts()
        {
            try
            {
                authorFailedAttempts.Clear();
                SaveAuthorAttempts();
                
                Console.WriteLine("[AuthorCleanup] ðŸ”„ Todos los contadores de intentos reseteados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error reseteando todos los contadores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtener estadÃ­sticas de auto-limpieza
        /// </summary>
        private string GetCleanupStatistics()
        {
            try
            {
                var totalAuthors = authorsList.Items.Count;
                var trackedAuthors = authorFailedAttempts.Count;
                var authorsNearLimit = authorFailedAttempts.Count(kvp => kvp.Value >= maxFailedAttempts - 1);
                
                // Cargar blacklisteados
                var blacklistFile = @"c:\p2p\SlskDown\blacklisted_authors.json";
                int blacklistedCount = 0;
                
                if (File.Exists(blacklistFile))
                {
                    var json = File.ReadAllText(blacklistFile);
                    var blacklisted = JsonSerializer.Deserialize<Dictionary<string, AuthorAttemptRecord>>(json);
                    blacklistedCount = blacklisted?.Count ?? 0;
                }
                
                return $"""
ðŸ“Š ESTADÃSTICAS DE AUTO-LIMPIEZA
â”œâ”€â”€ Autores en lista: {totalAuthors}
â”œâ”€â”€ Autores con tracking: {trackedAuthors}
â”œâ”€â”€ Autores cerca del lÃ­mite: {authorsNearLimit}
â”œâ”€â”€ Autores eliminados (blacklist): {blacklistedCount}
â”œâ”€â”€ LÃ­mite configurado: {maxFailedAttempts} intentos
â””â”€â”€ Ãšltima actualizaciÃ³n: {DateTime.Now:HH:mm:ss}
""";
            }
            catch (Exception ex)
            {
                return $"âŒ Error obteniendo estadÃ­sticas: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Mostrar diÃ¡logo de configuraciÃ³n de auto-limpieza
        /// </summary>
        private void ShowCleanupConfiguration()
        {
            try
            {
                var form = new Form
                {
                    Text = "ðŸ§¹ ConfiguraciÃ³n de Auto-Limpieza",
                    Size = new Size(500, 400),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = Color.FromArgb(18, 18, 18),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                
                var layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.Padding = new Padding(20);
                layout.RowCount = 5;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                
                // TÃ­tulo
                var title = new Label();
                title.Text = "âš™ï¸ ConfiguraciÃ³n de Auto-Limpieza de Autores";
                title.ForeColor = Color.FromArgb(59, 130, 246);
                title.Font = new Font("Segoe UI", 12, FontStyle.Bold);
                title.Dock = DockStyle.Fill;
                layout.Controls.Add(title, 0, 0);
                
                // DescripciÃ³n
                var desc = new Label();
                desc.Text = $"Los autores que no muestren resultados durante {maxFailedAttempts} intentos consecutivos serÃ¡n eliminados automÃ¡ticamente de la lista.";
                desc.ForeColor = Color.LightGray;
                desc.Size = new Size(450, 40);
                layout.Controls.Add(desc, 0, 1);
                
                // EstadÃ­sticas
                var statsLabel = new Label();
                statsLabel.Text = GetCleanupStatistics();
                statsLabel.ForeColor = Color.LightGreen;
                statsLabel.Font = new Font("Consolas", 9);
                statsLabel.Size = new Size(450, 120);
                layout.Controls.Add(statsLabel, 0, 2);
                
                // Botones
                var buttonPanel = new FlowLayoutPanel();
                buttonPanel.Dock = DockStyle.Fill;
                
                var resetBtn = new Button();
                resetBtn.Text = "ðŸ”„ Resetear Contadores";
                resetBtn.BackColor = Color.FromArgb(59, 130, 246);
                resetBtn.ForeColor = Color.White;
                resetBtn.FlatStyle = FlatStyle.Flat;
                resetBtn.FlatAppearance.BorderSize = 0;
                resetBtn.Click += (s, e) => 
                {
                    ResetAllAuthorAttempts();
                    statsLabel.Text = GetCleanupStatistics();
                };
                
                var closeBtn = new Button();
                closeBtn.Text = "âŒ Cerrar";
                closeBtn.BackColor = Color.FromArgb(107, 114, 128);
                closeBtn.ForeColor = Color.White;
                closeBtn.FlatStyle = FlatStyle.Flat;
                closeBtn.FlatAppearance.BorderSize = 0;
                closeBtn.Click += (s, e) => form.Close();
                
                buttonPanel.Controls.Add(resetBtn);
                buttonPanel.Controls.Add(closeBtn);
                layout.Controls.Add(buttonPanel, 0, 3);
                
                // Info adicional
                var infoLabel = new Label();
                infoLabel.Text = "ðŸ’¡ Tip: Puedes ajustar el lÃ­mite desde la pestaÃ±a âš™ï¸ Config";
                infoLabel.ForeColor = Color.Gray;
                infoLabel.Size = new Size(450, 20);
                layout.Controls.Add(infoLabel, 0, 4);
                
                form.Controls.Add(layout);
                form.ShowDialog();
                
                Console.WriteLine("[AuthorCleanup] âš™ï¸ DiÃ¡logo de configuraciÃ³n mostrado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorCleanup] âŒ Error mostrando configuraciÃ³n: {ex.Message}");
            }
        }
    }
}

