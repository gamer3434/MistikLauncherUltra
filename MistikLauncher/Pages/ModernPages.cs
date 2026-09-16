using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MistikLauncher.Pages;

public class ModernHomePage : Page, ILanguagePage
{
    readonly MainWindow main;
    public ModernHomePage(MainWindow window)
    {
        main = window;
        Loaded += (_, _) => { Localization.Changed += Render; Render(); };
        Unloaded += (_, _) => Localization.Changed -= Render;
        Render();
    }
    public void RefreshLanguage() => Render();
    void Render()
    {
        var stack = new StackPanel { Margin = new Thickness(36) };
        var hero = new Border { Background = new LinearGradientBrush(ColorThemes.Brush("#203853").Color,ColorThemes.Brush("#152A43").Color,0), CornerRadius = new CornerRadius(18), Padding = new Thickness(28) };
        var copy = new StackPanel();
        copy.Children.Add(PageHelpers.Lbl(Localization.T("welcome"), 30, "#FFFFFF", true, wrap: TextWrapping.Wrap));
        copy.Children.Add(PageHelpers.Lbl(Localization.T("intro"), 15, "#D5E5F2", pad: new Thickness(0,12,0,0), wrap: TextWrapping.Wrap));
        var heroGrid=new Grid(); heroGrid.ColumnDefinitions.Add(new ColumnDefinition()); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(200) });
        copy.Margin=new Thickness(0,0,24,0);
        var explore=PageHelpers.MkBtn(Localization.T("explore"),"#226DA0"); explore.Margin=new Thickness(0,20,0,0); explore.HorizontalAlignment=HorizontalAlignment.Left; explore.Click+=(_,_)=>main.Navigate("Vers"); copy.Children.Add(explore);
        var scene=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
        var blocks=new Canvas { Width=150,Height=98,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false };
        void Face(string points,string color) { var polygon=new System.Windows.Shapes.Polygon { Points=PointCollection.Parse(points),Fill=PageHelpers.HexBrush(color) }; blocks.Children.Add(polygon); }
        Face("75,8 135,38 75,68 15,38","#77D8D0"); Face("15,38 75,68 75,98 15,68","#3486A9"); Face("75,68 135,38 135,68 75,98","#245882");
        scene.Children.Add(blocks); scene.Children.Add(PageHelpers.Lbl(Localization.T("version"),12,"#BFD5EB",pad:new Thickness(0,16,0,4))); scene.Children.Add(PageHelpers.Lbl(main.Config.Version,17,"#FFFFFF",true,wrap:TextWrapping.Wrap));
        Grid.SetColumn(scene,1); heroGrid.Children.Add(copy); heroGrid.Children.Add(scene); hero.Child=heroGrid; stack.Children.Add(hero);
        var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in new[] { ("profile",main.Config.User), ("version",main.Config.Version), ("memory",$"{main.Config.Ram} GB") })
        {
            var section = new StackPanel { Margin = new Thickness(0,20,16,20) };
            section.Children.Add(PageHelpers.Lbl(Localization.T(item.Item1),14,"#BDCAD8"));
            section.Children.Add(PageHelpers.Lbl(item.Item2,22,"#FFFFFF",true,wrap:TextWrapping.Wrap));
            row.Children.Add(new Border { Child=section, Background=PageHelpers.HexBrush("#13253C"),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,0,0,0),Margin=new Thickness(0,18,12,24) });
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

