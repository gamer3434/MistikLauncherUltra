using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MistikLauncher;
using Newtonsoft.Json.Linq;

static class CloudTests
{
    const string Key="AIzaSyCP7R_Q8Ai_b_zKTCBRk9d1kkZY3iilENg";
    const string Database="https://mistiklauncher-9eb4b-default-rtdb.firebaseio.com";
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        var secret=Encoding.UTF8.GetBytes("fixture refresh token");
        var encrypted=WindowsSecret.Transform(secret,true);
        Require(!encrypted.SequenceEqual(secret) && WindowsSecret.Transform(encrypted,false).SequenceEqual(secret),"Windows session encryption round trip");
        var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,new byte[64*64*4],64*4);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string skin=Path.Combine(root,"skin.png"); using(var stream=File.Create(skin)) encoder.Save(stream);
        var config=new LauncherConfig { User="CloudTester",SkinType="local",SkinUser=skin,Role="Admin",Ram=64 };
        var profile=CloudProfiles.Snapshot(config);
        Require(profile["role"]==null && profile["skin_user"]!.ToString()=="" && !profile.ToString().Contains(root),"cloud snapshot excludes privileges and filesystem paths");
        var client=new CloudProfiles(Path.Combine(root,"restored"));
        profile["role"]="Admin"; profile["server_dir"]="C:\\Windows";
        var restored=client.Apply(profile,new LauncherConfig());
        Require(restored.Role=="User" && restored.Ram==32 && File.ReadAllBytes(restored.SkinUser).SequenceEqual(File.ReadAllBytes(skin)),"cloud restore preserves bounded skin bytes and ignores extra fields");
        bool rejected=false; try { CloudProfiles.ValidateSkin(new byte[33]); } catch(InvalidDataException) { rejected=true; }
        Require(rejected,"malformed cloud skin rejected");
        File.Delete(skin);
        Require(CloudProfiles.Snapshot(config)["skin_type"]!.ToString()=="default","missing local skin uses valid default cloud profile");
        config.QuickLinks.Clear();
        Require(client.Apply(CloudProfiles.Snapshot(config),new LauncherConfig()).QuickLinks.Count==0,"empty toolbar survives cloud snapshot and restore");
        return 6;
    }
    public static async Task<int> Live(string root)
    {
        var client=new CloudProfiles(root);
        using var http=new HttpClient { Timeout=TimeSpan.FromSeconds(25) };
        string email="mistik-validation-"+Guid.NewGuid().ToString("N")+"@example.invalid";
        string password=Guid.NewGuid().ToString("N")+"!aA1";
        JObject? session=null;
        try {
            await client.SignInAsync(email,password,true);
            session=JObject.Parse(Encoding.UTF8.GetString(WindowsSecret.Transform(File.ReadAllBytes(Path.Combine(root,"cloud-session.dat")),false)));
            string token=Uri.EscapeDataString(session["idToken"]!.ToString());
            string own=$"{Database}/launcherProfiles/{session["localId"]}.json?auth={token}";
            var config=new LauncherConfig { User="CloudTester",Lang="English",Accent="Nebula",Ram=6,QuickLinks=new() };
            await client.UploadAsync(config);
            var restored=await client.RestoreAsync(new LauncherConfig());
            Require(restored?.User==config.User && restored.Accent==config.Accent && restored.Ram==6 && restored.QuickLinks.Count==0,"live cloud upload and restore including empty toolbar");
            var restarted=new CloudProfiles(root);
            Require(restarted.SignedIn && restarted.Email==email,"protected session survives restart");
            using var other=await http.GetAsync($"{Database}/launcherProfiles/unrelated-user.json?auth={token}");
            Require(!other.IsSuccessStatusCode,"live rules reject another user's profile read");
            using var publicRead=await http.GetAsync(Database+"/launcherProfiles.json");
            Require(!publicRead.IsSuccessStatusCode,"live rules reject unauthenticated profile listing");
            var invalid=CloudProfiles.Snapshot(config); invalid["role"]="Admin";
            using var invalidWrite=await http.PutAsync(own,new StringContent(invalid.ToString(),Encoding.UTF8,"application/json"));
            Require(!invalidWrite.IsSuccessStatusCode,"live rules reject unknown privileged fields on update");
            invalid.Remove("role"); invalid["ram"]=500;
            using var ramWrite=await http.PutAsync(own,new StringContent(invalid.ToString(),Encoding.UTF8,"application/json"));
            Require(!ramWrite.IsSuccessStatusCode,"live rules reject out-of-range settings on update");
            using var otherWrite=await http.PutAsync($"{Database}/launcherProfiles/unrelated-user.json?auth={token}",new StringContent(CloudProfiles.Snapshot(config).ToString(),Encoding.UTF8,"application/json"));
            Require(!otherWrite.IsSuccessStatusCode,"live rules reject another user's profile write");
            await client.SignOutAsync();
            Require(!client.SignedIn && !File.Exists(Path.Combine(root,"cloud-session.dat")),"sign out removes encrypted local session");
            return 8;
        }
        finally {
            if(session!=null) {
                using var deleted=await http.DeleteAsync($"{Database}/launcherProfiles/{session["localId"]}.json?auth={Uri.EscapeDataString(session["idToken"]!.ToString())}");
                Require(deleted.IsSuccessStatusCode,"temporary cloud profile cleaned up");
                using var account=await http.PostAsync($"https://identitytoolkit.googleapis.com/v1/accounts:delete?key={Key}",new StringContent(new JObject { ["idToken"]=session["idToken"] }.ToString(),Encoding.UTF8,"application/json"));
                Require(account.IsSuccessStatusCode,"temporary validation account cleaned up");
            }
            await client.SignOutAsync();
        }
    }
    static void Require(bool value,string name) { if(!value) throw new InvalidOperationException(name); Console.WriteLine("PASS "+name); }
}
