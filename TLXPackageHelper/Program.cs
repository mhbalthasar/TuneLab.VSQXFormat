using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using TLXPackageHelper;

internal class Program
{

    private static void Main(string[] args)
    {
        Console.WriteLine("TLX插件打包助手");
        Package("VSQXFormat", @"..\..\..\..\VSQXFormat\", @"..\Output\");
    }
    //VsqxFormatSupport-0.0.5.tlx
    private static void Package(string PluginName, string ProjectDir = "", string OutputDir = "..\\")
    {
        string CompileOutputDir = Path.Combine(System.Environment.GetEnvironmentVariable("AppData"),"TuneLab","Extensions",PluginName);



        string Depends = Path.Combine(ProjectDir, "..", "Dependences");
        string descriptionPath = Path.Combine(Depends, "description.json");
        FileInfo fi = new FileInfo(descriptionPath);
        string ff = fi.FullName;
        ExtensionDescription? description = null;
        if (File.Exists(descriptionPath))
        {
            try
            {
                string desContent = "";
                using (FileStream fs = new FileStream(descriptionPath, FileMode.Open))
                {
                    StreamReader sr = new StreamReader(fs);
                    desContent = sr.ReadToEnd();
                }
                description = JsonSerializer.Deserialize<ExtensionDescription>(desContent);
            }
            catch {; }
        }
        #if DEBUG
                string DebugSign = "-Debug";
        #else
                        string DebugSign = "";
        #endif
        string FileName = description == null ? "OutputTlx.tlx" : string.Format("{0}Support-v{1}{2}.tlx", description.name, description.version, DebugSign);
        string OutputFile = Path.Combine(OutputDir, FileName);


        string GetProjectDir = Assembly.GetExecutingAssembly().Location.Split("\\bin\\Release\\")[0].Split("\\bin\\Debug\\")[0];
        if (ProjectDir.StartsWith(".")) { ProjectDir = Path.Combine(GetProjectDir, ProjectDir); }
        if (OutputFile.StartsWith(".")) { OutputFile = Path.Combine(GetProjectDir, OutputFile); }
        Dictionary<string, string> FilePath = SearchFile(Depends,SearchFile(CompileOutputDir),"",true);
        ZipTo(FilePath, OutputFile);
        Console.WriteLine("Done!");
    }

    private static Dictionary<string, string> SearchFile(string BaseDir,Dictionary<string,string>? BaseDictionary=null, string DirPrefix = "", bool isDepends=false)
    {
        List<string> disableFile = new List<string> { 
            "TuneLab.Base.dll","TuneLab.Extensions.Formats.dll"
        };
        List<string> disableExt = new List<string> { ".pdb" };
        List<string> disableDir = new List<string> { "runtimes" };
        Dictionary<string, string> ret = BaseDictionary == null ? new Dictionary<string, string>() : BaseDictionary;
        if (System.IO.Path.Exists(BaseDir))
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(BaseDir);
            foreach (FileInfo f in di.GetFiles())
            {
                if (!isDepends && disableFile.Contains(f.Name)) continue;//这个交给Depends
                string pf = Path.Combine(DirPrefix, f.Name);
                string file = f.FullName;
                string ext = f.Extension;
                if (!isDepends && disableExt.Contains(ext)) continue;
                if (ret.ContainsKey(pf)) ret[pf] = file; else ret.Add(pf, file);
            }
            foreach (DirectoryInfo d in di.GetDirectories())
            {
                if (!isDepends && disableDir.Contains(d.Name)) continue;//这个交给Depends
                string pf = Path.Combine(DirPrefix, d.Name);
                string dir = d.FullName;
                ret = SearchFile(dir, ret, pf);
            }
        }
        return ret;
    }

    private static void ZipTo(Dictionary<string,string> fileList,string outputFile)
    {
        if (System.IO.File.Exists(outputFile)) { System.IO.File.Delete(outputFile);}
        if (!System.IO.File.Exists(System.IO.Path.GetDirectoryName(outputFile))) { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputFile)); };
        using (FileStream zipFile = new FileStream(outputFile, FileMode.Create))
        {
            using (ZipArchive zipArchive = new ZipArchive(zipFile, ZipArchiveMode.Create))
            {
                foreach (var kv in fileList)
                {
                    if (File.Exists(kv.Value))
                    {
                        ZipArchiveEntry entry = zipArchive.CreateEntry(kv.Key);
                        using (Stream sourceStream = new FileStream(kv.Value, FileMode.Open))
                        {
                            using (Stream destinationStream = entry.Open())
                            {
                                sourceStream.CopyTo(destinationStream);
                            }
                        }
                    }
                }
            }
        }
    }
}