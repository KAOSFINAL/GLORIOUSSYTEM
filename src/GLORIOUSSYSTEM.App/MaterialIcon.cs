using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace GLORIOUSSYSTEM.App;

public enum MaterialIconKind
{
    Home,
    Camera,
    Analytics,
    Settings,
    Logout,
    CheckCircle,
    Water,
    Environment,
    Flow,
    Solar,
    Battery,
    Scan
}

/// <summary>
/// Small theme-aware vector icon used instead of fixed-color image assets.
/// It follows the rounded, two-pixel stroke language used by Material 3 icons
/// and remains crisp on every MAUI target and display density.
/// </summary>
public sealed class MaterialIcon : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(MaterialIconKind), typeof(MaterialIcon), MaterialIconKind.Home,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(MaterialIcon), Colors.Black,
        propertyChanged: OnVisualPropertyChanged);

    public MaterialIconKind Kind
    {
        get => (MaterialIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public MaterialIcon()
    {
        Drawable = new MaterialIconDrawable(this);
        InputTransparent = true;
    }

    static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MaterialIcon icon)
            icon.Invalidate();
    }

    sealed class MaterialIconDrawable(MaterialIcon owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var scale = Math.Min(dirtyRect.Width, dirtyRect.Height) / 24f;
            if (scale <= 0)
                return;

            var offsetX = dirtyRect.Left + (dirtyRect.Width - 24f * scale) / 2f;
            var offsetY = dirtyRect.Top + (dirtyRect.Height - 24f * scale) / 2f;

            canvas.SaveState();
            canvas.Translate(offsetX, offsetY);
            canvas.Scale(scale, scale);
            canvas.StrokeColor = owner.IconColor;
            canvas.FillColor = owner.IconColor;
            canvas.StrokeSize = 1.9f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            switch (owner.Kind)
            {
                case MaterialIconKind.Home:
                    DrawHome(canvas);
                    break;
                case MaterialIconKind.Camera:
                    DrawCamera(canvas);
                    break;
                case MaterialIconKind.Analytics:
                    DrawAnalytics(canvas);
                    break;
                case MaterialIconKind.Settings:
                    DrawSettings(canvas);
                    break;
                case MaterialIconKind.Logout:
                    DrawLogout(canvas);
                    break;
                case MaterialIconKind.CheckCircle:
                    DrawCheckCircle(canvas);
                    break;
                case MaterialIconKind.Water:
                    DrawWater(canvas);
                    break;
                case MaterialIconKind.Environment:
                    DrawEnvironment(canvas);
                    break;
                case MaterialIconKind.Flow:
                    DrawFlow(canvas);
                    break;
                case MaterialIconKind.Solar:
                    DrawSolar(canvas);
                    break;
                case MaterialIconKind.Battery:
                    DrawBattery(canvas);
                    break;
                case MaterialIconKind.Scan:
                    DrawScan(canvas);
                    break;
            }

            canvas.RestoreState();
        }

        static void DrawHome(ICanvas canvas)
        {
            var path = new PathF();
            path.MoveTo(3.5f, 10.5f);
            path.LineTo(12f, 3.5f);
            path.LineTo(20.5f, 10.5f);
            path.LineTo(20.5f, 20f);
            path.LineTo(14.8f, 20f);
            path.LineTo(14.8f, 14.2f);
            path.LineTo(9.2f, 14.2f);
            path.LineTo(9.2f, 20f);
            path.LineTo(3.5f, 20f);
            path.Close();
            canvas.DrawPath(path);
        }

        static void DrawCamera(ICanvas canvas)
        {
            canvas.DrawRoundedRectangle(3f, 6.5f, 18f, 13.5f, 2.5f);
            canvas.DrawCircle(12f, 13.2f, 3.3f);
            var top = new PathF();
            top.MoveTo(7.5f, 6.5f);
            top.LineTo(9.2f, 4f);
            top.LineTo(14.8f, 4f);
            top.LineTo(16.5f, 6.5f);
            canvas.DrawPath(top);
        }

        static void DrawAnalytics(ICanvas canvas)
        {
            canvas.DrawLine(4f, 20f, 20f, 20f);
            canvas.DrawRoundedRectangle(5f, 11f, 2.8f, 7f, 1.2f);
            canvas.DrawRoundedRectangle(10.6f, 5f, 2.8f, 13f, 1.2f);
            canvas.DrawRoundedRectangle(16.2f, 8f, 2.8f, 10f, 1.2f);
        }

        static void DrawSettings(ICanvas canvas)
        {
            canvas.DrawCircle(12f, 12f, 6.8f);
            canvas.DrawCircle(12f, 12f, 2.7f);
            for (var i = 0; i < 8; i++)
            {
                var angle = MathF.PI * i / 4f;
                canvas.DrawLine(
                    12f + MathF.Cos(angle) * 8.3f,
                    12f + MathF.Sin(angle) * 8.3f,
                    12f + MathF.Cos(angle) * 10f,
                    12f + MathF.Sin(angle) * 10f);
            }
        }

        static void DrawLogout(ICanvas canvas)
        {
            canvas.DrawRoundedRectangle(3.5f, 3.5f, 9.5f, 17f, 2f);
            canvas.DrawLine(10f, 12f, 20.5f, 12f);
            canvas.DrawLine(16.5f, 8f, 20.5f, 12f);
            canvas.DrawLine(20.5f, 12f, 16.5f, 16f);
        }

        static void DrawCheckCircle(ICanvas canvas)
        {
            canvas.DrawCircle(12f, 12f, 9f);
            canvas.DrawLine(7.5f, 12.2f, 10.5f, 15.2f);
            canvas.DrawLine(10.5f, 15.2f, 16.8f, 8.8f);
        }

        static void DrawWater(ICanvas canvas)
        {
            var drop = new PathF();
            drop.MoveTo(12f, 2.7f);
            drop.CurveTo(10.2f, 6.1f, 6.2f, 10.2f, 6.2f, 14.2f);
            drop.CurveTo(6.2f, 17.8f, 8.8f, 20.8f, 12f, 20.8f);
            drop.CurveTo(15.2f, 20.8f, 17.8f, 17.8f, 17.8f, 14.2f);
            drop.CurveTo(17.8f, 10.2f, 13.8f, 6.1f, 12f, 2.7f);
            drop.Close();
            canvas.DrawPath(drop);
            canvas.DrawLine(9.1f, 15.3f, 10.2f, 16.4f);
        }

        static void DrawEnvironment(ICanvas canvas)
        {
            canvas.DrawRoundedRectangle(9f, 3f, 6f, 13f, 3f);
            canvas.DrawCircle(12f, 16.8f, 4.1f);
            canvas.DrawLine(12f, 7f, 12f, 16.5f);
        }

        static void DrawFlow(ICanvas canvas)
        {
            canvas.DrawLine(3f, 7f, 15.5f, 7f);
            canvas.DrawLine(15.5f, 7f, 18f, 5f);
            canvas.DrawLine(3f, 12f, 20.5f, 12f);
            canvas.DrawLine(3f, 17f, 14f, 17f);
            canvas.DrawLine(14f, 17f, 16.5f, 19f);
        }

        static void DrawSolar(ICanvas canvas)
        {
            canvas.DrawCircle(12f, 12f, 4.2f);
            for (var i = 0; i < 8; i++)
            {
                var angle = MathF.PI * i / 4f;
                canvas.DrawLine(
                    12f + MathF.Cos(angle) * 7f,
                    12f + MathF.Sin(angle) * 7f,
                    12f + MathF.Cos(angle) * 9.8f,
                    12f + MathF.Sin(angle) * 9.8f);
            }
        }

        static void DrawBattery(ICanvas canvas)
        {
            canvas.DrawRoundedRectangle(3f, 6.5f, 16f, 11f, 2.2f);
            canvas.DrawLine(21f, 10f, 21f, 14f);
            canvas.FillRoundedRectangle(6f, 9.5f, 7f, 5f, 1.2f);
        }

        static void DrawScan(ICanvas canvas)
        {
            canvas.DrawLine(4f, 9f, 4f, 4f);
            canvas.DrawLine(4f, 4f, 9f, 4f);
            canvas.DrawLine(15f, 4f, 20f, 4f);
            canvas.DrawLine(20f, 4f, 20f, 9f);
            canvas.DrawLine(20f, 15f, 20f, 20f);
            canvas.DrawLine(20f, 20f, 15f, 20f);
            canvas.DrawLine(9f, 20f, 4f, 20f);
            canvas.DrawLine(4f, 20f, 4f, 15f);
            var leaf = new PathF();
            leaf.MoveTo(8f, 15.5f);
            leaf.CurveTo(8.6f, 10.2f, 12.2f, 8f, 16.8f, 8.2f);
            leaf.CurveTo(16.3f, 13f, 13.4f, 15.8f, 8f, 15.5f);
            canvas.DrawPath(leaf);
            canvas.DrawLine(9.5f, 14.2f, 15.3f, 9.7f);
        }
    }
}
