using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown
{
    public partial class MainForm
    {
        private void ShowIntelligentWishlistDialog()
        {
            if (intelligentWishlistManager == null)
            {
                MessageBox.Show("El gestor de wishlist no está inicializado.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var dialog = new Form
            {
                Text = "Wishlist Inteligente",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimumSize = new Size(600, 400)
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(15)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Botones
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Lista
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Estado

            // Panel de botones
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            var btnAdd = new Button
            {
                Text = "➕ Agregar",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 150, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnAdd.FlatAppearance.BorderSize = 0;

            var btnEdit = new Button
            {
                Text = "✏️ Editar",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnEdit.FlatAppearance.BorderSize = 0;

            var btnRemove = new Button
            {
                Text = "🗑️ Eliminar",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnRemove.FlatAppearance.BorderSize = 0;

            var btnSearchNow = new Button
            {
                Text = "🔍 Buscar Ahora",
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSearchNow.FlatAppearance.BorderSize = 0;

            btnPanel.Controls.Add(btnAdd);
            btnPanel.Controls.Add(btnEdit);
            btnPanel.Controls.Add(btnRemove);
            btnPanel.Controls.Add(btnSearchNow);

            mainPanel.Controls.Add(btnPanel, 0, 0);

            // ListView de items
            var lvItems = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                CheckBoxes = true
            };

            lvItems.Columns.Add("✓", 30);
            lvItems.Columns.Add("Término", 200);
            lvItems.Columns.Add("Filtro", 150);
            lvItems.Columns.Add("Resultados", 100);
            lvItems.Columns.Add("Última Búsqueda", 150);
            lvItems.Columns.Add("Estado", 100);

            // Cargar items existentes
            RefreshWishlistView(lvItems);

            mainPanel.Controls.Add(lvItems, 0, 1);

            // Panel de estado
            var lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"Total: {intelligentWishlistManager.GetAllItems().Count} items",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 10, 0, 0)
            };
            mainPanel.Controls.Add(lblStatus, 0, 2);

            // Eventos de botones
            btnAdd.Click += (s, e) =>
            {
                ShowAddWishlistItemDialog();
                RefreshWishlistView(lvItems);
                lblStatus.Text = $"Total: {intelligentWishlistManager.GetAllItems().Count} items";
            };

            btnEdit.Click += (s, e) =>
            {
                if (lvItems.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona un item para editar.", "Información",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var item = lvItems.SelectedItems[0].Tag as IntelligentWishlistItem;
                if (item != null)
                {
                    ShowEditWishlistItemDialog(item);
                    RefreshWishlistView(lvItems);
                }
            };

            btnRemove.Click += (s, e) =>
            {
                if (lvItems.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona un item para eliminar.", "Información",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var item = lvItems.SelectedItems[0].Tag as IntelligentWishlistItem;
                if (item != null)
                {
                    var result = MessageBox.Show($"¿Eliminar '{item.SearchTerm}'?", "Confirmar",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        intelligentWishlistManager.RemoveItem(item.Id);
                        RefreshWishlistView(lvItems);
                        lblStatus.Text = $"Total: {intelligentWishlistManager.GetAllItems().Count} items";
                        Log($"Item eliminado de wishlist: {item.SearchTerm}");
                    }
                }
            };

            btnSearchNow.Click += async (s, e) =>
            {
                var checkedItems = lvItems.CheckedItems.Cast<ListViewItem>()
                    .Select(i => i.Tag as IntelligentWishlistItem)
                    .Where(i => i != null)
                    .ToList();

                if (checkedItems.Count == 0)
                {
                    MessageBox.Show("Marca los items que quieres buscar.", "Información",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                btnSearchNow.Enabled = false;
                btnSearchNow.Text = "Buscando...";

                try
                {
                    foreach (var item in checkedItems)
                    {
                        Log($"Buscando wishlist: {item.SearchTerm}");
                        // Aquí integrarías con tu sistema de búsqueda existente
                        await System.Threading.Tasks.Task.Delay(100);
                    }

                    MessageBox.Show($"Búsqueda completada para {checkedItems.Count} items.", "Éxito",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    btnSearchNow.Enabled = true;
                    btnSearchNow.Text = "🔍 Buscar Ahora";
                    RefreshWishlistView(lvItems);
                }
            };

            dialog.Controls.Add(mainPanel);
            dialog.ShowDialog(this);
        }

        private void RefreshWishlistView(ListView lv)
        {
            if (intelligentWishlistManager == null) return;

            lv.Items.Clear();
            var items = intelligentWishlistManager.GetAllItems();

            foreach (var item in items)
            {
                var lvItem = new ListViewItem("");
                lvItem.Checked = item.IsActive;
                lvItem.SubItems.Add(item.SearchTerm);
                lvItem.SubItems.Add(item.FilterPresetId ?? "Sin filtro");
                lvItem.SubItems.Add(item.DismissedResults.Count.ToString());
                lvItem.SubItems.Add(item.LastSearchTime?.ToString("dd/MM HH:mm") ?? "Nunca");
                lvItem.SubItems.Add(item.IsActive ? "Activo" : "Inactivo");
                lvItem.Tag = item;

                if (!item.IsActive)
                    lvItem.ForeColor = Color.Gray;

                lv.Items.Add(lvItem);
            }
        }

        private void ShowAddWishlistItemDialog()
        {
            var dialog = new Form
            {
                Text = "Agregar a Wishlist",
                Size = new Size(500, 400),
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
                AutoScroll = true
            };

            // Término de búsqueda
            var lblTerm = new Label
            {
                Text = "Término de Búsqueda:",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblTerm);

            var txtTerm = new TextBox
            {
                Width = 440,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtTerm);

            // Filtro preset
            var lblFilter = new Label
            {
                Text = "Filtro (opcional):",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblFilter);

            var cmbFilter = new ComboBox
            {
                Width = 440,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 15)
            };
            cmbFilter.Items.Add("(Sin filtro)");
            
            if (filterPresetManager != null)
            {
                foreach (var filter in filterPresetManager.GetAllFilters())
                {
                    cmbFilter.Items.Add(filter.Name);
                }
            }
            cmbFilter.SelectedIndex = 0;
            mainPanel.Controls.Add(cmbFilter);

            // Notificar
            var chkNotify = new CheckBox
            {
                Text = "Notificar cuando se encuentren resultados",
                ForeColor = Color.White,
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(chkNotify);

            // Activo
            var chkActive = new CheckBox
            {
                Text = "Activar búsqueda automática",
                ForeColor = Color.White,
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 20)
            };
            mainPanel.Controls.Add(chkActive);

            // Botones
            var btnPanel = new FlowLayoutPanel
            {
                Width = 440,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true
            };

            var btnAdd = new Button
            {
                Text = "Agregar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnAdd.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnAdd.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtTerm.Text))
                {
                    MessageBox.Show("Debes especificar un término de búsqueda.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var item = new IntelligentWishlistItem
                {
                    SearchTerm = txtTerm.Text.Trim(),
                    FilterPresetId = cmbFilter.SelectedIndex > 0 ? cmbFilter.SelectedItem.ToString() : null,
                    NotifyOnMatch = chkNotify.Checked,
                    IsActive = chkActive.Checked
                };

                intelligentWishlistManager.AddItem(item);
                Log($"Item agregado a wishlist: {item.SearchTerm}");
                MessageBox.Show($"'{item.SearchTerm}' agregado a la wishlist.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                dialog.Close();
            };

            btnCancel.Click += (s, e) => dialog.Close();

            btnPanel.Controls.Add(btnAdd);
            btnPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(btnPanel);

            dialog.Controls.Add(mainPanel);
            dialog.ShowDialog(this);
        }

        private void ShowEditWishlistItemDialog(IntelligentWishlistItem item)
        {
            var dialog = new Form
            {
                Text = "Editar Item de Wishlist",
                Size = new Size(500, 400),
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
                AutoScroll = true
            };

            // Término de búsqueda
            var lblTerm = new Label
            {
                Text = "Término de Búsqueda:",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };
            mainPanel.Controls.Add(lblTerm);

            var txtTerm = new TextBox
            {
                Width = 440,
                Text = item.SearchTerm,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(txtTerm);

            // Notificar
            var chkNotify = new CheckBox
            {
                Text = "Notificar cuando se encuentren resultados",
                ForeColor = Color.White,
                AutoSize = true,
                Checked = item.NotifyOnMatch,
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(chkNotify);

            // Activo
            var chkActive = new CheckBox
            {
                Text = "Activar búsqueda automática",
                ForeColor = Color.White,
                AutoSize = true,
                Checked = item.IsActive,
                Margin = new Padding(0, 0, 0, 15)
            };
            mainPanel.Controls.Add(chkActive);

            // Botón limpiar resultados descartados
            var btnClearDismissed = new Button
            {
                Text = $"Limpiar {item.DismissedResults.Count} resultados descartados",
                Width = 440,
                Height = 35,
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 20)
            };
            btnClearDismissed.FlatAppearance.BorderSize = 0;
            btnClearDismissed.Click += (s, e) =>
            {
                item.DismissedResults.Clear();
                btnClearDismissed.Text = "Resultados descartados limpiados";
                btnClearDismissed.Enabled = false;
            };
            mainPanel.Controls.Add(btnClearDismissed);

            // Botones
            var btnPanel = new FlowLayoutPanel
            {
                Width = 440,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true
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
                if (string.IsNullOrWhiteSpace(txtTerm.Text))
                {
                    MessageBox.Show("Debes especificar un término de búsqueda.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                item.SearchTerm = txtTerm.Text.Trim();
                item.NotifyOnMatch = chkNotify.Checked;
                item.IsActive = chkActive.Checked;

                intelligentWishlistManager.UpdateItem(item);
                Log($"Item actualizado en wishlist: {item.SearchTerm}");
                MessageBox.Show("Item actualizado correctamente.", "Éxito",
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
    }
}
