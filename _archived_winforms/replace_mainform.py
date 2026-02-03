from pathlib import Path
import shutil

# Backup del archivo original
shutil.copy('MainForm.cs', 'MainForm.cs.backup_full')
print("Backup created: MainForm.cs.backup_full")

# Crear archivo mínimo
minimal_content = """using System;
using System.Windows.Forms;

namespace SlskDown
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
    }
}
"""

# Escribir archivo mínimo
Path('MainForm.cs').write_text(minimal_content, encoding='utf-8')
print("MainForm.cs replaced with minimal version")

# Verificar
lines = Path('MainForm.cs').read_text(encoding='utf-8').split('\n')
print(f"New MainForm.cs has {len(lines)} lines")
