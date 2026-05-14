using lulz;
using Terminal.Gui;

Application.Init();

try
{
    // Classic Turbo Vision desktop: cyan background, gray text
    var turboVisionColors = new ColorScheme
    {
        Normal    = Application.Driver.MakeAttribute(Color.Gray,        Color.Cyan),
        Focus     = Application.Driver.MakeAttribute(Color.Black,       Color.Green),
        HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Cyan),
        HotFocus  = Application.Driver.MakeAttribute(Color.Black,       Color.Green)
    };
    Application.Top.ColorScheme = turboVisionColors;
    Application.Run(new MyView());
}
finally
{
    Application.Shutdown();
}