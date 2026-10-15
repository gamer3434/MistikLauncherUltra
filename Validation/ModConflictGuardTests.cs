using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MistikLauncher;
using Newtonsoft.Json.Linq;

public static class ModConflictGuardTests
{
    static byte[] Zip(string metadata, byte[]? nested = null)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
            using (var writer = new StreamWriter(zip.CreateEntry("fabric.mod.json").Open(), new UTF8Encoding(false))) writer.Write(metadata);
            if (nested != null) { using var output = zip.CreateEntry("META-INF/jars/nested.jar").Open(); output.Write(nested); }
        }
        return memory.ToArray();
    }
    static byte[] Mod(string id, JToken? breaks = null, JArray? provides = null)
    {
        var data = new JObject { ["schemaVersion"] = 1, ["id"] = id, ["version"] = "1.0.0" };
        if (breaks != null) data["breaks"] = breaks;
        if (provides != null) data["provides"] = provides;
        return Zip(data.ToString());
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA512.HashData(bytes));
    static bool Conflict(Action operation)
    {
        try { operation(); return false; } catch (ModConflictException) { return true; }
    }
    static bool InvalidMetadata(Action operation)
    {
        try { operation(); return false; } catch (InvalidDataException ex) { return ex.Message.Contains("fabric.mod.json") && ex.Message.Length < 256; }
    }

    public static int Run(string root)
    {
        root = Path.Combine(root, "conflict-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
        string Folder(string name) { var path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        void Candidate(string folder, string filename, byte[] bytes) { using var input = new MemoryStream(bytes); ModConflictGuard.CheckInstall(folder, filename, input); }
        var first = Mod("first_mod", new JObject { ["second_mod"] = "*" });
        var second = Mod("second_mod");
        var pair = Folder("pair"); File.WriteAllBytes(Path.Combine(pair, "first.jar"), first);
        using (var input = new MemoryStream(second)) {
            Check(Conflict(() => ModConflictGuard.CheckInstall(pair, "second.jar", input)) && input.CanRead && input.Position == 0,
                "existing unconditional break rejects incoming mod without closing or advancing its stream");
        }
        var reverse = Folder("reverse"); File.WriteAllBytes(Path.Combine(reverse, "second.jar"), second);
        Check(Conflict(() => Candidate(reverse, "first.jar", first)), "incoming unconditional break rejects existing mod");
        var array = Mod("array_mod", new JObject { ["second_mod"] = new JArray(">=9", "*") });
        Check(Conflict(() => Candidate(reverse, "array.jar", array)), "wildcard in break alternatives is unconditional");
        var alias = Folder("alias"); File.WriteAllBytes(Path.Combine(alias, "provider.jar"), Mod("provider_mod", provides:new JArray("second_mod")));
        Check(Conflict(() => Candidate(alias, "first.jar", first)), "breaks match a declared provided alias");
        var reverseAlias = Folder("reverse-alias"); File.WriteAllBytes(Path.Combine(reverseAlias, "first.jar"), first);
        Check(Conflict(() => Candidate(reverseAlias, "provider.jar", Mod("provider_mod", provides:new JArray("second_mod")))), "existing breaks match incoming provided alias");
        File.WriteAllBytes(Path.Combine(pair, "second.jar"), second);
        Check(Conflict(() => ModConflictGuard.CheckDirectory(pair)), "prelaunch directory guard detects the same top-level conflict");
        var disabled = Folder("disabled"); File.WriteAllBytes(Path.Combine(disabled, "first.jar.disabled"), first);
        Candidate(disabled, "second.jar", second); ModConflictGuard.CheckDirectory(disabled);
        Check(true, "disabled jars do not participate in conflict detection");
        var replace = Folder("replace"); File.WriteAllBytes(Path.Combine(replace, "same.jar"), first);
        Candidate(replace, "SAME.jar", second);
        Check(File.ReadAllBytes(Path.Combine(replace, "same.jar")).SequenceEqual(first), "same filename replacement excludes its previous identity without changing bytes");
        Candidate(reverse, "versioned.jar", Mod("versioned_mod", new JObject { ["second_mod"] = ">=2.0.0" }));
        Check(true, "versioned break predicates remain for Fabric Loader to resolve");
        var serverOnly = new JObject { ["id"] = "server_only", ["version"] = "1.0.0", ["environment"] = "server", ["breaks"] = new JObject { ["second_mod"] = "*" } };
        Candidate(reverse, "server.jar", Zip(serverOnly.ToString()));
        Check(true, "server-only metadata cannot create a client-side conflict");
        var nested = Zip(new JObject { ["schemaVersion"] = 1, ["id"] = "multi_version", ["version"] = "1.0.0",
            ["jars"] = new JArray(new JObject { ["file"] = "META-INF/jars/nested.jar" }) }.ToString(),
            Mod("nested_other_version", new JObject { ["second_mod"] = "*" }));
        Candidate(reverse, "multi-version.jar", nested);
        Check(true, "nested multi-version alternatives are not treated as unconditional top-level conflicts");
        using (var input = new MemoryStream(Encoding.UTF8.GetBytes("non-ZIP fixture bytes"))) {
            ModConflictGuard.CheckInstall(reverse, "fixture.jar", input);
            Check(input.CanRead && input.Position == 0, "non-ZIP fixtures are ignored and candidate stream remains usable");
        }
        using (var memory = new MemoryStream()) {
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) zip.CreateEntry("META-INF/mods.toml");
            Candidate(reverse, "nonfabric.jar", memory.ToArray());
        }
        Check(true, "JARs without top-level Fabric metadata are ignored");
        foreach (var descriptor in new[] { "META-INF/mods.toml", "META-INF/neoforge.mods.toml" }) {
            using var memory = new MemoryStream();
            memory.Write(first);
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) zip.CreateEntry(descriptor);
            Candidate(reverse, "hybrid.jar", memory.ToArray());
            Check(true, "hybrid JAR Fabric constraints cannot reject " + descriptor + " installations");
        }
        Check(InvalidMetadata(() => Candidate(reverse, "malformed.jar", Zip("{"))), "malformed recognized Fabric JSON produces a bounded metadata error");
        Check(InvalidMetadata(() => Candidate(reverse, "oversized.jar", Zip(new string(' ', 256 * 1024 + 1)))), "oversized recognized Fabric metadata is rejected before JSON parsing");
        Check(InvalidMetadata(() => Candidate(reverse, "bad-break.jar", Mod("bad_break", new JObject { ["second_mod"] = true }))), "malformed recognized break constraint is rejected");
        var installed = Folder("install"); File.WriteAllBytes(Path.Combine(installed, "first.jar"), first);
        Check(Conflict(() => ModFiles.Install(installed, "second.jar", second, Hash(second))) && !File.Exists(Path.Combine(installed, "second.jar")) &&
            File.ReadAllBytes(Path.Combine(installed, "first.jar")).SequenceEqual(first), "conflicting install preserves existing bytes and creates no new jar");
        var sources = Folder("sources"); var source = Path.Combine(sources, "second.jar"); File.WriteAllBytes(source, second);
        Check(Conflict(() => ModFiles.Import(installed, source)) && File.ReadAllBytes(source).SequenceEqual(second) && !File.Exists(Path.Combine(installed, "second.jar")),
            "conflicting import preserves its source and destination");
        var inactive = Path.Combine(installed, "second.jar.disabled"); File.WriteAllBytes(inactive, second);
        Check(Conflict(() => ModFiles.Toggle(installed, inactive)) && File.Exists(inactive) && File.ReadAllBytes(inactive).SequenceEqual(second) &&
            !File.Exists(Path.Combine(installed, "second.jar")), "enabling a conflicting mod preserves its disabled state and bytes");
        return checks;
    }
}
