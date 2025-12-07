using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MyTextLayout_winui3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        int caretIndex = 0;
        static string text = "🧔🏽‍♂️ is ヒゲの男性: 肌色";
        MyTextLayout layout = new MyTextLayout(text);
        bool inited = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CanvasControl_Draw(CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            if (!inited)
            {
                layout.DefaultForegorundBrush = new CanvasSolidColorBrush(sender, Colors.Black);
                //layout.SetForgroundColor(new CanvasCharacterRange() { CharacterIndex = 0, CharacterCount = 2 }, new CanvasSolidColorBrush(sender, Colors.Blue));
                layout.IsDrawControlCode = false;
                layout.TextFormat = new CanvasTextFormat() { FontFamily = this.Canvas.FontFamily.Source };
                layout.TextDirection = CanvasTextDirection.LeftToRightThenTopToBottom;
                layout.RequireSize = new Windows.Foundation.Size(1000, 500);
                inited = true;
            }

            args.DrawingSession.Clear(Colors.White);

            var actualSize = layout.ActualSize;

            for (int i = 0; i < 1; i++)
            {
                layout.Draw(args.DrawingSession, 0, i * (float)actualSize.Height);
            }

            var regions = layout.GetCharacterRegions();
            foreach (CanvasTextLayoutRegion region in regions)
            {
                args.DrawingSession.DrawRectangle(region.LayoutBounds, Colors.Red);
            }

            regions = layout.GetCharacterRegions(1, 10);
            foreach (CanvasTextLayoutRegion region in regions)
            {
                args.DrawingSession.DrawRectangle(region.LayoutBounds, Colors.Green);
            }

            CanvasTextLayoutRegion r;
            if (caretIndex < text.Length)
            {
                layout.GetCaretPosition(caretIndex, false, out r);
                caretIndex = r.CharacterIndex + r.CharacterCount - 1;
                args.DrawingSession.DrawRectangle(r.LayoutBounds, Colors.Black, 4.0f);
            }

            const float posx = 25, posy = 10;
            layout.HitText(posx, posy, out r);
            args.DrawingSession.DrawCircle(posx, posy, 1, Colors.Green);
            args.DrawingSession.DrawRoundedRectangle(r.LayoutBounds, 5, 5, Colors.Blue);

        }

        private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Left)
            {
                caretIndex--;
                if (caretIndex < 0) caretIndex = 0;
            }
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                caretIndex++;
                if (caretIndex >= text.Length) caretIndex = text.Length - 1;
            }
            this.Canvas.Invalidate();
        }
    }
}
