using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlskDown.Core;

namespace SlskDown.UI
{
    public sealed class CandidateQueueForm : Form
    {
        private readonly CandidateQueueService service;
        private readonly Func<IReadOnlyList<CandidateQueueItem>, bool, Task> onProcessApproved;

        private readonly ListView lv;
        private readonly CheckBox chkShowApproved;
        private readonly CheckBox chkDryRun;

        private readonly System.Windows.Forms.Timer refreshDebounceTimer;
        private bool refreshInProgress;
        private bool refreshRequested;

        public CandidateQueueForm(
            CandidateQueueService service,
            bool dryRun,
            Func<IReadOnlyList<CandidateQueueItem>, bool, Task> onProcessApproved)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.onProcessApproved = onProcessApproved ?? throw new ArgumentNullException(nameof(onProcessApproved));

            Text = "📝 Cola de candidatos";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            Width = 980;
            Height = 640;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor
            };

            chkShowApproved = new CheckBox
            {
                Text = "Mostrar aprobados",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 18, 0)
            };

            chkDryRun = new CheckBox
            {
                Text = "Dry-run (no descargar)",
                Checked = dryRun,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 18, 0)
            };

            var btnRefresh = CreateButton("Refrescar", Color.FromArgb(100, 100, 100), async (_, _) => await RefreshAsync());
            btnRefresh.Margin = new Padding(0, 0, 12, 0);

            header.Controls.Add(chkShowApproved);
            header.Controls.Add(chkDryRun);
            header.Controls.Add(btnRefresh);

            refreshDebounceTimer = new System.Windows.Forms.Timer
            {
                Interval = 250
            };
            refreshDebounceTimer.Tick += async (_, _) =>
            {
                refreshDebounceTimer.Stop();
                await RefreshAsync();
            };

            lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(25, 25, 28),
                ForeColor = Color.FromArgb(240, 240, 245),
                Font = new Font("Segoe UI", 10f),
                CheckBoxes = true
            };

            lv.Columns.Add("Estado", 110);
            lv.Columns.Add("Score", 70);
            lv.Columns.Add("Archivo", 360);
            lv.Columns.Add("Usuario", 140);
            lv.Columns.Add("Tamaño", 110);
            lv.Columns.Add("Autor", 180);
            lv.Columns.Add("Reasons", 320);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = BackColor
            };

            var btnClose = CreateButton("Cerrar", Color.FromArgb(64, 64, 64), (_, _) => Close());
            var btnProcess = CreateButton("Procesar aprobados", Color.FromArgb(0, 120, 215), async (_, _) => await ProcessApprovedAsync());
            var btnApprove = CreateButton("Aprobar", Color.FromArgb(16, 185, 129), (_, _) => ApproveSelected());
            var btnUnapprove = CreateButton("Quitar aprobación", Color.FromArgb(245, 158, 11), (_, _) => UnapproveSelected());
            var btnRemove = CreateButton("Eliminar", Color.FromArgb(220, 38, 38), (_, _) => RemoveSelected());

            footer.Controls.Add(btnClose);
            footer.Controls.Add(btnProcess);
            footer.Controls.Add(btnRemove);
            footer.Controls.Add(btnUnapprove);
            footer.Controls.Add(btnApprove);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(lv, 0, 1);
            layout.Controls.Add(footer, 0, 2);

            Controls.Add(layout);

            Shown += (_, _) => ScheduleRefresh();
        }

        public bool DryRunEnabled => chkDryRun.Checked;

        private Button CreateButton(string text, Color color, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Padding = new Padding(14, 8, 14, 8),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += handler;
            return btn;
        }

        private async Task RefreshAsync()
        {
            if (refreshInProgress)
            {
                refreshRequested = true;
                return;
            }

            refreshInProgress = true;
            await Task.Yield();
            try
            {
                var items = service.GetSnapshot();
                var view = items
                    .Where(i => i.Status == CandidateQueueItemStatus.Candidate || (chkShowApproved.Checked && i.Status == CandidateQueueItemStatus.Approved))
                    .OrderByDescending(i => i.Status)
                    .ThenByDescending(i => i.Score)
                    .ThenBy(i => i.File.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                lv.BeginUpdate();
                try
                {
                    lv.Items.Clear();
                    foreach (var item in view)
                    {
                        var li = new ListViewItem(item.Status.ToString())
                        {
                            Tag = item,
                            Checked = item.Status == CandidateQueueItemStatus.Approved
                        };

                        li.SubItems.Add(item.Score.ToString("F1"));
                        li.SubItems.Add(item.File.FileName);
                        li.SubItems.Add(item.File.Username);
                        li.SubItems.Add(FormatSize(item.File.SizeBytes));
                        li.SubItems.Add(item.File.Author ?? string.Empty);
                        li.SubItems.Add(string.Join(", ", item.Reasons ?? new List<string>()));

                        lv.Items.Add(li);
                    }
                }
                finally
                {
                    lv.EndUpdate();
                }
            }
            finally
            {
                refreshInProgress = false;
                if (refreshRequested)
                {
                    refreshRequested = false;
                    ScheduleRefresh();
                }
            }
        }

        private void ScheduleRefresh()
        {
            if (refreshDebounceTimer.Enabled)
            {
                return;
            }

            refreshDebounceTimer.Start();
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private IReadOnlyList<CandidateQueueItem> SelectedItems()
        {
            return lv.CheckedItems
                .Cast<ListViewItem>()
                .Select(i => i.Tag as CandidateQueueItem)
                .Where(i => i != null)
                .Cast<CandidateQueueItem>()
                .ToList();
        }

        private void ApproveSelected()
        {
            var ids = SelectedItems().Select(i => i.Id).ToList();
            if (ids.Count == 0) return;
            service.Approve(ids);
            service.ScheduleSave();
            ScheduleRefresh();
        }

        private void UnapproveSelected()
        {
            var ids = SelectedItems().Select(i => i.Id).ToList();
            if (ids.Count == 0) return;
            service.Unapprove(ids);
            service.ScheduleSave();
            ScheduleRefresh();
        }

        private void RemoveSelected()
        {
            var ids = SelectedItems().Select(i => i.Id).ToList();
            if (ids.Count == 0) return;
            service.Remove(ids);
            service.ScheduleSave();
            ScheduleRefresh();
        }

        private async Task ProcessApprovedAsync()
        {
            var approved = service.GetApproved();
            if (approved.Count == 0)
            {
                MessageBox.Show("No hay elementos aprobados.", "Cola", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await onProcessApproved(approved, chkDryRun.Checked);
            ScheduleRefresh();
        }
    }
}