public class ModernSettingsPage : Page, ILanguagePage
{
    readonly MainWindow main;
    TextBox user = null!, ram = null!;
    ComboBox language = null!, provider = null!;
    CheckBox close = null!;
    TextBlock result = null!, updateStatus = null!;
    Button updateButton = null!;
    public ModernSettingsPage(MainWindow window)
    {
        main=window;
        Loaded += (_,_) => { Localization.Changed += Render; main.LauncherUpdates.Changed += UpdateStatusAsync; Render(); };
        Unloaded += (_,_) => { Localization.Changed -= Render; main.LauncherUpdates.Changed -= UpdateStatusAsync; };
        Render();
    }
    public void RefreshLanguage() => Render();
    void Render()
    {
        var stack = new StackPanel { Margin = new Thickness(36), MaxWidth=680, HorizontalAlignment=HorizontalAlignment.Left };
        stack.Children.Add(PageHelpers.Lbl(Localization.T("settings"),28,"#FFFFFF",true));
        stack.Children.Add(PageHelpers.Lbl(Localization.T("settingsIntro"),15,"#BDCAD8",pad:new Thickness(0,8,0,18),wrap:TextWrapping.Wrap));
        var updateCard=new Border { Background=PageHelpers.HexBrush("#192C46"), CornerRadius=new CornerRadius(14), Padding=new Thickness(20), Margin=new Thickness(0,0,0,12) };
        var updateContent=new StackPanel();
        updateContent.Children.Add(PageHelpers.Lbl(Localization.T("luTitle")+" · "+main.LauncherUpdates.CurrentVersion,18,"#EFF5FF",true));
        var auto=new CheckBox { Content=Localization.T("luAutomatic"), IsChecked=main.Config.LauncherAutoUpdate, Foreground=Brushes.White, Margin=new Thickness(0,12,0,12) };
        auto.Click += (_,_)=> { main.Config.LauncherAutoUpdate=auto.IsChecked==true; ConfigManager.Save(main.Config); };
        updateContent.Children.Add(auto);
        updateButton=PageHelpers.MkBtn(Localization.T("luCheck"),"#226DA0"); updateButton.HorizontalAlignment=HorizontalAlignment.Left;
        updateButton.Click += async (_,_)=> await main.CheckLauncherUpdatesAsync(true); updateContent.Children.Add(updateButton);
        updateStatus=PageHelpers.Lbl("",13,"#ADBED6",pad:new Thickness(0,10,0,0),wrap:TextWrapping.Wrap); updateContent.Children.Add(updateStatus);
        updateCard.Child=updateContent; stack.Children.Add(updateCard); UpdateStatus();
        void Label(string key) => stack.Children.Add(PageHelpers.Lbl(Localization.T(key),15,"#FFFFFF",true,pad:new Thickness(0,16,0,6)));
        var themeCard=new Border { Background=PageHelpers.HexBrush("#192C46"), Padding=new Thickness(20), CornerRadius=new CornerRadius(14) };
        var themeContent=new StackPanel();
        themeContent.Children.Add(PageHelpers.Lbl(Localization.T("themeTitle"),18,"#EFF5FF",true));
        themeContent.Children.Add(PageHelpers.Lbl(Localization.T("themeHelp"),13,"#ADBED6",pad:new Thickness(0,8,0,12),wrap:TextWrapping.Wrap));
        var swatches=new System.Windows.Controls.Primitives.UniformGrid { Columns=4 };
        var tiles=new List<(string name,Border tile,TextBlock mark)>();
        void UpdateSelection() {
            foreach(var entry in tiles) {
                bool selected=main.Config.Accent==entry.name;
                entry.tile.BorderBrush=selected?ColorThemes.Brush("#00A3FF"):PageHelpers.HexBrush("#29292E");
                entry.tile.BorderThickness=new Thickness(selected?2:1);
                entry.tile.Background=selected?ColorThemes.Brush("#274565"):ColorThemes.Brush("#0D1727");
                entry.mark.Visibility=selected?Visibility.Visible:Visibility.Hidden;
            }
        }
        foreach(var name in ColorThemes.Names)
        {
            var color=PageHelpers.HexColor(ColorThemes.Preview(name));
            var shade=Color.FromRgb((byte)(color.R*0.18),(byte)(color.G*0.18),(byte)(color.B*0.18));
            var tile=new Border { CornerRadius=new CornerRadius(14),Padding=new Thickness(8),MinHeight=116 };
            var grid=new Grid();
            var mark=PageHelpers.Lbl("✓",14,"#FFFFFF",true); mark.HorizontalAlignment=HorizontalAlignment.Right; mark.VerticalAlignment=VerticalAlignment.Top;
            var sample=new Border { Width=46,Height=46,CornerRadius=new CornerRadius(13),Background=new LinearGradientBrush(color,shade,45),Margin=new Thickness(0,4,0,10) };
            var label=PageHelpers.Lbl(Localization.T("theme"+name),12,"#EFF5FF",wrap:TextWrapping.Wrap); label.TextAlignment=TextAlignment.Center;
            label.FontWeight=FontWeights.Normal;
            var content=new StackPanel { VerticalAlignment=VerticalAlignment.Center }; content.Children.Add(sample); content.Children.Add(label); grid.Children.Add(content); grid.Children.Add(mark); tile.Child=grid;
            var choice=PageHelpers.MkBtn("","#0D1727"); choice.Padding=new Thickness(0); choice.Content=tile; choice.Margin=new Thickness(0,0,10,10);
            choice.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            choice.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Name='focus' BorderThickness='1' BorderBrush='Transparent' CornerRadius='14'><ContentPresenter Content='{TemplateBinding Content}' HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='White'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='focus' Property='Opacity' Value='0.85'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            System.Windows.Automation.AutomationProperties.SetName(choice,Localization.T("theme"+name));
            choice.Click+=(_,_)=> {
                main.SetColorTheme(name); UpdateSelection();
                result.Text=Localization.T("themeApplied")+" · "+Localization.T("theme"+name);
            };
            tiles.Add((name,tile,mark)); swatches.Children.Add(choice);
        }
        UpdateSelection(); themeContent.Children.Add(swatches); themeCard.Child=themeContent; stack.Children.Insert(2,themeCard);
        var barCard=new Border { Background=PageHelpers.HexBrush("#192C46"),Padding=new Thickness(20),CornerRadius=new CornerRadius(14),Margin=new Thickness(0,12,0,0) };
        var barContent=new StackPanel();
        barContent.Children.Add(PageHelpers.Lbl(Localization.T("barTitle"),18,"#EFF5FF",true));
        barContent.Children.Add(PageHelpers.Lbl(Localization.T("barHelp"),13,"#ADBED6",pad:new Thickness(0,8,0,12),wrap:TextWrapping.Wrap));
        var options=new WrapPanel();
        foreach(var item in new[]{("Dash","home"),("Vers","versions"),("Mods","mods"),("Skin","skin"),("Server","server"),("Settings","settings")})
        {
            var option=new CheckBox { Content=Localization.T(item.Item2),IsChecked=main.Config.QuickLinks.Contains(item.Item1),Foreground=PageHelpers.HexBrush("#EFF5FF"),MinHeight=44,MinWidth=140,VerticalContentAlignment=VerticalAlignment.Center };
            option.Click+=(_,_)=> {
                if(option.IsChecked==true && !main.Config.QuickLinks.Contains(item.Item1)) main.Config.QuickLinks.Add(item.Item1);
                else if(option.IsChecked!=true) main.Config.QuickLinks.Remove(item.Item1);
                ConfigManager.Save(main.Config); main.BuildQuickBar();
            };
            options.Children.Add(option);
        }
        barContent.Children.Add(options); barCard.Child=barContent; stack.Children.Insert(2,barCard);
        var windowCard=new Border { Background=PageHelpers.HexBrush("#192C46"),Padding=new Thickness(20),CornerRadius=new CornerRadius(14),Margin=new Thickness(0,12,0,12) };
        var windowContent=new StackPanel();
        windowContent.Children.Add(PageHelpers.Lbl(Localization.T("windowStyleTitle"),18,"#EFF5FF",true));
        windowContent.Children.Add(PageHelpers.Lbl(Localization.T("windowStyleHelp"),13,"#ADBED6",pad:new Thickness(0,8,0,12),wrap:TextWrapping.Wrap));
        var styleChoice=new ComboBox { ItemsSource=MainWindow.WindowButtonStyles.Select(name=>Localization.T("style"+name)).ToArray(),SelectedIndex=Math.Max(0,Array.IndexOf(MainWindow.WindowButtonStyles,main.Config.WindowButtons)),MinHeight=40 };
        styleChoice.SelectionChanged+=(_,_)=> {
            if(styleChoice.SelectedIndex>=0) main.SetWindowButtons(MainWindow.WindowButtonStyles[styleChoice.SelectedIndex]);
        };
        windowContent.Children.Add(styleChoice); windowCard.Child=windowContent; stack.Children.Insert(3,windowCard);
        Label("username"); user=PageHelpers.DarkTextBox(main.Config.User); user.MaxLength=16; stack.Children.Add(user);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("usernameHelp"),13,"#BDCAD8",wrap:TextWrapping.Wrap));
        Label("memory"); ram=PageHelpers.DarkTextBox(main.Config.Ram.ToString()); stack.Children.Add(ram);
        stack.Children.Add(PageHelpers.Lbl(Localization.T("ramHelp"),13,"#BDCAD8",wrap:TextWrapping.Wrap));
        Label("language"); language=new ComboBox { ItemsSource=new[] { "Türkçe", "English" }, SelectedIndex=Localization.Language=="en"?1:0, MinHeight=36 }; stack.Children.Add(language);
        Label("auth"); provider=new ComboBox { ItemsSource=new[] { Localization.T("offline"), "Ely.by" }, SelectedIndex=main.Config.AuthType=="elyby"?1:0, MinHeight=36 }; stack.Children.Add(provider);
        close=new CheckBox { Content=Localization.T("close"), IsChecked=main.Config.AutoClose, Foreground=Brushes.White, Margin=new Thickness(0,20,0,20) }; stack.Children.Add(close);
        var save=PageHelpers.MkBtn(Localization.T("save"),"#226DA0"); save.HorizontalAlignment=HorizontalAlignment.Left;
        save.Click += (_,_) => Save();
        result=PageHelpers.Lbl("",14,"#F0CF84",pad:new Thickness(0,10,0,0),wrap:TextWrapping.Wrap); stack.Children.Add(result);
        Label("privacy"); stack.Children.Add(PageHelpers.Lbl(Localization.T("privacyText"),14,"#BDCAD8",wrap:TextWrapping.Wrap));
        var layout=new DockPanel();
        var footer=new Border { Background=PageHelpers.HexBrush("#152A43"), Padding=new Thickness(36,16,36,16), BorderBrush=PageHelpers.HexBrush("#365574"), BorderThickness=new Thickness(0,1,0,0) };
        footer.Child=save; DockPanel.SetDock(footer,Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer { Content=stack, VerticalScrollBarVisibility=ScrollBarVisibility.Auto }); Content=layout;
    }
    void UpdateStatusAsync() { if(!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(UpdateStatus)); }
    void UpdateStatus()
    {
        if(updateStatus==null) return;
        updateStatus.Text=Localization.T(main.LauncherUpdates.StatusKey)+(main.LauncherUpdates.Busy?$" ({main.LauncherUpdates.Progress:0}%)":"")+(main.LauncherUpdates.Error==null?"":"\n"+main.LauncherUpdates.Error);
        updateButton.IsEnabled=!main.LauncherUpdates.Busy;
    }
    void Save()
    {
        if (!Regex.IsMatch(user.Text.Trim(), @"^[A-Za-z0-9_]{3,16}$") || !int.TryParse(ram.Text,out int memory) || memory<1 || memory>32)
        { result.Text=Localization.T("invalid"); return; }
        try {
            main.Config.User=user.Text.Trim(); main.Config.Ram=memory;
            main.Config.AuthType=provider.SelectedIndex==1?"elyby":"offline";
            main.Config.AutoClose=close.IsChecked==true;
            main.Config.Lang=language.SelectedIndex==1?"English":"Turkce";
            ConfigManager.Save(main.Config); main.ReloadConfig(); main.SwitchLanguage(main.Config.Lang);
            result.Text=Localization.T("saved");
        } catch(Exception ex) { result.Text=Localization.T("error")+": "+ex.Message; }
    }
}
