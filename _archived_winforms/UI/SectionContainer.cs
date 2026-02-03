using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SlskDown.UI
{
    /// <summary>
    /// Contenedor simple para SectionPanel - Layout vertical con scroll
    /// SIN funcionalidad de colapsar - todo siempre visible
    /// </summary>
    public class SectionContainer : Panel
    {
        private FlowLayoutPanel flowLayout;
        private List<SectionPanel> sections = new List<SectionPanel>();
        
        public SectionContainer()
        {
            AutoScroll = true;
            BackColor = Color.Transparent;
            Padding = new Padding(10);
            
            flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            
            Controls.Add(flowLayout);
            
            // Sistema responsive
            this.SizeChanged += SectionContainer_SizeChanged;
        }
        
        private void SectionContainer_SizeChanged(object sender, EventArgs e)
        {
            // Ajustar ancho de todas las secciones al cambiar el tamaño del contenedor
            int newWidth = this.ClientSize.Width - this.Padding.Horizontal;
            if (this.AutoScroll && this.VerticalScroll.Visible)
            {
                newWidth -= SystemInformation.VerticalScrollBarWidth;
            }
            
            flowLayout.SuspendLayout();
            
            foreach (var section in sections)
            {
                if (section.Visible && newWidth > 0)
                {
                    section.Width = newWidth;
                }
            }
            
            flowLayout.ResumeLayout(true);
        }
        
        public void AddSection(SectionPanel section)
        {
            section.Margin = new Padding(0, 0, 0, 0);
            
            int newWidth = this.ClientSize.Width - this.Padding.Horizontal;
            if (this.AutoScroll && this.VerticalScroll.Visible)
            {
                newWidth -= SystemInformation.VerticalScrollBarWidth;
            }
            
            if (newWidth > 0)
            {
                section.Width = newWidth;
            }
            
            flowLayout.Controls.Add(section);
            sections.Add(section);
        }
    }
}
