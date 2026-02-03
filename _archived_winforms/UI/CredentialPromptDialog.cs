using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown.UI
{
    /// <summary>
    /// Dark-themed dialog for securely entering credentials.
    /// </summary>
    public class CredentialPromptDialog : Form
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private CheckBox chkRemember;
        private Button btnOK;
        private Button btnCancel;
        private Label lblTitle;
        private Label lblUsername;
        private Label lblPassword;

        public string Username => txtUsername.Text;
        public string Password => txtPassword.Text;
        public bool RememberCredentials => chkRemember.Checked;

        public CredentialPromptDialog(string currentUsername = null)
        {
            InitializeComponents();
            
            if (!string.IsNullOrWhiteSpace(currentUsername))
            {
                txtUsername.Text = currentUsername;
                txtPassword.Select();
            }
        }

        private void InitializeComponents()
        {
            // Form properties
            this.Text = "SlskDown - Credenciales Soulseek";
            this.Size = new Size(450, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(28, 28, 32);

            // Title
            lblTitle = new Label
            {
                Text = "🔐 Ingrese sus credenciales de Soulseek",
                Location = new Point(20, 20),
                Size = new Size(410, 30),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255),
                AutoSize = false
            };

            // Username label
            lblUsername = new Label
            {
                Text = "Usuario:",
                Location = new Point(20, 70),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(220, 220, 225)
            };

            // Username textbox
            txtUsername = new TextBox
            {
                Location = new Point(20, 95),
                Size = new Size(410, 30),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Password label
            lblPassword = new Label
            {
                Text = "Contraseña:",
                Location = new Point(20, 135),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(220, 220, 225)
            };

            // Password textbox
            txtPassword = new TextBox
            {
                Location = new Point(20, 160),
                Size = new Size(410, 30),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true
            };

            // Remember checkbox
            chkRemember = new CheckBox
            {
                Text = "Recordar credenciales (almacenamiento seguro)",
                Location = new Point(20, 200),
                Size = new Size(410, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(200, 200, 205),
                Checked = true
            };

            // OK button
            btnOK = new Button
            {
                Text = "Conectar",
                Location = new Point(230, 235),
                Size = new Size(100, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => 
            {
                if (ValidateInput())
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            // Cancel button
            btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(340, 235),
                Size = new Size(90, 35),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // Add controls
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblUsername);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);
            this.Controls.Add(chkRemember);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            // Set accept/cancel buttons
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                // ERROR: DarkMessageBox.Show("Por favor ingrese un nombre de usuario", "Validación", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsername.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                // ERROR: DarkMessageBox.Show("Por favor ingrese una contraseña", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPassword.Focus();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shows the credential prompt dialog and returns the credentials if OK was clicked.
        /// </summary>
        public static (string username, string password, bool remember)? ShowDialog(IWin32Window owner = null, string currentUsername = null)
        {
            using (var dialog = new CredentialPromptDialog(currentUsername))
            {
                if (((Form)dialog).ShowDialog(owner) == DialogResult.OK)
                {
                    return (dialog.Username, dialog.Password, dialog.RememberCredentials);
                }
            }

            return null;
        }
    }
}
