using System.IO;
using MistikLauncher;
using Newtonsoft.Json.Linq;
public static class ForgeTests
{
    public static int Run(string root)
    {
        int checks=0;
        void Check(bool result,string name) { if(!result) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        void Profile(string id,string json,bool jar=false) {
            var dir=Path.Combine(root,"versions",id); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,id+".json"),json);
            if(jar) File.WriteAllText(Path.Combine(dir,id+".jar"),"fixture");
        }
        Profile("1.20.1","{\"id\":\"1.20.1\"}",true);
        Profile("1.20.1-forge-47.4.10","{\"id\":\"1.20.1-forge-47.4.10\",\"inheritsFrom\":\"1.20.1\",\"libraries\":[{\"name\":\"net.minecraftforge:forge:1.20.1-47.4.10\"}]}");
        Profile("1.20.1-forge-47.4.23","{\"inheritsFrom\":\"1.20.1\",\"libraries\":[{\"name\":\"net.minecraftforge:fmlloader:1.20.1-47.4.23\"}]}");
        Check(GameProfiles.IsInstalled(root,"1.20.1-forge-47.4.10"),"official Forge profile inherits Vanilla jar without own jar");
        Check(GameProfiles.InstalledIds(root).Count(x=>GameProfiles.Kind(GameProfiles.Read(root,x)!)=="Forge")==2,"multiple installed Forge builds remain selectable");
        Profile("forge-1.20.1-47.2.0","{\"mainClass\":\"net.minecraftforge.bootstrap.ForgeBootstrap\"}",true);
        Check(!GameProfiles.IsInstalled(root,"forge-1.20.1-47.2.0"),"old fake Forge Vanilla copy is rejected");
        Profile("cycle","{\"inheritsFrom\":\"cycle\"}");
        Check(!GameProfiles.IsInstalled(root,"cycle"),"profile inheritance cycle rejected");
        Profile("escape","{\"inheritsFrom\":\"../../outside\"}");
        Check(!GameProfiles.IsInstalled(root,"escape"),"profile inheritance traversal rejected");
        var args=JArray.Parse("[\"--launchTarget\",\"forgeclient\",{\"rules\":[{\"action\":\"allow\",\"os\":{\"name\":\"windows\"}}],\"value\":[\"--add-opens\",\"java.base/java.lang=ALL-UNNAMED\"]},{\"rules\":[{\"action\":\"allow\",\"os\":{\"name\":\"linux\"}}],\"value\":\"linux-only\"}]");
        Check(GameProfiles.Arguments(args).SequenceEqual(new[]{"--launchTarget","forgeclient","--add-opens","java.base/java.lang=ALL-UNNAMED"}),"Forge launch arguments and Windows JVM rules retained");
        Profile("1.20.1-neoforge-47.1.106","{\"inheritsFrom\":\"1.20.1\",\"libraries\":[{\"name\":\"net.neoforged:neoforge:47.1.106\"}]}");
        Check(GameProfiles.Kind(GameProfiles.Read(root,"1.20.1-neoforge-47.1.106")!)=="NeoForge" && GameProfiles.IsInstalled(root,"1.20.1-neoforge-47.1.106"),"NeoForge profiles remain distinct and selectable");
        return checks;
    }
}
