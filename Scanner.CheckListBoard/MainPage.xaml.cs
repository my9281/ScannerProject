using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Scanner.CheckListBoard;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        PorcelainBackground.Drawable = new PorcelainDrawable();
        string[] modules = ["入库检测", "安全检测", "裸机检测", "整机检测", "查看评级", "历史查看", "系统配置"];
        string[] subtitles = ["来料验收", "安全核验", "单机检查", "整机验证", "品质分级", "检测记录", "工作台设置"];
        for (var row = 0; row < 4; row++)
            ModuleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var index = 0; index < modules.Length; index++)
        {
            var title = modules[index];
            var card = new Border
            {
                Stroke = new SolidColorBrush(Color.FromArgb("#F9FDFF")), StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) },
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb("#F7FFFFFF"), 0),
                        new GradientStop(Color.FromArgb("#B8FFFFFF"), 0.48f),
                        new GradientStop(Color.FromArgb("#80D8EAF7"), 0.5f),
                        new GradientStop(Color.FromArgb("#DAFFFFFF"), 1)
                    }
                },
                Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#689ABB")), Offset = new Point(0, 6), Radius = 14, Opacity = 0.18f },
                HeightRequest = index == 6 ? 90 : 138
            };
            var content = new Grid { Padding = new Thickness(17, 14), InputTransparent = true };
            content.Add(new Label { Text = $"{index + 1:00}", FontSize = 26, TextColor = Color.FromArgb("#7DA8C8"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start });
            content.Add(new VerticalStackLayout
            {
                Spacing = 5, VerticalOptions = LayoutOptions.End,
                Children =
                {
                    new Label { Text = title, FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#224F75") },
                    new Label { Text = subtitles[index], FontSize = 12, TextColor = Color.FromArgb("#6587A2") }
                }
            });
            var layers = new Grid();
            layers.Add(content);
            // Native button retains keyboard focus, accessibility and touch feedback.
            var button = new Button { BackgroundColor = Colors.Transparent, Text = title, TextColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 22, Padding = 0 };
            SemanticProperties.SetDescription(button, title);
            button.Clicked += async (_, _) => await DisplayAlertAsync(title, "此功能页面待接入。", "返回工作台");
            layers.Add(button);
            card.Content = layers;
            ModuleGrid.Add(card, index % 2, index / 2);
            if (index == 6) { Grid.SetColumn(card, 0); Grid.SetColumnSpan(card, 2); }
        }
    }
}

internal sealed class PorcelainDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Color.FromArgb("#E5F2FA");
        canvas.FillRectangle(dirtyRect);
        canvas.StrokeColor = Color.FromArgb("#88ADCD");
        canvas.StrokeSize = 1.3f;
        canvas.Alpha = 0.26f;
        DrawFlower(canvas, dirtyRect.Width - 30, 95, 92);
        DrawFlower(canvas, 10, dirtyRect.Height - 100, 120);
        canvas.Alpha = 0.16f;
        for (var i = 0; i < 7; i++)
        {
            var y = 160 + i * 85f;
            var vine = new PathF();
            vine.MoveTo(dirtyRect.Width - 14, y - 60);
            vine.CurveTo(dirtyRect.Width - 100, y - 20, dirtyRect.Width + 30, y + 30, dirtyRect.Width - 30, y + 80);
            canvas.DrawPath(vine);
            DrawFlower(canvas, dirtyRect.Width - 26, y, 25);
        }
        canvas.Alpha = 1;
    }

    private static void DrawFlower(ICanvas canvas, float x, float y, float radius)
    {
        canvas.SaveState();
        canvas.Translate(x, y);
        for (var i = 0; i < 8; i++)
        {
            canvas.Rotate(45);
            var petal = new PathF();
            petal.MoveTo(0, 0);
            petal.CurveTo(-radius * 0.55f, -radius * 0.4f, -radius * 0.35f, -radius * 0.85f, 0, -radius);
            petal.CurveTo(radius * 0.35f, -radius * 0.85f, radius * 0.55f, -radius * 0.4f, 0, 0);
            canvas.DrawPath(petal);
            canvas.DrawLine(0, -radius * 0.18f, 0, -radius * 0.75f);
        }
        canvas.DrawCircle(0, 0, radius * 0.13f);
        canvas.DrawCircle(0, 0, radius * 0.2f);
        canvas.RestoreState();
    }
}
