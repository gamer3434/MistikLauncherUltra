using System.IO;
using MistikLauncher;
using Newtonsoft.Json.Linq;
public static class ForgeTests
{
    public static int Classpath(MainWindow window)
    {
        int checks=0;
        void Check(bool result,string name) { if(!result) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        string root=App.GameDir, id="classpath-fixture";
        string directory=Path.Combine(root,"versions",id); Directory.CreateDirectory(directory);
        string profilePath=Path.Combine(directory,id+".json");
        File.WriteAllText(Path.Combine(directory,id+".jar"),"client fixture");
        JObject Library(string name,JArray? rules=null,bool classifiers=false) {
            string relative="classpath-fixture/"+name+".jar";
            string path=Path.Combine(root,"libraries",relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,"library fixture");
            var item=new JObject { ["name"]="fixture:"+name+":1", ["downloads"]=classifiers?new JObject { ["classifiers"]=new JObject() }:new JObject { ["artifact"]=new JObject { ["path"]=relative } } };
            if(rules!=null) item["rules"]=rules;
            return item;
        }
        var allowed=Library("allowed");
        var linux=Library("linux",JArray.Parse("[{\"action\":\"allow\",\"os\":{\"name\":\"linux\"}}]"));
        var blocked=Library("blocked",JArray.Parse("[{\"action\":\"allow\"},{\"action\":\"disallow\",\"os\":{\"name\":\"windows\"}}]"));
        var nativeOnly=Library("native-only",classifiers:true);
        var modernNative=Library("modern-native"); modernNative["name"]="org.lwjgl:lwjgl:3:natives-windows";
        var windowsVersion=Library("windows-version",new JArray(new JObject { ["action"]="allow", ["os"]=new JObject { ["name"]="windows", ["version"]="^"+System.Text.RegularExpressions.Regex.Escape(Environment.OSVersion.Version.ToString())+"$" } }));
        var profile=new JObject { ["libraries"]=new JArray(allowed,linux,blocked,nativeOnly,modernNative,windowsVersion) };
        File.WriteAllText(profilePath,profile.ToString());
        var method=typeof(MainWindow).GetMethod("BuildClasspath",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        string[] Resolve()=>((string)method.Invoke(window,new object[]{id})!).Split(';');
        bool Rejected() { try { Resolve(); return false; } catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is InvalidDataException) { return true; } }
        var paths=Resolve();
        Check(paths.Length==3 && paths.Any(p=>p.EndsWith("allowed.jar")) && paths.Any(p=>p.EndsWith("windows-version.jar")),"launch classpath honors Windows allow/disallow/version rules and excludes native-only jars");
        profile["libraries"]=new JArray(new JObject { ["name"]="fixture:zip:1@zip" });
        string zip=Path.Combine(root,"libraries","fixture","zip","1","zip-1.zip"); Directory.CreateDirectory(Path.GetDirectoryName(zip)!); File.WriteAllText(zip,"fixture"); File.WriteAllText(profilePath,profile.ToString());
        Check(Resolve().Contains(zip),"launch classpath uses the runtime verifier Maven extension resolution");
        profile["libraries"]=new JArray(new JObject { ["name"]="fixture:escape:1", ["downloads"]=new JObject { ["artifact"]=new JObject { ["path"]="../outside.jar" } } }); File.WriteAllText(profilePath,profile.ToString());
        Check(Rejected(),"launch classpath rejects library path traversal");
        profile["libraries"]=new JArray(); profile["inheritsFrom"]=id; File.WriteAllText(profilePath,profile.ToString());
        Check(Rejected(),"launch classpath rejects cyclic profile inheritance without recursion overflow");
        profile["inheritsFrom"]="../../outside"; File.WriteAllText(profilePath,profile.ToString());
        Check(Rejected(),"launch classpath rejects inherited profile path traversal");
        File.Delete(profilePath); File.Delete(Path.Combine(directory,id+".jar"));
        return checks;
    }

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
        Profile("1.21.11","{\"javaVersion\":{\"majorVersion\":21}}",true);
        Profile("26.1","{\"javaVersion\":{\"majorVersion\":25}}",true);
        Profile("fabric-java21","{\"inheritsFrom\":\"1.21.11\"}");
        Check(GameProfiles.RequiredJava(root,"1.21.11")==21 && GameProfiles.RequiredJava(root,"fabric-java21")==21 && GameProfiles.RequiredJava(root,"26.1")==25,"Java requirements follow official metadata and inherited profiles, not version-name guesses");
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
        var conditional=JArray.Parse("[{\"rules\":[{\"action\":\"allow\",\"os\":{\"name\":\"windows\",\"version\":\"^THIS_OS_CANNOT_MATCH$\"}}],\"value\":\"wrong-os\"},{\"rules\":[{\"action\":\"allow\",\"features\":{\"is_demo_user\":false}}],\"value\":\"normal-user\"},{\"rules\":[{\"action\":\"allow\",\"features\":{\"is_demo_user\":true}}],\"value\":\"demo\"},{\"rules\":[{\"action\":\"allow\",\"os\":{\"name\":\"windows\",\"arch\":\"amd64\"}}],\"value\":\"windows64\"}]");
        Check(GameProfiles.Arguments(conditional).SequenceEqual(new[]{"normal-user","windows64"}),"JVM arguments share runtime Windows version, architecture and disabled-feature rules");
        Profile("1.20.1-neoforge-47.1.106","{\"inheritsFrom\":\"1.20.1\",\"libraries\":[{\"name\":\"net.neoforged:neoforge:47.1.106\"}]}");
        Check(GameProfiles.Kind(GameProfiles.Read(root,"1.20.1-neoforge-47.1.106")!)=="NeoForge" && GameProfiles.IsInstalled(root,"1.20.1-neoforge-47.1.106"),"NeoForge profiles remain distinct and selectable");
        Profile("quilt-custom","{\"inheritsFrom\":\"26.1\",\"libraries\":[{\"name\":\"net.fabricmc:fabric-loader:1\"},{\"name\":\"org.quiltmc:quilt-loader:1\"}]}");
        Check(GameProfiles.MinecraftVersion(root,"quilt-custom")=="26.1" && GameProfiles.Loader(root,"quilt-custom")=="quilt","mod identity follows inherited year-based version and distinguishes Quilt");
        Profile("custom-alias","{\"inheritsFrom\":\"quilt-custom\",\"libraries\":[]}");
        Check(GameProfiles.MinecraftVersion(root,"custom-alias")=="26.1" && GameProfiles.Loader(root,"custom-alias")=="quilt","custom profile without loader libraries inherits its parent loader and mod pool identity");
        Check(GameProfiles.MinecraftVersion(root,"fabric-loader-0.16.10-26.1")=="26.1" && GameProfiles.VersionPoolKey("26.1","NeoForge")=="26.1_neoforge" && GameProfiles.VersionPoolKey("","vanilla")=="vanilla","pool keys preserve exact version and loader for queued and active mods");
        return checks;
    }
}
