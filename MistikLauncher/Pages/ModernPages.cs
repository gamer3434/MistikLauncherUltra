using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MistikLauncher.Pages;

public class ModernHomePage : Page
{
    readonly MainWindow main;
    public ModernHomePage(MainWindow window)
    {
        main = window;
        Loaded += (_, _) => { Localization.Changed += Render; Render(); };
        Unloaded += (_, _) => Localization.Changed -= Render;
        Render();
    }
    void Render()
    {
        var stack = new StackPanel { Margin = new Thickness(36) };
        var hero = new Border { Background = PageHelpers.HexBrush("#203F5D"), CornerRadius = new CornerRadius(18), Padding = new Thickness(28) };
        var copy = new StackPanel();
        copy.Children.Add(PageHelpers.Lbl(Localization.T("welcome"), 30, "#FFFFFF", true, wrap: TextWrapping.Wrap));
        copy.Children.Add(PageHelpers.Lbl(Localization.T("intro"), 15, "#D5E5F2", pad: new Thickness(0,12,0,0), wrap: TextWrapping.Wrap));
        hero.Child = copy; stack.Children.Add(hero);
        var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in new[] { ("profile",main.Config.User), ("version",main.Config.Version), ("memory",$"{main.Config.Ram} GB") })
        {
            var section = new StackPanel { Margin = new Thickness(0,20,16,20) };
            section.Children.Add(PageHelpers.Lbl(Localization.T(item.Item1),14,"#BDCAD8"));
            section.Children.Add(PageHelpers.Lbl(item.Item2,22,"#FFFFFF",true,wrap:TextWrapping.Wrap));
            row.Children.Add(section);
        }
        stack.Children.Add(row);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("quick"),20,"#FFFFFF",true));
        var actions = new WrapPanel { Margin = new Thickness(0,12,0,18) };
        foreach (var item in new[] { ("versions","Vers"),("mods","Mods"),("server","Server"),("settings","Settings") })
        {
            var button = PageHelpers.MkBtn(Localization.T(item.Item1),"#28445E");
            button.Margin = new Thickness(0,0,12,12); button.Click += (_,_) => main.Navigate(item.Item2); actions.Children.Add(button);
        }
        stack.Children.Add(actions);
        var tools = new WrapPanel();
        foreach (var item in new[] { ("files",App.GameDir),("logs",App.LogFile) })
        {
            var button = PageHelpers.MkBtn(Localization.T(item.Item1),"#28445E"); button.Margin = new Thickness(0,0,12,12);
            button.Click += (_,_) => {
                try {
                    System.IO.Directory.CreateDirectory(App.GameDir);
                    if (item.Item1 == "logs" && !System.IO.File.Exists(App.LogFile)) App.Log("Launcher ready");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Item2) { UseShellExecute = true });
                } catch (Exception ex) { MessageBox.Show(ex.Message,Localization.T("error")); }
            }; tools.Children.Add(button);
        }
        stack.Children.Add(tools);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("help"),20,"#FFFFFF",true,pad:new Thickness(0,20,0,10)));
        stack.Children.Add(PageHelpers.Lbl(Localization.T("helpText"),15,"#BDCAD8",wrap:TextWrapping.Wrap));
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}

public class ModernSettingsPage : Page
{
    readonly MainWindow main;
    TextBox user = null!, ram = null!;
    ComboBox language = null!, accent = null!, provider = null!;
    CheckBox close = null!;
    TextBlock result = null!;
    public ModernSettingsPage(MainWindow window)
    {
        main=window;
        Loaded += (_,_) => { Localization.Changed += Render; Render(); };
        Unloaded += (_,_) => Localization.Changed -= Render;
        Render();
    }
    void Render()
    {
        var stack = new StackPanel { Margin = new Thickness(36), MaxWidth=680, HorizontalAlignment=HorizontalAlignment.Left };
        stack.Children.Add(PageHelpers.Lbl(Localization.T("settings"),28,"#FFFFFF",true));
        stack.Children.Add(PageHelpers.Lbl(Localization.T("settingsIntro"),15,"#BDCAD8",pad:new Thickness(0,8,0,18),wrap:TextWrapping.Wrap));
        void Label(string key) => stack.Children.Add(PageHelpers.Lbl(Localization.T(key),15,"#FFFFFF",true,pad:new Thickness(0,16,0,6)));
        Label("username"); user=PageHelpers.DarkTextBox(main.Config.User); user.MaxLength=16; stack.Children.Add(user);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("usernameHelp"),13,"#BDCAD8",wrap:TextWrapping.Wrap));
        Label("memory"); ram=PageHelpers.DarkTextBox(main.Config.Ram.ToString()); stack.Children.Add(ram);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("ramHelp"),13,"#BDCAD8",wrap:TextWrapping.Wrap));
        Label("language"); language=new ComboBox { ItemsSource=new[] { "Türkçe", "English" }, SelectedIndex=Localization.Language=="en"?1:0, MinHeight=36 }; stack.Children.Add(language);
        Label("accent"); accent=new ComboBox { ItemsSource=new[] { Localization.T("Blue"), Localization.T("Green"), Localization.T("Purple"), Localization.T("Orange"), Localization.T("Red") }, SelectedIndex=Array.IndexOf(new[] { "Blue", "Green", "Purple", "Orange", "Red" },main.Config.Accent), MinHeight=36 }; stack.Children.Add(accent);
        Label("auth"); provider=new ComboBox { ItemsSource=new[] { Localization.T("offline"), "Ely.by" }, SelectedIndex=main.Config.AuthType=="elyby"?1:0, MinHeight=36 }; stack.Children.Add(provider);
        close=new CheckBox { Content=Localization.T("close"), IsChecked=main.Config.AutoClose, Foreground=Brushes.White, Margin=new Thickness(0,20,0,20) }; stack.Children.Add(close);
        var save=PageHelpers.MkBtn(Localization.T("save"),"#226DA0"); save.HorizontalAlignment=HorizontalAlignment.Left;
        save.Click += (_,_) => Save(); stack.Children.Add(save);
        result=PageHelpers.Lbl("",14,"#F0CF84",pad:new Thickness(0,10,0,0),wrap:TextWrapping.Wrap); stack.Children.Add(result);
        Label("privacy"); stack.Children.Add(PageHelpers.Lbl(Localization.T("privacyText"),14,"#BDCAD8",wrap:TextWrapping.Wrap));
        Content=new ScrollViewer { Content=stack, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    }
    void Save()
    {
        if (!Regex.IsMatch(user.Text.Trim(), @"^[A-Za-z0-9_]{3,16}$") || !int.TryParse(ram.Text,out int memory) || memory<1 || memory>32)
        { result.Text=Localization.T("invalid"); return; }
        try {
            main.Config.User=user.Text.Trim(); main.Config.Ram=memory;
            main.Config.Accent=new[] { "Blue", "Green", "Purple", "Orange", "Red" }[Math.Max(0,accent.SelectedIndex)];
            main.Config.AuthType=provider.SelectedIndex==1?"elyby":"offline";
            main.Config.AutoClose=close.IsChecked==true;
            main.Config.Lang=language.SelectedIndex==1?"English":"Turkce";
            ConfigManager.Save(main.Config); main.ReloadConfig(); main.SwitchLanguage(main.Config.Lang);
            result.Text=Localization.T("saved");
        } catch(Exception ex) { result.Text=Localization.T("error")+": "+ex.Message; }
    }
}
