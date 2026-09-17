using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Runtime.InteropServices;
namespace MistikLauncher;
public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { public NativePoint Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    static IntPtr ConstrainMaximizedWindow(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        // Native pixel coordinates keep the taskbar visible on the current monitor, including mixed DPI screens.
        if(message!=0x0024) return IntPtr.Zero;
        var info=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
        if(!GetMonitorInfo(MonitorFromWindow(window,2),ref info)) return IntPtr.Zero;
        var bounds=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxPosition.X=info.Work.Left-info.Monitor.Left;
        bounds.MaxPosition.Y=info.Work.Top-info.Monitor.Top;
        bounds.MaxSize.X=info.Work.Right-info.Work.Left;
        bounds.MaxSize.Y=info.Work.Bottom-info.Work.Top;
        Marshal.StructureToPtr(bounds,lParam,false);
        handled=true;
        return IntPtr.Zero;
    }
    public static readonly string[] WindowButtonStyles={"MacOS","Windows","Minimal","Neon","Retro","Glass","Terminal","Pill","Cyberpunk","Balloon","Aurora","Diamond","NeonOutline","FrostedGlass","Samurai","Hologram"};
    public void SetWindowButtons(string style)
    {
        Config.WindowButtons=WindowButtonStyles.Contains(style)?style:"MacOS";
        ConfigManager.Save(Config); ApplyWindowAppearance();
    }
    public void ApplyWindowAppearance()
    {
        string style=WindowButtonStyles.Contains(Config.WindowButtons)?Config.WindowButtons:"MacOS";
        CaptionHost.Visibility=Visibility.Visible;
        WindowSurface.Margin=WindowState==WindowState.Maximized?new Thickness(6):new Thickness(0);
        WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight=42,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false });
        if(WindowStyle!=WindowStyle.None) WindowStyle=WindowStyle.None;
        DockPanel.SetDock(CaptionButtons,style=="MacOS"?Dock.Left:Dock.Right);
        WindowChrome.SetIsHitTestVisibleInChrome(CaptionButtons,true);
        CaptionButtons.Children.Clear();
        var actions=style=="MacOS"?new[]{"close","minimize","maximize"}:new[]{"minimize","maximize","close"};
        foreach(string action in actions)
        {
            var frame=WindowButtonPreview(style,action,WindowState==WindowState.Maximized);
            // Caption lighting belongs exclusively to close, regardless of the selected button style.
            frame.Effect=null;
            if(action=="close" && Config.CloseLighting!="Off") {
                var color=Config.CloseLighting=="RGB"?(Color)ColorConverter.ConvertFromString(Config.CloseRgb):ColorThemes.Brush("#00A3FF").Color;
                frame.Width=28; frame.Height=28; frame.CornerRadius=new CornerRadius(14);
                frame.BorderThickness=new Thickness(2); frame.BorderBrush=new SolidColorBrush(color);
                frame.Background=Brushes.Transparent;
                ((TextBlock)frame.Child).Foreground=Brushes.White;
                if(Config.CloseLighting=="Rainbow") {
                    var rotation=new RotateTransform(0,0.5,0.5);
                    var rainbow=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,1),RelativeTransform=rotation };
                    var colors=new[]{Colors.Red,Colors.Orange,Colors.Yellow,Colors.Lime,Colors.Cyan,Colors.Blue,Colors.Magenta,Colors.Red};
                    for(int i=0;i<colors.Length;i++) rainbow.GradientStops.Add(new GradientStop(colors[i],i/(double)(colors.Length-1)));
                    frame.BorderBrush=rainbow;
                    if(SystemParameters.ClientAreaAnimation) rotation.BeginAnimation(RotateTransform.AngleProperty,new System.Windows.Media.Animation.DoubleAnimation(0,360,TimeSpan.FromSeconds(5)) { RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever });
                }
                frame.Effect=new System.Windows.Media.Effects.DropShadowEffect { Color=color,BlurRadius=9,ShadowDepth=0,Opacity=0.6 };
            }
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
    public static Border WindowButtonPreview(string style,string action,bool maximized=false)
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
            string glyph=style=="Diamond"?"◆":action switch { "close"=>"×","minimize"=>"−",_=>maximized?"❐":"□" };
            frame.Child=new TextBlock { Text=glyph,FontSize=15,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=outline || style is "Glass" or "FrostedGlass"?fill:style=="MacOS"?Brushes.Black:ColorThemes.ActionText };
            if(style is "Neon" or "NeonOutline" or "Hologram") frame.Effect=new System.Windows.Media.Effects.DropShadowEffect { Color=color,BlurRadius=8,ShadowDepth=0,Opacity=0.45 };
            if(style=="Windows") { frame.Background=Brushes.Transparent; frame.BorderBrush=Brushes.DimGray; frame.BorderThickness=new Thickness(1); frame.CornerRadius=new CornerRadius(0); ((TextBlock)frame.Child).Foreground=Brushes.White; }
        return frame;
    }

}
