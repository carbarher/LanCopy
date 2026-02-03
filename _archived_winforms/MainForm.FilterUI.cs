using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown
{
    public partial class MainForm
    {
        private void ShowSaveFilterDialog()
        {
            if (filterPresetManager == null)
            {
                MessageBox.Show("El gestor de filtros no está inicializado.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var dialog = new Form
            {
                Text = "Guardar Filtro de Búsqueda",
                Size = new Size(500, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(20),
                AutoScroll = true,
                WrapContents = false
            };

            // Nombre del filtro
            var lblName = new Label
            {
                Text = "Nombre del Filtro:",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblName);

            var txtName = new TextBox
            {
                Width = 440,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtName);

            // Descripción
            var lblDesc = new Label
            {
                Text = "Descripción (opcional):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblDesc);

            var txtDesc = new TextBox
            {
                Width = 440,
                Height = 60,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtDesc);

            // Tamaño mínimo
            var lblMinSize = new Label
            {
                Text = "Tamaño Mínimo (KB):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblMinSize);

            var numMinSize = new NumericUpDown
            {
                Width = 440,
                Minimum = 0,
                Maximum = 1000000,
                Value = minFileSizeKB,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(numMinSize);

            // Tamaño máximo
            var lblMaxSize = new Label
            {
                Text = "Tamaño Máximo (KB, 0 = sin límite):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblMaxSize);

            var numMaxSize = new NumericUpDown
            {
                Width = 440,
                Minimum = 0,
                Maximum = 10000000,
                Value = 0,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(numMaxSize);

            // Extensiones permitidas
            var lblExtensions = new Label
            {
                Text = "Extensiones Permitidas (separadas por coma):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblExtensions);

            var txtExtensions = new TextBox
            {
                Width = 440,
                Text = "epub,pdf,mobi",
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtExtensions);

            // Palabras excluidas
            var lblExclude = new Label
            {
                Text = "Palabras Excluidas (separadas por coma):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblExclude);

            var txtExclude = new TextBox
            {
                Width = 440,
                Height = 60,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtExclude);

            // Botones
            var btnPanel = new FlowLayoutPanel
            {
                Width = 440,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };

            var btnSave = new Button
            {
                Text = "Guardar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnSave.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnSave.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Debes especificar un nombre para el filtro.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var filter = new SavedSearchFilter
                {
                    Name = txtName.Text.Trim(),
                    Description = txtDesc.Text.Trim(),
                    MinFileSizeKB = (int)numMinSize.Value,
                    MaxFileSizeKB = (int)numMaxSize.Value > 0 ? (int)numMaxSize.Value : (int?)null,
                    AllowedExtensions = txtExtensions.Text.Split(',')
                        .Select(e => e.Trim().ToLower())
                        .Where(e => !string.IsNullOrEmpty(e))
                        .ToList(),
                    ExcludedWords = txtExclude.Text.Split(',')
                        .Select(w => w.Trim().ToLower())
                        .Where(w => !string.IsNullOrEmpty(w))
                        .ToList()
                };

                filterPresetManager.AddFilter(filter);
                Log($"Filtro guardado: {filter.Name}");
                MessageBox.Show($"Filtro '{filter.Name}' guardado correctamente.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                dialog.Close();
            };

            btnCancel.Click += (s, e) => dialog.Close();

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(btnPanel);

            dialog.Controls.Add(mainPanel);
            dialog.ShowDialog(this);
        }

        private void ShowLoadFilterDialog()
        {
            if (filterPresetManager == null)
            {
                MessageBox.Show("El gestor de filtros no está inicializado.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var filters = filterPresetManager.GetAllFilters();
            if (filters.Count == 0)
            {
                MessageBox.Show("No hay filtros guardados. Guarda un filtro primero.", "Información",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dialog = new Form
            {
                Text = "Cargar Filtro de Búsqueda",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(20)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ListView de filtros
            var lvFilters = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            lvFilters.Columns.Add("Nombre", 200);
            lvFilters.Columns.Add("Descripción", 250);
            lvFilters.Columns.Add("Creado", 100);

            foreach (var filter in filters)
            {
                var item = new ListViewItem(filter.Name);
                item.SubItems.Add(filter.Description ?? "");
                item.SubItems.Add(filter.CreatedAt.ToString("dd/MM/yyyy"));
                item.Tag = filter;
                lvFilters.Items.Add(item);
            }

            mainPanel.Controls.Add(lvFilters, 0, 0);

            // Panel de botones
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };

            var btnLoad = new Button
            {
                Text = "Cargar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnLoad.FlatAppearance.BorderSize = 0;

            var btnDelete = new Button
            {
                Text = "Eliminar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnDelete.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnLoad.Click += (s, e) =>
            {
                if (lvFilters.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona un filtro para cargar.", "Información",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var filter = lvFilters.SelectedItems[0].Tag as SavedSearchFilter;
                if (filter != null)
                {
                    // Aplicar filtro a la configuración actual
                    minFileSizeKB = filter.MinFileSizeKB;
                    // Aquí puedes aplicar más configuraciones según necesites
                    
                    Log($"Filtro cargado: {filter.Name}");
                    MessageBox.Show($"Filtro '{filter.Name}' aplicado correctamente.", "Éxito",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dialog.Close();
                }
            };

            btnDelete.Click += (s, e) =>
            {
                if (lvFilters.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona un filtro para eliminar.", "Información",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var filter = lvFilters.SelectedItems[0].Tag as SavedSearchFilter;
                if (filter != null)
                {
                    var result = MessageBox.Show($"¿Eliminar el filtro '{filter.Name}'?", "Confirmar",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        filterPresetManager.RemoveFilter(filter.Id);
                        lvFilters.Items.Remove(lvFilters.SelectedItems[0]);
                        Log($"Filtro eliminado: {filter.Name}");
                    }
                }
            };

            btnCancel.Click += (s, e) => dialog.Close();

            btnPanel.Controls.Add(btnLoad);
            btnPanel.Controls.Add(btnDelete);
            btnPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(btnPanel, 0, 1);

            dialog.Controls.Add(mainPanel);
            dialog.ShowDialog(this);
        }
    }
}
