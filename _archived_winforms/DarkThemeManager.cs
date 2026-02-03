using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    public class DarkThemeManager
    {
        private static readonly Color BackgroundDark = Color.FromArgb(30, 30, 30);
        private static readonly Color BackgroundMedium = Color.FromArgb(40, 40, 40);
        private static readonly Color BackgroundLight = Color.FromArgb(50, 50, 50);
        private static readonly Color BorderColor = Color.FromArgb(70, 70, 70);
        private static readonly Color TextColor = Color.White;
        private static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
        
        public static void ApplyToAll(Control root)
        {
            if (root == null) return;
            
            root.BackColor = BackgroundDark;
            root.ForeColor = TextColor;
            
            foreach (Control control in root.Controls)
            {
                ApplyToControl(control);
                ApplyToAll(control);
            }
        }
        
        private static void ApplyToControl(Control control)
        {
            if (control is Button btn)
            {
                btn.BackColor = BackgroundLight;
                btn.ForeColor = TextColor;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = BorderColor;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 70);
            }
            else if (control is TextBox txt)
            {
                txt.BackColor = BackgroundMedium;
                txt.ForeColor = TextColor;
                txt.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox cmb)
            {
                cmb.BackColor = BackgroundMedium;
                cmb.ForeColor = TextColor;
                cmb.FlatStyle = FlatStyle.Flat;
            }
            else if (control is ListView lv)
            {
                lv.BackColor = BackgroundMedium;
                lv.ForeColor = TextColor;
                lv.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is TreeView tv)
            {
                tv.BackColor = BackgroundMedium;
                tv.ForeColor = TextColor;
                tv.BorderStyle = BorderStyle.FixedSingle;
                tv.LineColor = BorderColor;
            }
            else if (control is DataGridView dgv)
            {
                dgv.BackgroundColor = BackgroundMedium;
                dgv.ForeColor = TextColor;
                dgv.GridColor = BorderColor;
                dgv.BorderStyle = BorderStyle.FixedSingle;
                dgv.EnableHeadersVisualStyles = false;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = BackgroundLight;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
                dgv.DefaultCellStyle.BackColor = BackgroundMedium;
                dgv.DefaultCellStyle.ForeColor = TextColor;
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
                dgv.DefaultCellStyle.SelectionForeColor = TextColor;
            }
            else if (control is Panel pnl)
            {
                pnl.BackColor = BackgroundDark;
            }
            else if (control is GroupBox grp)
            {
                grp.BackColor = BackgroundDark;
                grp.ForeColor = TextColor;
            }
            else if (control is Label lbl)
            {
                lbl.ForeColor = TextColor;
            }
            else if (control is CheckBox chk)
            {
                chk.ForeColor = TextColor;
            }
            else if (control is RadioButton rad)
            {
                rad.ForeColor = TextColor;
            }
            else if (control is TabControl tab)
            {
                tab.BackColor = BackgroundDark;
                tab.ForeColor = TextColor;
            }
            else if (control is TabPage page)
            {
                page.BackColor = BackgroundDark;
                page.ForeColor = TextColor;
            }
            else if (control is NumericUpDown num)
            {
                num.BackColor = BackgroundMedium;
                num.ForeColor = TextColor;
            }
            else if (control is ProgressBar prog)
            {
                prog.BackColor = BackgroundMedium;
                prog.ForeColor = Color.FromArgb(0, 120, 215);
            }
        }
        
        public static void ApplyToForm(Form form)
        {
            if (form == null) return;
            
            form.BackColor = BackgroundDark;
            form.ForeColor = TextColor;
            
            ApplyToAll(form);
        }
    }
}
