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
        Point? tappedPoint = null;
        static string text = "🧔🏽‍♂️ is ヒゲの男性: 肌色." + "\x02" + "test" + "\x01" + "六　大会社　次に掲げる要件のいずれかに該当する株式会社をいう。イ　最終事業年度に係る貸借対照表（第439条前段に規定する場合にあっては、同条の規定により定時株主総会に報告された貸借対照表をいい、株式会社の成立後最初の定時株主総会までの間においては、第435条第1項の貸借対照表をいう。ロにおいて同じ。）に資本金として計上した額が5億円以上であること。ロ　最終事業年度に係る貸借対照表の負債の部に計上した額の合計額が200億円以上であること。";
        const float margin = 100;
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
                layout.IsDrawControlCode = true;
                layout.TextFormat = new CanvasTextFormat() { FontFamily = this.Canvas.FontFamily.Source };
                layout.TextDirection = CanvasTextDirection.LeftToRightThenTopToBottom;
                layout.RequireSize = new Windows.Foundation.Size(1000, 500);
                inited = true;
            }

            args.DrawingSession.Clear(Colors.White);

            var actualSize = layout.ActualSize;

            for (int i = 0; i < 1; i++)
            {
                layout.Draw(args.DrawingSession, 0, margin + i * (float)actualSize.Height);
            }

            var regions = layout.GetCharacterRegions();
            foreach (CanvasTextLayoutRegion region in regions)
            {
                var rect = new Rect(region.LayoutBounds.Left, region.LayoutBounds.Top + margin, region.LayoutBounds.Width, region.LayoutBounds.Height);
                args.DrawingSession.DrawRectangle(rect, Colors.Red);
            }

            regions = layout.GetCharacterRegions(1, 10);
            foreach (CanvasTextLayoutRegion region in regions)
            {
                var rect = new Rect(region.LayoutBounds.Left, region.LayoutBounds.Top + margin, region.LayoutBounds.Width, region.LayoutBounds.Height);
                args.DrawingSession.DrawRectangle(rect, Colors.Green);
            }

            CanvasTextLayoutRegion r;
            if (caretIndex < text.Length)
            {
                layout.GetCaretPosition(caretIndex, false, out r);
                caretIndex = r.CharacterIndex + r.CharacterCount - 1;
                var rect = new Rect(r.LayoutBounds.Left, r.LayoutBounds.Top + margin, r.LayoutBounds.Width, r.LayoutBounds.Height);
                args.DrawingSession.DrawRectangle(rect, Colors.Gray, 4.0f);
            }

            if(tappedPoint != null)
            {
                //ヒットテストはマージンを考慮しないのであらかじめ引いておく
                float posx = (float)tappedPoint.Value.X, posy = (float)tappedPoint.Value.Y - margin;
                if(layout.HitText(posx, posy, out r))
                {
                    args.DrawingSession.DrawCircle(posx, posy + margin, 2, Colors.Green);
                    var rect = new Rect(r.LayoutBounds.Left, r.LayoutBounds.Top + margin, r.LayoutBounds.Width, r.LayoutBounds.Height);
                    args.DrawingSession.DrawRoundedRectangle(rect, 5, 5, Colors.Blue);
                }
                else
                {
                    tappedPoint = null;
                }
            }

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

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.tappedPoint = e.GetPosition(this.Canvas);
            this.Canvas.Invalidate();
        }
    }
}
