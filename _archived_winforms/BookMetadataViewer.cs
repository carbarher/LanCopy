using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    public class BookMetadataViewer : Form
    {
        private BookMetadata metadata;
        private string filePath;
        private BookMetadataService metadataService;

        private PictureBox coverBox;
        private Label titleLabel;
        private Label authorLabel;
        private RichTextBox synopsisBox;
        private RichTextBox backCoverBox;
        private FlowLayoutPanel tagsPanel;
        private Label infoLabel;
        private Button btnSave;
        private Button btnRefresh;
        private Button btnDownloadCover;
        private ProgressBar progressBar;

        public BookMetadataViewer(string bookFilePath)
        {
            this.filePath = bookFilePath;
            this.metadataService = new BookMetadataService();
            
            InitializeUI();
            LoadMetadataAsync();
        }

        private void InitializeUI()
        {
            // Configuración de la ventana
            this.Text = "📖 Metadata del Libro";
            this.Size = new Size(900, 700);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Portada
            coverBox = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(200, 300),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            this.Controls.Add(coverBox);

            // Título
            titleLabel = new Label
            {
                Location = new Point(240, 20),
                Size = new Size(620, 40),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "Cargando..."
            };
            this.Controls.Add(titleLabel);

            // Autor
            authorLabel = new Label
            {
                Location = new Point(240, 65),
                Size = new Size(620, 25),
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(200, 200, 200),
                Text = ""
            };
            this.Controls.Add(authorLabel);

            // Información adicional
            infoLabel = new Label
            {
                Location = new Point(240, 95),
                Size = new Size(620, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 150, 150),
                Text = ""
            };
            this.Controls.Add(infoLabel);

            // Sinopsis
            var synopsisTitle = new Label
            {
                Location = new Point(240, 125),
                Size = new Size(620, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "📄 Sinopsis"
            };
            this.Controls.Add(synopsisTitle);

            synopsisBox = new RichTextBox
            {
                Location = new Point(240, 150),
                Size = new Size(620, 150),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(synopsisBox);

            // Contraportada
            var backCoverTitle = new Label
            {
                Location = new Point(20, 330),
                Size = new Size(840, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "📖 Contraportada"
            };
            this.Controls.Add(backCoverTitle);

            backCoverBox = new RichTextBox
            {
                Location = new Point(20, 355),
                Size = new Size(840, 150),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(backCoverBox);

            // Tags/Keywords
            var tagsTitle = new Label
            {
                Location = new Point(20, 515),
                Size = new Size(840, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "🏷️ Etiquetas"
            };
            this.Controls.Add(tagsTitle);

            tagsPanel = new FlowLayoutPanel
            {
                Location = new Point(20, 540),
                Size = new Size(840, 60),
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            this.Controls.Add(tagsPanel);

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 610),
                Size = new Size(840, 10),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            this.Controls.Add(progressBar);

            // Botones
            btnRefresh = new Button
            {
                Location = new Point(20, 625),
                Size = new Size(150, 35),
                Text = "🔄 Actualizar",
                BackColor = Color.FromArgb(60, 90, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnRefresh.Click += (s, e) => RefreshMetadata();
            this.Controls.Add(btnRefresh);

            btnDownloadCover = new Button
            {
                Location = new Point(180, 625),
                Size = new Size(180, 35),
                Text = "📥 Descargar Portada",
                BackColor = Color.FromArgb(60, 120, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false
            };
            btnDownloadCover.Click += async (s, e) => await DownloadCover();
            this.Controls.Add(btnDownloadCover);

            btnSave = new Button
            {
                Location = new Point(710, 625),
                Size = new Size(150, 35),
                Text = "💾 Guardar",
                BackColor = Color.FromArgb(90, 60, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false
            };
            btnSave.Click += (s, e) => SaveMetadata();
            this.Controls.Add(btnSave);
        }

        private async void LoadMetadataAsync()
        {
            progressBar.Visible = true;
            btnRefresh.Enabled = false;

            try
            {
                // Extraer título y autor del nombre del archivo
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var (title, author) = ParseFileName(fileName);

                titleLabel.Text = $"Buscando: {title}";
                if (!string.IsNullOrEmpty(author))
                    authorLabel.Text = $"Autor: {author}";

                // Buscar metadata
                metadata = await metadataService.SearchMetadata(title, author);

                if (metadata != null)
                {
                    DisplayMetadata();
                }
                else
                {
                    titleLabel.Text = "❌ No se encontró metadata";
                    synopsisBox.Text = "No se pudo encontrar información para este libro en las bases de datos públicas.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar metadata:\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
                btnRefresh.Enabled = true;
            }
        }

        private void DisplayMetadata()
        {
            // Título y autor
            titleLabel.Text = metadata.Title ?? "Sin título";
            authorLabel.Text = metadata.Author ?? "Autor desconocido";

            // Info adicional
            var infoParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(metadata.Publisher))
                infoParts.Add($"📚 {metadata.Publisher}");
            if (!string.IsNullOrEmpty(metadata.PublishDate))
                infoParts.Add($"📅 {metadata.PublishDate}");
            if (metadata.PageCount > 0)
                infoParts.Add($"📄 {metadata.PageCount} páginas");
            if (metadata.Rating > 0)
                infoParts.Add($"⭐ {metadata.Rating:F1}/5");
            
            infoParts.Add($"🔍 Fuente: {metadata.Source}");
            infoLabel.Text = string.Join("  |  ", infoParts);

            // Sinopsis
            synopsisBox.Text = metadata.Synopsis ?? "Sin sinopsis disponible.";

            // Contraportada
            backCoverBox.Text = metadata.BackCover ?? "Sin contraportada disponible.";

            // Tags
            tagsPanel.Controls.Clear();
            foreach (var keyword in metadata.Keywords)
            {
                var tagLabel = new Label
                {
                    Text = keyword,
                    AutoSize = true,
                    Padding = new Padding(8, 4, 8, 4),
                    Margin = new Padding(5),
                    BackColor = Color.FromArgb(60, 90, 120),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9),
                    BorderStyle = BorderStyle.FixedSingle
                };
                tagsPanel.Controls.Add(tagLabel);
            }

            // Portada
            if (!string.IsNullOrEmpty(metadata.CoverUrl))
            {
                LoadCoverAsync(metadata.CoverUrl);
                btnDownloadCover.Enabled = true;
            }

            btnSave.Enabled = true;
        }

        private async void LoadCoverAsync(string url)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var imageBytes = await client.GetByteArrayAsync(url);
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        coverBox.Image = Image.FromStream(ms);
                    }
                }
            }
            catch
            {
                // Si falla, mostrar placeholder
                coverBox.BackColor = Color.FromArgb(60, 60, 60);
            }
        }

        private void RefreshMetadata()
        {
            LoadMetadataAsync();
        }

        private async Task DownloadCover()
        {
            if (string.IsNullOrEmpty(metadata?.CoverUrl))
                return;

            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Imagen JPG|*.jpg|Imagen PNG|*.png",
                    FileName = $"{metadata.Title} - Cover"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    using (var client = new HttpClient())
                    {
                        var imageBytes = await client.GetByteArrayAsync(metadata.CoverUrl);
                        File.WriteAllBytes(saveDialog.FileName, imageBytes);
                        MessageBox.Show("Portada descargada exitosamente!", "Éxito", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al descargar portada:\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMetadata()
        {
            if (metadata == null)
                return;

            try
            {
                // Guardar metadata como archivo JSON junto al libro
                var metadataPath = Path.ChangeExtension(filePath, ".metadata.json");
                var json = System.Text.Json.JsonSerializer.Serialize(metadata, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                File.WriteAllText(metadataPath, json);

                MessageBox.Show($"Metadata guardada en:\n{metadataPath}", "Guardado", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar metadata:\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private (string title, string author) ParseFileName(string fileName)
        {
            // Intentar parsear formatos comunes:
            // "Autor - Titulo"
            // "Titulo (Autor)"
            // "Titulo - Autor"
            
            if (fileName.Contains(" - "))
            {
                var parts = fileName.Split(new[] { " - " }, 2, StringSplitOptions.None);
                return (parts[1].Trim(), parts[0].Trim());
            }
            
            if (fileName.Contains("(") && fileName.Contains(")"))
            {
                var titleEnd = fileName.IndexOf("(");
                var authorStart = fileName.IndexOf("(") + 1;
                var authorEnd = fileName.IndexOf(")");
                
                var title = fileName.Substring(0, titleEnd).Trim();
                var author = fileName.Substring(authorStart, authorEnd - authorStart).Trim();
                
                return (title, author);
            }

            // Si no hay patrón reconocible, asumir que todo es el título
            return (fileName, null);
        }
    }
}
