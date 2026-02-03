using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Simple modal dialog that captures a string input.
    /// </summary>
    public class InputBox : Form
    {
        private readonly TextBox textBox;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public string Value { get; private set; } = string.Empty;

        public InputBox(string title, string prompt)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 150);
            BackColor = Color.FromArgb(18, 18, 18);
            MaximizeBox = false;
            MinimizeBox = false;

            var lblPrompt = new Label
            {
                Text = prompt,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 40
            };

            textBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(25, 25, 25)
            };

            btnOk = new Button
            {
                Text = "Aceptar",
                DialogResult = DialogResult.OK,
                Width = 80,
                Height = 30,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(220, 5)
            };
            btnOk.Click += (_, _) =>
            {
                Value = textBox.Text;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 30,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Location = new Point(310, 5)
            };
            btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttonPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
            Controls.AddRange(new Control[] { lblPrompt, textBox, buttonPanel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
