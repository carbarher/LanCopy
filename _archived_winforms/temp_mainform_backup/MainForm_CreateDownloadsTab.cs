        private void CreateDownloadsTab(TabPage parent)
        {
            parent.BackColor = Color.FromArgb(18, 18, 18);

            var outerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(18, 18, 18)
            };
            outerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(25, 25, 25),
                Padding = new Padding(15),
                Margin = new Padding(0, 0, 0, 10)
            };

            Button CreateActionButton(string text, Color color, EventHandler handler)
            {
                var button = new Button
                {
                    Text = text,
                    AutoSize = true,
                    BackColor = color,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Margin = new Padding(0, 0, 15, 10)
                };
                button.FlatAppearance.BorderSize = 0;
                button.Click += handler;
                return button;
            }

            buttonsPanel.Controls.Add(CreateActionButton("⏸️ Pausar Todo", Color.FromArgb(200, 150, 0), (s, e) => PauseAllDownloads()));
            buttonsPanel.Controls.Add(CreateActionButton("▶️ Reanudar Todo", Color.FromArgb(29, 185, 84), (s, e) => ResumeAllDownloads()));
            buttonsPanel.Controls.Add(CreateActionButton("❌ Cancelar Todo", Color.FromArgb(200, 50, 50), (s, e) => CancelAllDownloads()));
            buttonsPanel.Controls.Add(CreateActionButton("🗑️ Limpiar Completados", Color.FromArgb(100, 100, 100), (s, e) => ClearCompletedDownloads()));
            buttonsPanel.Controls.Add(CreateActionButton("📁 Abrir Carpeta", Color.FromArgb(0, 120, 215), (s, e) => OpenDownloadFolder()));

            outerLayout.Controls.Add(buttonsPanel, 0, 0);

            lvDownloads = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            lvDownloads.Columns.Add("Archivo", 400);
            lvDownloads.Columns.Add("Estado", 130);
            lvDownloads.Columns.Add("Progreso", 110);
            lvDownloads.Columns.Add("Velocidad", 110);
            lvDownloads.Columns.Add("Usuario", 160);
            lvDownloads.Columns.Add("Tamaño", 110);

            var downloadsContext = new ContextMenuStrip { BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            downloadsContext.Items.Add(new ToolStripMenuItem("⏸️ Pausar", null, (s, e) => PauseSelectedDownload()));
            downloadsContext.Items.Add(new ToolStripMenuItem("▶️ Reanudar", null, (s, e) => ResumeSelectedDownload()));
            downloadsContext.Items.Add(new ToolStripMenuItem("❌ Cancelar", null, (s, e) => CancelSelectedDownload()));
            downloadsContext.Items.Add(new ToolStripSeparator());
            downloadsContext.Items.Add(new ToolStripMenuItem("📁 Abrir Carpeta", null, (s, e) => OpenDownloadFolder()));
            lvDownloads.ContextMenuStrip = downloadsContext;

            outerLayout.Controls.Add(lvDownloads, 0, 1);
            parent.Controls.Add(outerLayout);
        }
