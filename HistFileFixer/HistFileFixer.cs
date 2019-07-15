using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using OpenMcdf;
using static System.IO.Path;
using File = System.IO.File;

namespace HistFileFixer
{
	public delegate bool AskOk(string message);

	public delegate string SelectFromMultiple(string key, string[] list);
	public class HistFileFixer
	{
		public delegate string AskForFile(string path, string filter);

		private static string ShortcutTarget(string shortcutpath) => ShellLink.Shortcut.ReadFromFile(shortcutpath).LinkTargetIDList.Path;

		public void SetHistFileDataPaths(string historyfile, string rawpath, string headerpath = null)
		{
			if (!historyfile.EndsWith(".ehst2")) throw new ArgumentException($"History file {historyfile} extension isn't .ehst2");
			var hf = new CompoundFile(historyfile, CFSUpdateMode.Update, CFSConfiguration.Default);
			SetPaths(hf, "DataPath", rawpath);
			if (headerpath != null) SetPaths(hf, "HeaderPath", headerpath);
			hf.Commit();
			hf.Close();
			Console.WriteLine($"Wrote new paths to {historyfile}");
		}

		private void SetPaths(CompoundFile f, string path, string value)
		{
			var newpath = Encoding.UTF8.GetBytes(value).Append((byte)0).ToArray();
			f.RootStorage.GetStream(path).SetData(newpath);
			f.RootStorage.GetStream(path + 'W').SetData(newpath);
		}

		private string GetRawFilepath(string vhdr)
		{
			var datafileline = File.ReadLines(vhdr).First(line => line.StartsWith("DataFile"));
			return GetDirectoryName(vhdr) + DirectorySeparatorChar + datafileline.Split('=').Skip(1).First().Trim();
		}

		private string SelectFromMultiple(string key, string[] list)
		{
			if (list.Length == 1) return list[0];
			return SelectFromMultipleFn.Invoke(key, list);
		}

		public Dictionary<string, string> EnumerateHeaders(string datapath)
		{
			var headerpaths =
				Directory.GetFiles(datapath, "*.lnk", SearchOption.AllDirectories)
					.Select(ShortcutTarget)
					.Where(link => link.EndsWith(".vhdr"))
					.Concat(Directory.GetFiles(datapath, "*.vhdr", SearchOption.AllDirectories)
						.Select(GetFullPath))
					.GroupBy(GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
					.ToDictionary(el => el.Key, el => SelectFromMultiple(el.Key + ".vhdr", el.Distinct().ToArray()));
			return headerpaths;
		}

		public Dictionary<string, string> RawPathsFromHeaders(Dictionary<string, string> headerpaths)
		{
			var rawfilepaths = headerpaths.ToDictionary(path => path.Key,
				path => GetRawFilepath(path.Value));
			return rawfilepaths;
		}

		public void LoadWorkspace(string workspace, out string rawpath, out string historypath)
		{
			var doc = new XmlDocument();
			doc.Load(workspace);
			var wksp = doc.SelectSingleNode("Workspace");
			if (wksp == null) throw new ArgumentException("Workspace node not found");
			var rawfilepathxml = wksp.SelectSingleNode("RawFilePath");
			var histpathxml = wksp.SelectSingleNode("HistoryFilePath");
			if (rawfilepathxml == null) throw new ArgumentException("<RawFilePath> not found");
			if (histpathxml == null) throw new ArgumentException("<HistoryFilePath> not found");
			rawpath = rawfilepathxml.InnerText;
			historypath = histpathxml.InnerText;
		}

		public void FixupWorkspace(string rawpath, string histpath, bool dryrun = false)
		{
			foreach (var (histfile, header, rawfile) in MatchHistFiles(rawpath, Directory.GetFiles(histpath, "*.ehst2")))
			{
				var basename = GetFileNameWithoutExtension(histfile);
				Console.WriteLine($"Match: {basename} -> {rawfile ?? "???"}, {header ?? "???"}");
				if (rawfile == null)
				{
					if (AskOkFn.Invoke($"No data file found for {basename}. Continue?"))
						continue;
					return;
				}

				if (dryrun) continue;
				if (AskOkFn.Invoke($"Write changes to file {basename}?"))
					SetHistFileDataPaths(histfile, rawfile, header);
			}
		}
		public (string histfile, string header, string rawfile)[] MatchHistFiles(string datapath, IEnumerable<string> histfiles)
		{
			var headers = EnumerateHeaders(datapath);
			var raws = RawPathsFromHeaders(headers);

			return histfiles.Select(histfile =>
			{
				var basename = GetFileNameWithoutExtension(histfile);
				if (!headers.TryGetValue(basename ?? throw new InvalidOperationException($"Couldn't determine filename for {histfile}"), out var header))
					Console.Out.WriteLine("No header found for " + histfile);
				if (!raws.TryGetValue(basename, out var rawfile))
					Console.Out.WriteLine("No raw file found for " + histfile);
				return (histfile, header, rawfile);
			}).ToArray();
		}
		public AskOk AskOkFn;
		public SelectFromMultiple SelectFromMultipleFn;
	}
}
