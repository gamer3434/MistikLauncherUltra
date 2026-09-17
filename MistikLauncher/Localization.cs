using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MistikLauncher;
public static class Localization
{
    static readonly Dictionary<string, Dictionary<string, string>> Catalogs = new();
    public static string Language { get; private set; } = "tr";
    public static event Action? Changed;
    static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(string), typeof(Localization));
    static readonly DependencyProperty RenderedProperty = DependencyProperty.RegisterAttached(
        "Rendered", typeof(string), typeof(Localization));
    static Localization()
    {
        foreach (var code in new[] { "tr", "en" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"MistikLauncher.Locales.{code}.json")!;
            Catalogs[code] = JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        }
    }
    public static string T(string key) => Catalogs[Language].GetValueOrDefault(key, key);
    public static void SetLanguage(string code)
    {
        Language = code == "en" || code == "English" ? "en" : "tr";
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Language == "tr" ? "tr-TR" : "en-US");
        Changed?.Invoke();
    }
    // Handles cached legacy pages without replacing running server controllers.
    public static void TranslateTree(DependencyObject node)
    {
        // Selection/item visuals are generated from data. Writing their text breaks WPF bindings
        // and leaves selectors displaying the first value even after a new choice is saved.
        if(node is ComboBox) return;
        string? current = node switch
        {
            TextBlock text => text.Text,
            ContentControl control when control is not ComboBoxItem && control.Content is string value => value,
            _ => null
        };
        if (current != null)
        {
            var previous = node.GetValue(RenderedProperty) as string;
            if (previous != current) node.SetValue(SourceProperty, current);
            var source = node.GetValue(SourceProperty) as string ?? current;
            var translated = T(source);
            if (node is TextBlock text) text.SetCurrentValue(TextBlock.TextProperty, translated);
            else if (node is ContentControl control) control.SetCurrentValue(ContentControl.ContentProperty, translated);
            node.SetValue(RenderedProperty, translated);
        }
        if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                TranslateTree(VisualTreeHelper.GetChild(node, i));
    }
}
