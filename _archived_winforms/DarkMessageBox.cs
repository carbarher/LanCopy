using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public static class DarkMessageBox
    {
        public static DialogResult Show(
            string message,
            string title = "Mensaje",
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            // Límite de ancho basado en pantalla para evitar diálogos demasiado angostos o anchos
            var working = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
            int maxTextWidth = Math.Min(900, (int)(working.Width * 0.6));
            int minFormWidth = 420;

            var form = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Padding = new Padding(16)
            };

            // Layout principal: icono + texto arriba, botones abajo
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(root);

            // Icono del sistema
            var picture = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.CenterImage,
                Size = new Size(48, 48),
                Margin = new Padding(0, 0, 12, 0)
            };
            picture.Image = icon switch
            {
                MessageBoxIcon.Error => SystemIcons.Error.ToBitmap(),
                MessageBoxIcon.Warning => SystemIcons.Warning.ToBitmap(),
                MessageBoxIcon.Question => SystemIcons.Question.ToBitmap(),
                _ => SystemIcons.Information.ToBitmap()
            };

            // Mensaje autosizing con máximo de ancho
            var messageLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(maxTextWidth, 0),
                ForeColor = Color.Gainsboro,
                Text = message,
                Margin = new Padding(0),
                UseMnemonic = false
            };

            root.Controls.Add(picture, 0, 0);
            root.Controls.Add(messageLabel, 1, 0);

            // Panel de botones alineados a la derecha
            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.FromArgb(24, 24, 24),
                Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0, 12, 0, 0)
            };

            DialogResult result = DialogResult.OK;

            void CloseWith(DialogResult dr)
            {
                result = dr;
                form.Close();
            }

            Button CreateButton(string text)
            {
                var b = new Button
                {
                    Text = text,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    Margin = new Padding(8, 0, 0, 0),
                    Padding = new Padding(12, 6, 12, 6)
                };
                b.FlatAppearance.BorderSize = 0;
                return b;
            }

            // Crear botones según tipo
            Button btnAccept = null, btnCancel = null;
            if (buttons == MessageBoxButtons.OK)
            {
                btnAccept = CreateButton("Aceptar");
                btnAccept.Click += (s, e) => CloseWith(DialogResult.OK);
                buttonsPanel.Controls.Add(btnAccept);
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                var btnNo = CreateButton("No");
                btnNo.Click += (s, e) => CloseWith(DialogResult.No);
                buttonsPanel.Controls.Add(btnNo);

                btnAccept = CreateButton("S\u00ED");
                btnAccept.Click += (s, e) => CloseWith(DialogResult.Yes);
                buttonsPanel.Controls.Add(btnAccept);
            }
            else if (buttons == MessageBoxButtons.YesNoCancel)
            {
                btnCancel = CreateButton("Cancelar");
                btnCancel.Click += (s, e) => CloseWith(DialogResult.Cancel);
                buttonsPanel.Controls.Add(btnCancel);

                var btnNo = CreateButton("No");
                btnNo.Click += (s, e) => CloseWith(DialogResult.No);
                buttonsPanel.Controls.Add(btnNo);

                btnAccept = CreateButton("S\u00ED");
                btnAccept.Click += (s, e) => CloseWith(DialogResult.Yes);
                buttonsPanel.Controls.Add(btnAccept);
            }

            root.SetColumnSpan(buttonsPanel, 2);
            root.Controls.Add(buttonsPanel, 0, 1);

            // Teclas rápidas
            if (btnAccept != null) form.AcceptButton = btnAccept;
            if (btnCancel != null) form.CancelButton = btnCancel;

            // Calcular tamaño final del formulario tras autosize del label
            form.Load += (s, e) =>
            {
                // Ancho mínimo considerando el área de botones
                int buttonsWidth = 0;
                foreach (Control c in buttonsPanel.Controls) buttonsWidth += c.Width + c.Margin.Horizontal;
                buttonsWidth += 32; // márgenes laterales

                int contentWidth = picture.Width + 12 + messageLabel.Width + 32;
                int finalWidth = Math.Max(Math.Max(contentWidth, buttonsWidth), minFormWidth);
                finalWidth = Math.Min(finalWidth, working.Width - 80);

                int contentHeight = Math.Max(picture.Height, messageLabel.Height) + 16; // margen inferior
                int buttonsHeight = buttonsPanel.Height + 24;
                int finalHeight = contentHeight + buttonsHeight + form.Padding.Vertical + 16;
                finalHeight = Math.Min(finalHeight, working.Height - 80);

                form.ClientSize = new Size(finalWidth, finalHeight);
            };

            form.ShowDialog();
            return result;
        }
    }
}

