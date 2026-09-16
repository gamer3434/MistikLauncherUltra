using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
namespace MistikLauncher;
public partial class MainWindow
{
    public static readonly string[] WindowButtonStyles={"MacOS","Windows","Minimal","Neon","Retro","Glass","Terminal","Pill","Cyberpunk","Balloon","Aurora","Diamond","NeonOutline","FrostedGlass","Samurai","Hologram"};
    public void SetWindowButtons(string style)
    {
        Config.WindowButtons=WindowButtonStyles.Contains(style)?style:"MacOS";
        ConfigManager.Save(Config); ApplyWindowAppearance();
    }
    public void ApplyWindowAppearance()
    {
        string style=WindowButtonStyles.Contains(Config.WindowButtons)?Config.WindowButtons:"MacOS";
        bool native=style=="Windows";
        CaptionHost.Visibility=native?Visibility.Collapsed:Visibility.Visible;
        WindowChrome.SetWindowChrome(this,native?null:new WindowChrome { CaptionHeight=42,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false });
        WindowStyle=native?WindowStyle.SingleBorderWindow:WindowStyle.None;
        DockPanel.SetDock(CaptionButtons,style=="MacOS"?Dock.Left:Dock.Right);
        WindowChrome.SetIsHitTestVisibleInChrome(CaptionButtons,true);
        CaptionButtons.Children.Clear();
        var actions=style=="MacOS"?new[]{"close","minimize","maximize"}:new[]{"minimize","maximize","close"};
        foreach(string action in actions)
        {
            var accent=ColorThemes.Brush("#00A3FF").Color;
            var color=style=="MacOS" ? action switch { "close"=>Color.FromRgb(255,95,87),"minimize"=>Color.FromRgb(255,189,46),_=>Color.FromRgb(40,201,64) } : accent;
            bool round=new[]{"MacOS","Minimal","Neon","Balloon","Aurora","FrostedGlass","Hologram"}.Contains(style);
            bool outline=new[]{"Minimal","Terminal","Cyberpunk","NeonOutline","Samurai"}.Contains(style);
            double width=round?24:38;
            var fill=new SolidColorBrush(color);
            Brush background=outline?Brushes.Transparent:style is "Glass" or "FrostedGlass"?new SolidColorBrush(Color.FromArgb(45,color.R,color.G,color.B)):fill;
            if(style is "Aurora" or "Hologram" or "Pill" or "Balloon") background=new LinearGradientBrush(color,Color.FromRgb((byte)(color.R*0.35),(byte)(color.G*0.35),(byte)(color.B*0.35)),45);
            var frame=new Border { Background=background,BorderBrush=fill,BorderThickness=new Thickness(outline?1:0),CornerRadius=new CornerRadius(round?12:style=="Pill"?10:style=="Retro"?0:5),Width=width,Height=24 };
            string glyph=style=="Diamond"?"◆":action switch { "close"=>"×","minimize"=>"−",_=>WindowState==WindowState.Maximized?"❐":"□" };
            frame.Child=new TextBlock { Text=glyph,FontSize=15,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=outline || style is "Glass" or "FrostedGlass"?fill:style=="MacOS"?Brushes.Black:ColorThemes.ActionText };
            if(style is "Neon" or "NeonOutline" or "Hologram") frame.Effect=new System.Windows.Media.Effects.DropShadowEffect { Color=color,BlurRadius=8,ShadowDepth=0,Opacity=0.45 };
            var button=new Button { Content=frame,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(5),MinHeight=36,MinWidth=36,ToolTip=Localization.T("window"+action),Cursor=System.Windows.Input.Cursors.Hand };
            button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Name='focus' Padding='{TemplateBinding Padding}' BorderBrush='Transparent' BorderThickness='1' CornerRadius='5'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='White'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='focus' Property='Background' Value='#303035'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            System.Windows.Automation.AutomationProperties.SetName(button,Localization.T("window"+action));
            button.Click+=(_,_)=> {
                if(action=="close") Close();
                else if(action=="minimize") WindowState=WindowState.Minimized;
                else { WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized; ApplyWindowAppearance(); }
            };
            CaptionButtons.Children.Add(button);
        }
    }
}
