using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;

static class VersionListTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var blue=PageHelpers.MkBtn("Blue","#226DA0"); var red=PageHelpers.MkBtn("Red","#FF4B4B");
        Check(ReferenceEquals(blue.Template,red.Template) && ((System.Windows.Media.SolidColorBrush)blue.Background).Color!=((System.Windows.Media.SolidColorBrush)red.Background).Color,"buttons reuse rounded template while retaining individual backgrounds");
        var catalog=typeof(VersionManagerPage).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(field=>field.FieldType==typeof(JArray));
        var previous=catalog.GetValue(null);
        try
        {
            catalog.SetValue(null,new JArray(Enumerable.Range(1,95).Select(index=>new JObject { ["id"]="batch-snapshot-"+index,["type"]="snapshot" })));
            var page=new VersionManagerPage(window);
            var cards=Nodes(page).OfType<StackPanel>().Single(panel=>panel.Name=="VersionListPanel");
            Button? More()=>Nodes(page).OfType<Button>().SingleOrDefault(button=>button.Name=="VersionLoadMore");
            string[] Versions()=>Nodes(page).OfType<TextBlock>().Select(text=>text.Text).Where(text=>text.StartsWith("batch-snapshot-",StringComparison.Ordinal)).ToArray();
            void Filter(string title)=>Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,title)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(More()!=null && cards.Children.OfType<Border>().Count()==40,"large version catalog initially creates forty cards across all loaders");
            Filter("Snapshot");
            Check(Versions().Length==40,"snapshot filter resets version batch to forty");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==80,"show more adds one forty-version batch");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==95 && Versions().Distinct().Count()==95 && More()==null,"all catalog versions remain reachable without duplicates");
            Filter("Vanilla"); Filter("Snapshot");
            Check(Versions().Length==40 && More()!=null,"changing filters resets expanded version list");
            Localization.TranslateTree(page);
            Check(Equals(More()!.Content,Localization.T("versionLoadMore")),"version batch action uses selected locale");
        }
        finally { catalog.SetValue(null,previous); }
        return checks;
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
}
