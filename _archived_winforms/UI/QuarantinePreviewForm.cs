using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;

namespace SlskDown.UI
{
    internal sealed class QuarantinePreviewForm : Form
    {
        private sealed class FileItem
        {
            public FileItem(string fullPath, string display)
            {
                FullPath = fullPath;
                Display = display;
            }

            public string FullPath { get; }
            public string Display { get; }

            public override string ToString() => Display;
        }

        private readonly CheckedListBox filesList;
        private readonly Button btnDelete;
        private readonly Button btnCancel;
        private readonly Button btnSelectAll;
        private readonly Button btnDeselectAll;

        internal IReadOnlyList<string> SelectedPaths => filesList.CheckedItems.Cast<FileItem>().Select(f => f.FullPath).ToList();

        internal QuarantinePreviewForm(
            IEnumerable<string> filePaths,
            string baseDirectory,
            string title = "Revisar archivos no españoles",
            string actionButtonText = "Eliminar seleccionados",
            string emptySelectionMessage = "Selecciona al menos un archivo para eliminar.")
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(600, 400);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            filesList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var footerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10),
                ColumnCount = 5
            };
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            btnSelectAll = new Button
            {
                Text = "Seleccionar todo",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            btnSelectAll.FlatAppearance.BorderSize = 0;
            btnSelectAll.Click += (_, __) => SetAllChecked(true);

            btnDeselectAll = new Button
            {
                Text = "Deseleccionar todo",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            btnDeselectAll.FlatAppearance.BorderSize = 0;
            btnDeselectAll.Click += (_, __) => SetAllChecked(false);

            btnDelete = new Button
            {
                Text = actionButtonText,
                DialogResult = DialogResult.OK,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                Padding = new Padding(12, 6, 12, 6)
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += (_, __) =>
            {
                if (SelectedPaths.Count == 0)
                {
                    MessageBox.Show(this, emptySelectionMessage, actionButtonText, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                }
            };

            btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Padding = new Padding(12, 6, 12, 6)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            footerPanel.Controls.Add(btnSelectAll, 0, 0);
            footerPanel.Controls.Add(btnDeselectAll, 1, 0);
            footerPanel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 2, 0);
            footerPanel.Controls.Add(btnDelete, 3, 0);
            footerPanel.Controls.Add(btnCancel, 4, 0);

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            container.Controls.Add(filesList, 0, 0);
            container.Controls.Add(footerPanel, 0, 1);

            Controls.Add(container);

            PopulateFiles(filePaths ?? Array.Empty<string>(), baseDirectory);
        }

        private void PopulateFiles(IEnumerable<string> paths, string baseDirectory)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string display;
                if (!string.IsNullOrWhiteSpace(baseDirectory) && path.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    display = path.Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    display = path;
                }

                filesList.Items.Add(new FileItem(path, display), true);
            }
        }

        private void SetAllChecked(bool value)
        {
            for (int i = 0; i < filesList.Items.Count; i++)
            {
                filesList.SetItemChecked(i, value);
            }
        }
    }
}
