using System.IO;
using MistikLauncher;
public static class ModToggleTests
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root); int checks=0;
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); checks++; }
        var jar=Path.Combine(root,"sample.jar"); File.WriteAllText(jar,"original mod bytes");
        var disabled=ModFiles.Toggle(root,jar);
        Check(!File.Exists(jar) && File.ReadAllText(disabled)=="original mod bytes" && !ModFiles.Enabled(disabled),"disabling preserves mod bytes outside active jar scan");
        Check(ModFiles.List(root).Single()==disabled,"disabled mod remains in installed list");
        Check(ModFiles.Toggle(root,disabled)==jar && File.ReadAllText(jar)=="original mod bytes","enabling restores exact file without redownload");
        File.WriteAllText(disabled,"existing disabled copy"); bool rejected=false;
        try { ModFiles.Toggle(root,jar); } catch(IOException) { rejected=true; }
        Check(rejected && File.ReadAllText(jar)=="original mod bytes" && File.ReadAllText(disabled)=="existing disabled copy","name collision never overwrites either mod");
        using(var locked=new FileStream(jar,FileMode.Open,FileAccess.Read,FileShare.None)) {
            File.Delete(disabled); rejected=false; try { ModFiles.Toggle(root,jar); } catch(IOException) { rejected=true; }
            Check(rejected && File.Exists(jar),"locked running-game mod reports failure and remains intact");
        }
        var outside=Path.Combine(Path.GetDirectoryName(root)!,"outside.jar"); File.WriteAllText(outside,"outside"); rejected=false;
        try { ModFiles.Toggle(root,outside); } catch(IOException) { rejected=true; }
        Check(rejected && File.Exists(outside),"toggle rejects files outside managed mod directory");
        return checks;
    }
}
