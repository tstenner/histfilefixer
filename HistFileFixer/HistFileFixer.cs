using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using OpenMcdf;

namespace HistFileFixer
{
	[Serializable]
	public class Workspace
	{
		[XmlElement()]
		public string RawFilePath { get; set; }
		[XmlElement()]
		public string HistoryFilePath { get; set; }
		[XmlElement()]
		public string ExportPath { get; set; }
	}

	public delegate bool AskOk(string message);

	public delegate string SelectFromMultiple(string key, string[] list);

	public class HistFileFixer
	{
		public delegate string AskForFile(string path, string filter);

		// Get the target for a lnk file (https://blez.wordpress.com/2013/02/18/get-file-shortcuts-target-with-c#)
		private string ShortcutTarget(string file)
		{
			if (Path.GetExtension(file).ToLower() != ".lnk")
				throw new Exception("Supplied file must be a .LNK file");

			var fileStream = File.Open(file, FileMode.Open, FileAccess.Read);
			using var fileReader = new BinaryReader(fileStream);
			fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
			if ((fileReader.ReadUInt32() & 1) == 1)
			{
				// Bit 1 set means we have to skip the shell item ID list
				// Seek to the end of the header
				fileStream.Seek(0x4c, SeekOrigin.Begin);
				// Read the length of the Shell item ID list
				// Seek past it (to the file locator info)
				fileStream.Seek(fileReader.ReadUInt16(), SeekOrigin.Current);
			}

			// Store the offset where the file info structure begins
			var fileInfoStartsAt = fileStream.Position;
			// read the length of the whole struct
			var totalStructLength = fileReader.ReadUInt32();

			// seek to offset to base pathname
			fileStream.Seek(0xc, SeekOrigin.Current);
			// read offset to base pathname the offset is from the beginning of the file info struct (fileInfoStartsAt)
			var fileOffset = fileReader.ReadUInt32();
			// Seek to beginning of base pathname (target)
			fileStream.Seek(fileInfoStartsAt + fileOffset, SeekOrigin.Begin);
			var pathLength = totalStructLength + fileInfoStartsAt - fileStream.Position;
			// read the base pathname. I don't need the 2 terminating nulls.
			var link = new string(fileReader.ReadChars((int)pathLength));
			var parts = link.Split('\0').Where(substring=>substring.Length!=0).ToArray();

			if (parts.Length == 1) return parts[0];
			else if (parts[parts.Length - 2].StartsWith("\\\\")) return parts.First() + parts.Last();
			else return parts[parts.Length - 2] + '\\' + parts.Last();
		}

		public static void SetHistFileDataPaths(string historyfile, string rawpath, string headerpath = null)
		{
			if (Path.GetExtension(historyfile) != ".ehst2") throw new ArgumentException($"History file {historyfile} extension isn't .ehst2");
			var hf = new CompoundFile(historyfile, CFSUpdateMode.Update, CFSConfiguration.Default);
			SetPaths(hf, "DataPath", rawpath);
			if (headerpath != null) SetPaths(hf, "HeaderPath", headerpath);
			hf.Commit();
			hf.Close();
			Console.WriteLine($"Wrote new paths to {historyfile}");
		}

		private static void SetPaths(CompoundFile f, string path, string value)
		{
			var newpath = Encoding.UTF8.GetBytes(value).Append((byte)0).ToArray();
			f.RootStorage.GetStream(path).SetData(newpath);
			f.RootStorage.GetStream(path + 'W').SetData(newpath);
		}

		private static string GetRawFilepath(string vhdr)
		{
			var datafileline = File.ReadLines(vhdr).First(line => line.StartsWith("DataFile"));
			return Path.GetDirectoryName(vhdr) + Path.DirectorySeparatorChar + datafileline.Split('=').Skip(1).First().Trim();
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
					.Where(link => Path.GetExtension(link) == ".vhdr")
					.Concat(Directory.GetFiles(datapath, "*.vhdr", SearchOption.AllDirectories)
						.Select(Path.GetFullPath))
					.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
					.ToDictionary(el => el.Key, el => SelectFromMultiple(el.Key + ".vhdr", el.Distinct().ToArray()));
			return headerpaths;
		}

		public static Dictionary<string, string> RawPathsFromHeaders(Dictionary<string, string> headerpaths)
		{
			var rawfilepaths = headerpaths.ToDictionary(path => path.Key,
				path => GetRawFilepath(path.Value));
			return rawfilepaths;
		}

		public static void LoadWorkspace(string workspace, out string rawpath, out string historypath)
		{
			var ser = new XmlSerializer(typeof(Workspace));
			using var f = new StreamReader(workspace);
			var wksp = (Workspace)ser.Deserialize(f);

			rawpath = wksp.RawFilePath;
			historypath = wksp.HistoryFilePath;
		}

		public void FixupWorkspace(string rawpath, string histpath, bool dryrun = false)
		{
			foreach (var (histfile, header, rawfile) in MatchHistFiles(rawpath, Directory.GetFiles(histpath, "*.ehst2")))
			{
				var basename = Path.GetFileNameWithoutExtension(histfile);
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
				var basename = Path.GetFileNameWithoutExtension(histfile);
				if (!headers.TryGetValue(basename ?? throw new InvalidOperationException($"Couldn't determine filename for {histfile}"), out var header))
					Console.Out.WriteLine("No header found for " + histfile);
				if (!raws.TryGetValue(basename, out var rawfile))
					Console.Out.WriteLine("No raw file found for " + histfile);
				return (histfile, header, rawfile);
			}).ToArray();
		}
		public AskOk AskOkFn { get; set; }
		public SelectFromMultiple SelectFromMultipleFn { get; set; }
	}
}
