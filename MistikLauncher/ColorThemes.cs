using System.Windows.Media;
using System.Windows;
using System.Windows.Data;
namespace MistikLauncher;
public static class ColorThemes
{
    public static readonly string[] Names={"Red","Purple","Blue","Green","Orange","Teal","Rose","Amber","NeonGreen","Cyan","Magenta","Lava","Indigo","Sunburst","Gold","Platinum","Nebula","Pink","Diamond","Obsidian"};
    public static string Preview(string name) => name switch {
        "Red"=>"#DB1633", "Purple"=>"#8045E5", "Blue"=>"#2986E0", "Green"=>"#00BA60",
        "Orange"=>"#EF730B", "Teal"=>"#00A99C", "Rose"=>"#D72C76", "Amber"=>"#FFB000",
        "NeonGreen"=>"#20B68B", "Cyan"=>"#159BB1", "Magenta"=>"#B046C5", "Lava"=>"#D6532C",
        "Indigo"=>"#7550CE", "Sunburst"=>"#E69024", "Gold"=>"#C89529", "Platinum"=>"#B5A2CD",
        "Nebula"=>"#AA4DCF", "Pink"=>"#D16B9B", "Diamond"=>"#62ADD0", "Obsidian"=>"#B9A345", _=>"#FFB000"
    };
    public static Brush ActionText => Brush("#ACTIONTEXT");
    static readonly Dictionary<string,SolidColorBrush> BrushesBySource=new(StringComparer.OrdinalIgnoreCase);
    sealed class ThemeColor : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(Color),typeof(ThemeColor));
        public Color Value { get=>(Color)GetValue(ValueProperty); set=>SetValue(ValueProperty,value); }
    }
    static readonly Dictionary<string,ThemeColor> ColorsBySource=new(StringComparer.OrdinalIgnoreCase);
    public static string Accent { get; private set; }="#377ACB";
    public static string Selected { get; private set; }="Blue";
    public static IEnumerable<KeyValuePair<string,SolidColorBrush>> Resources => Sources.Select(s=>new KeyValuePair<string,SolidColorBrush>("Theme"+s.TrimStart('#'),Brush(s)));
    static readonly string[] Sources={"#0D1727","#192C46","#203853","#365574","#152A43","#274565","#00A3FF","#226DA0","#77D8D0","#3486A9","#245882"};
    public static SolidColorBrush Brush(string source)
    {
        if(!BrushesBySource.TryGetValue(source,out var brush)) {
            var value=new ThemeColor { Value=Map(source) }; ColorsBySource[source]=value;
            brush=new SolidColorBrush();
            BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(ThemeColor.Value)) { Source=value });
            BrushesBySource[source]=brush;
        }
        return brush;
    }
    static Color Map(string source)
    {
        var accent=(Color)ColorConverter.ConvertFromString(Preview(Selected));
        Color Shade(double amount)=>Color.FromRgb((byte)(accent.R*amount),(byte)(accent.G*amount),(byte)(accent.B*amount));
        if(source=="#ACTIONTEXT") {
            double Linear(byte channel) { double value=channel/255.0; return value<=0.04045?value/12.92:Math.Pow((value+0.055)/1.055,2.4); }
            double luminance=Linear(accent.R)*0.2126+Linear(accent.G)*0.7152+Linear(accent.B)*0.0722;
            return luminance>0.179?Colors.Black:Colors.White;
        }
        if(source=="#274565") return Shade(0.19);
        if(source=="#00A3FF" || source=="#226DA0") return accent;
        if(source=="#77D8D0") return accent;
        if(source=="#3486A9") return Shade(0.65);
        if(source=="#245882") return Shade(0.38);
        var palette=new[]{"#0C0C0E","#1C1C1F","#242427","#36363C","#161619","#242427","#FFB000","#FFB000","#A96B10","#674512"};
        int index=Array.IndexOf(Sources,source.ToUpperInvariant());
        int slot=index switch { 7=>6,8=>7,9=>8,10=>9,_=>index };
        return (Color)ColorConverter.ConvertFromString(slot>=0?palette[slot]:source);
    }
    public static void Apply(string name)
    {
        Selected=Names.Contains(name)?name:"Amber";
        foreach(var item in ColorsBySource) item.Value.Value=Map(item.Key);
        var accent=Map("#00A3FF");
        Accent=$"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
    }
    public static bool IsThemed(string hex)=>Sources.Contains(hex.ToUpperInvariant());
}
