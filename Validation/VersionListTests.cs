using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MistikLauncher;
using MistikLauncher.Pages;
using Newtonsoft.Json.Linq;
using Localization = MistikLauncher.Localization;

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
        var previousVersion=window.Config.Version;
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
            var snapshotFilter=Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,"Snapshot"));
            var allFilter=Nodes(page).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("Hepsi")));
            Check(((System.Windows.Media.SolidColorBrush)snapshotFilter.Background).Color==PageHelpers.HexBrush("#00A3FF").Color && ((System.Windows.Media.SolidColorBrush)allFilter.Background).Color==PageHelpers.HexBrush("#333333").Color && ReferenceEquals(allFilter.Foreground,System.Windows.Media.Brushes.White),"version filter highlight follows selected results with readable inactive text");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==80,"show more adds one forty-version batch");
            More()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Versions().Length==95 && Versions().Distinct().Count()==95 && More()==null,"all catalog versions remain reachable without duplicates");
            Filter("Vanilla"); Filter("Snapshot");
            Check(Versions().Length==40 && More()!=null,"changing filters resets expanded version list");
            Localization.TranslateTree(page);
            Check(Equals(More()!.Content,Localization.T("versionLoadMore")),"version batch action uses selected locale");
            foreach(var id in new[]{"batch-selection-first","batch-selection-second"})
            {
                var directory=System.IO.Path.Combine(App.GameDir,"versions",id); System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".json"),new JObject { ["id"]=id }.ToString());
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,id+".jar"),"fixture");
            }
            window.Config.Version="batch-selection-first"; Filter("Vanilla");
            Button Selection(string id)=>Nodes(cards.Children.OfType<Border>().Single(card=>Nodes(card).OfType<TextBlock>().Any(text=>text.Text==id))).OfType<Button>().Single(button=>Equals(button.Content,Localization.T("SEC")) || Equals(button.Content,Localization.T("SECILDI")));
            Check(!Selection("batch-selection-first").IsEnabled && Selection("batch-selection-second").IsEnabled,"version cards identify current installed profile");
            window.Config.Version="batch-selection-second"; page.RefreshSelection();
            Check(Selection("batch-selection-first").IsEnabled && !Selection("batch-selection-second").IsEnabled,"visible version selection refreshes after global profile change");
            window.Config.Version="batch-selection-first"; page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(!Selection("batch-selection-first").IsEnabled && Selection("batch-selection-second").IsEnabled,"cached version page refreshes stale selection on load");
            var unchanged=cards.Children[0]; page.RefreshSelection();
            Check(ReferenceEquals(unchanged,cards.Children[0]),"unchanged version selection retains cached cards");
        }
        finally { catalog.SetValue(null,previous); window.Config.Version=previousVersion; }
        return checks;
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
}
