using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MistikLauncher;
using MistikLauncher.Pages;
using Localization = MistikLauncher.Localization;

static class SkinWorkflowTests
{
    public static int Run(MainWindow window)
    {
        int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var original=window.Config;
        try
        {
            var page=new SkinPage(window);
            var nameBox=Nodes(page).OfType<TextBox>().Single(box=>box.Name=="SkinLookupName");
            nameBox.Text="../../invalid";
            var apply=Nodes(page).OfType<Button>().Single(button=>button.Name=="SkinApplyUsername");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ReferenceEquals(original,window.Config) && ConfigManager.Load().User==original.User && apply.IsEnabled && Nodes(page).OfType<TextBlock>().Any(label=>label.Text==Localization.T("usernameHelp")),"invalid skin name shows guidance without changing the saved player profile");

            var pack=Path.Combine(App.GameDir,"resourcepacks","MistikSkinPack");
            Directory.CreateDirectory(pack);
            var previousSkin=Path.Combine(pack,"existing-skin.png");
            File.WriteAllText(previousSkin,"existing skin bytes");
            var options=Path.Combine(App.GameDir,"options.txt");
            const string savedOptions="resourcePacks:[\"vanilla\",\"file/MistikSkinPack\"]\n";
            File.WriteAllText(options,savedOptions);
            var invalidSkin=Path.Combine(App.AppData,"invalid-local-skin.png");
            File.WriteAllText(invalidSkin,"not a PNG");
            window.Config=new LauncherConfig { SkinType="local",SkinUser=invalidSkin };
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1")) && File.ReadAllText(previousSkin)=="existing skin bytes" && File.ReadAllText(options)==savedOptions,"corrupt local skin leaves the existing skin pack and game options intact");
            window.Config=new LauncherConfig { SkinType="username",SkinUser="../../invalid" };
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1")) && File.ReadAllText(previousSkin)=="existing skin bytes","skin preparation rejects invalid names before cache or network access");
            window.Config=new LauncherConfig { SkinType="username",SkinUser="TestSkin" };
            var cachedAvatar=Path.Combine(App.AppData,"elyby_TestSkin.png");
            File.WriteAllText(cachedAvatar,"existing avatar bytes");
            using var offline=new HttpClient(new SkinHandler(Array.Empty<byte>(),true));
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1",offline)) && File.ReadAllText(previousSkin)=="existing skin bytes" && File.ReadAllText(options)==savedOptions && File.ReadAllText(cachedAvatar)=="existing avatar bytes","offline username skin refresh preserves the installed pack, options and cached avatar");
            using var corrupt=new HttpClient(new SkinHandler("invalid PNG"u8.ToArray()));
            Check(!Wait(window.PrepareSkinPackAsync("1.20.1",corrupt)) && File.ReadAllText(previousSkin)=="existing skin bytes" && File.ReadAllText(options)==savedOptions && File.Exists(cachedAvatar),"invalid online skin responses leave the previous installed skin intact");
            var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,new byte[64*64*4],64*4);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var png=new MemoryStream(); encoder.Save(png);
            var bytes=png.ToArray();
            var fallbackHandler=new SkinHandler(bytes);
            using var fallback=new HttpClient(fallbackHandler);
            Check(Wait(window.PrepareSkinPackAsync("1.20.1",fallback)) && fallbackHandler.FallbackRequests==1 && File.ReadAllBytes(Path.Combine(pack,"assets","minecraft","textures","entity","steve.png")).SequenceEqual(bytes),"corrupt Ely.by skin falls back to a validated Minecraft skin and installs it");
            var image=new Image();
            var previewHandler=new SkinHandler(bytes);
            using var preview=new HttpClient(previewHandler);
            Check(Wait(SkinPage.LoadImgAsync(image,"TestSkin",80,preview)) && previewHandler.FallbackRequests==1 && image.Source!=null,"skin preview falls back after invalid Ely.by image data");
            var priorPreview=image.Source;
            using var invalidPreview=new HttpClient(new SkinHandler("not an image"u8.ToArray()));
            Check(!Wait(SkinPage.LoadImgAsync(image,"TestSkin",80,invalidPreview)) && ReferenceEquals(image.Source,priorPreview),"invalid skin preview reports failure while preserving the displayed image");
            window.Config.SkinType="none";
            Check(Wait(window.PrepareSkinPackAsync("1.20.1",offline)) && !Directory.Exists(pack) && !File.ReadAllText(options).Contains("MistikSkinPack"),"explicit skin reset still removes the installed pack and disables it");

            window.Config=new LauncherConfig { SkinType="username",SkinUser="SlowSkin" };
            var resetHandler=new DelayedSkinHandler(bytes);
            using var resetClient=new HttpClient(resetHandler);
            var beforeReset=window.PrepareSkinPackAsync("1.20.1",resetClient);
            Check(!beforeReset.IsCompleted,"skin race fixture waits for an in-flight username download");
            window.Config.SkinType="none";
            Check(Wait(window.PrepareSkinPackAsync("1.20.1",offline)),"skin reset completes while a username request is in flight");
            resetHandler.Release();
            Check(!Wait(beforeReset) && !Directory.Exists(pack) && !File.ReadAllText(options).Contains("MistikSkinPack"),"late username download cannot re-enable a skin after reset");

