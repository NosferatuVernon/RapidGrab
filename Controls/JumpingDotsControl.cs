using System;
using System.Drawing;
using System.Windows.Forms;

namespace RapidGrab_file_downloader.Controls
{
    public sealed class JumpingDotsControl : Control
    {
        private const int DotCount = 8;
        private const int DotSize = 8;
        private const int DotSpacing = 6;
        private const int DotInterval = 200;

        private int currentDot;
        private SolidBrush brush;


        public JumpingDotsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

            // Set the control's BackColor to transparent
            BackColor = Color.Transparent;

            var timer = new Timer();
            timer.Interval = DotInterval;
            timer.Tick += Timer_Tick;
            timer.Start();

            brush = new SolidBrush(ForeColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            brush = new SolidBrush(Color.FromArgb(0,122,204));

            for (int i = 0; i < DotCount; i++)
            {
                var x = (DotSize + DotSpacing) * i + (Width - (DotSize + DotSpacing) * DotCount) / 2;
                var y = Height / 2 - DotSize / 2;

                // Highlight the current dot
                e.Graphics.FillEllipse(i == currentDot ? Brushes.DodgerBlue : brush, x, y, DotSize, DotSize);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            currentDot = (currentDot + 1) % DotCount;
            Invalidate();
        }
    }
}