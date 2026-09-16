using System.Windows.Media;
using System.Windows;
using System.Windows.Data;
namespace MistikLauncher;
public static class ColorThemes
{
    public static readonly string[] Names={"Blue","Green","Purple","Orange","Red"};
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
        var palette=Selected switch {
            "Green"=>new[]{"#0D1C1A","#18332E","#23473E","#3B695D","#142C26","#254D40","#237A59","#7EE5BA","#359B75","#236348"},
            "Purple"=>new[]{"#171222","#2B2140","#3B2F55","#62507D","#241B37","#423057","#7950B7","#CAB0F5","#9470CD","#644292"},
            "Orange"=>new[]{"#20170F","#38291D","#4C3928","#765A40","#302116","#513822","#A65B26","#F9C48C","#C88C4E","#925C2D"},
            "Red"=>new[]{"#211319","#3D232F","#522F3E","#795061","#321C27","#522A3A","#B43F60","#F6ADC2","#C96382","#934361"},
            _=>new[]{"#0D1727","#192C46","#203853","#365574","#152A43","#274565","#377ACB","#77D8D0","#3486A9","#245882"}
        };
        int index=Array.IndexOf(Sources,source.ToUpperInvariant());
        int slot=index switch { 7=>6,8=>7,9=>8,10=>9,_=>index };
        return (Color)ColorConverter.ConvertFromString(slot>=0?palette[slot]:source);
    }
    public static void Apply(string name)
    {
        Selected=Names.Contains(name)?name:"Blue";
        foreach(var item in ColorsBySource) item.Value.Value=Map(item.Key);
        var accent=Map("#00A3FF");
        Accent=$"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
    }
    public static bool IsThemed(string hex)=>Sources.Contains(hex.ToUpperInvariant());
}