            window.Config=new LauncherConfig { SkinType="username",SkinUser="SlowSkin" };
            var localHandler=new DelayedSkinHandler(bytes);
            using var localClient=new HttpClient(localHandler);
            var beforeLocal=window.PrepareSkinPackAsync("1.20.1",localClient);
            var localFile=Path.Combine(App.AppData,"race-local-skin.png"); File.WriteAllBytes(localFile,bytes);
            window.Config.SkinType="local"; window.Config.SkinUser=localFile;
            Check(Wait(window.PrepareSkinPackAsync("1.20.1",offline)),"local skin replaces an in-flight username selection");
            var installedSkin=Path.Combine(pack,"assets","minecraft","textures","entity","steve.png");
            var localOptions=File.ReadAllText(options);
            localHandler.Release();
            Check(!Wait(beforeLocal) && File.ReadAllBytes(installedSkin).SequenceEqual(bytes) && File.ReadAllText(options)==localOptions,"late username download preserves the newer local pack and options");

            window.Config=new LauncherConfig { SkinType="username",SkinUser="SlowSkin" };
            var versionHandler=new DelayedSkinHandler(bytes);
            using var versionClient=new HttpClient(versionHandler);
            var beforeVersionChange=window.PrepareSkinPackAsync("1.20.1",versionClient);
            window.Config.Version="26.1";
            versionHandler.Release();
            Check(!Wait(beforeVersionChange) && File.ReadAllBytes(installedSkin).SequenceEqual(bytes),"a changed game profile rejects a delayed skin pack commit even without another preparation call");

            var previewRaceHandler=new DelayedSkinHandler(bytes);
            using var previewRaceClient=new HttpClient(previewRaceHandler);
            var olderPreview=SkinPage.LoadImgAsync(image,"OldSkin",80,previewRaceClient);
            Check(Wait(SkinPage.LoadImgAsync(image,"NewSkin",80,preview)),"new preview completes while an old search is pending");
            var newPreview=image.Source;
            previewRaceHandler.Release();
            Check(!Wait(olderPreview) && ReferenceEquals(image.Source,newPreview),"late skin search cannot overwrite a newer preview");
            var chosenLocalHandler=new DelayedSkinHandler(bytes);
            using var chosenLocalClient=new HttpClient(chosenLocalHandler);
            var beforeChoosingLocal=SkinPage.LoadImgAsync(image,"OldSkin",80,chosenLocalClient);
            SkinPage.SetPreview(image,bitmap);
            chosenLocalHandler.Release();
            Check(!Wait(beforeChoosingLocal) && ReferenceEquals(image.Source,bitmap),"choosing a local preview invalidates an earlier online search");

            window.Config=new LauncherConfig { SkinType="username",SkinUser="AvatarSlow" };
            var avatarHandler=new DelayedSkinHandler(bytes);
            using var avatarClient=new HttpClient(avatarHandler);
            var olderAvatar=window.LoadAvatarAsync(avatarClient);
            window.Config.SkinType="local"; window.Config.SkinUser=localFile;
            Wait(window.LoadAvatarAsync(offline));
            var avatar=(Image)window.FindName("AvatarImg"); var localAvatar=avatar.Source;
            avatarHandler.Release(); Wait(olderAvatar);
            Check(localAvatar!=null && ReferenceEquals(avatar.Source,localAvatar),"late account avatar download cannot replace a newly selected local avatar");

            var packMetadata=Path.Combine(pack,"pack.mcmeta");
            var metadataBefore=File.ReadAllText(packMetadata);
            using(var locked=new FileStream(packMetadata,FileMode.Open,FileAccess.Read,FileShare.None))
                Check(!Wait(window.PrepareSkinPackAsync("1.20.1",offline)) && File.ReadAllBytes(installedSkin).SequenceEqual(bytes) && File.ReadAllText(options)==localOptions,"locked skin pack replacement fails without damaging the installed textures or options");
            Check(File.ReadAllText(packMetadata)==metadataBefore,"failed skin pack commit preserves original metadata");
        }
        finally { window.Config=original; }
        return checks;
    }
    static bool Wait(Task<bool> task)
    {
        Wait((Task)task);
        return task.GetAwaiter().GetResult();
    }
    static void Wait(Task task)
    {
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame(); var dispatcher=Dispatcher.CurrentDispatcher;
            _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    {
        yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var node in Nodes(child)) yield return node;
    }
    sealed class SkinHandler(byte[] fallback,bool offline=false) : HttpMessageHandler
    {
        public int FallbackRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(offline) return Task.FromException<HttpResponseMessage>(new HttpRequestException("offline fixture"));
            HttpContent content;
            if(request.RequestUri!.Host=="skinsystem.ely.by") content=new StringContent("{\"SKIN\":{\"url\":\"https://textures.minecraft.net/skin/fixture\"}}");
            else if(request.RequestUri.Host=="textures.minecraft.net") content=new ByteArrayContent("corrupt Ely.by image"u8.ToArray());
            else { FallbackRequests++; content=new ByteArrayContent(fallback); }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
        }
    }
    sealed class DelayedSkinHandler(byte[] skin) : HttpMessageHandler
    {
        readonly TaskCompletionSource<HttpResponseMessage> response=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release()=>response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"SKIN\":{\"url\":\"https://textures.minecraft.net/skin/fixture\"}}") });
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
            => request.RequestUri!.Host=="skinsystem.ely.by" ? response.Task : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new ByteArrayContent(skin) });
    }
}
