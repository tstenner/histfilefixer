using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using IWshRuntimeLibrary;
using OpenMcdf;
using static System.IO.Path;
using File = System.IO.File;

namespace HistFileFixer
{
    public class HistFileFixer
    {
        public static void SetHistFileDataPaths(string s, string rawpath, string headerpath = null)
        {
            var hf = new CompoundFile(s, CFSUpdateMode.Update, CFSConfiguration.Default);
            SetPaths(hf, "DataPath", rawpath);
            if (headerpath != null) SetPaths(hf, "HeaderPath", headerpath);
            hf.Commit();
            hf.Close();
        }

        private static void SetPaths(CompoundFile f, string path, string value)
        {
            var newpath = Encoding.UTF8.GetBytes(value).Append((byte) 0).ToArray();
            f.RootStorage.GetStream(path).SetData(newpath);
            f.RootStorage.GetStream(path + 'W').SetData(newpath);
        }

        private static string GetRawFilepath(string vhdr)
        {
            var datafileline = File.ReadLines(vhdr).First(line => line.StartsWith("DataFile"));
            return GetDirectoryName(vhdr) + DirectorySeparatorChar + datafileline.Split('=').Skip(1).First().Trim();
        }

        public static string WarnIfMultiple(string key, string[] list)
        {
            string res = list.First();
            if (list.Length > 1)
                MessageBox.Show($"Found {list.Length} files for {key}:\n{string.Join("\n",list)}\n\nSelecting {res}");
            return res;
        }

        public static Dictionary<string, string> EnumerateHeaders(string datapath)
        {
            var shell = new WshShell();
            var headerpaths =
                Directory.GetFiles(datapath, "*.lnk", SearchOption.AllDirectories)
                    .Select(link => ((IWshShortcut) shell.CreateShortcut(link)).TargetPath)
                    .Where(link=>link.EndsWith(".vhdr"))
                    .Concat(Directory.GetFiles(datapath, "*.vhdr", SearchOption.AllDirectories)
                        .Select(GetFullPath))
                    .GroupBy(GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(el => el.Key, el => WarnIfMultiple(el.Key + ".vhdr", el.Distinct().ToArray()));
            return headerpaths;
        }

        public static Dictionary<string, string> RawPathsFromHeaders(Dictionary<string, string> headerpaths)
        {
            var rawfilepaths = headerpaths.ToDictionary(path => path.Key,
                path => GetRawFilepath(path.Value));
            return rawfilepaths;
        }

        public static void FixupWorkspace(string workspace)
        {
            var doc = new XmlDocument();
            doc.Load(workspace);
            Console.WriteLine(doc.InnerXml);
            Console.Out.WriteLine(doc.SelectSingleNode("Workspace")?.InnerXml);
            var rawpath = doc.SelectSingleNode("Workspace/RawFilePath")?.InnerText;
            var histpath = doc.SelectSingleNode("Workspace/HistoryFilePath")?.InnerText;
            Console.WriteLine(rawpath);
            Console.WriteLine(histpath);
            FixupWorkspace(rawpath, histpath);
        }

        public static void FixupWorkspace(string rawpath, string histpath)
        {
            var headers = EnumerateHeaders(rawpath);
            var raws = RawPathsFromHeaders(headers);
            Console.WriteLine(string.Join(Environment.NewLine,
                headers.Concat(raws).Select(kvp => kvp.Key + ": " + kvp.Value.ToString())));
            foreach (var histfile in Directory.GetFiles(histpath, "*.ehst2"))
            {
                var basename = GetFileNameWithoutExtension(histfile);
                if (!headers.TryGetValue(basename, out var header))
                    Console.Out.WriteLine("No header found for " + basename);
                if (!raws.TryGetValue(basename, out var raw))
                {
                    Console.Out.WriteLine("No raw file found for " + basename);
                    continue;
                }

                Console.WriteLine($"{basename} -> {raw}, {header}");
                SetHistFileDataPaths(histfile, raws[basename], header);
            }
        }
    }
}