using System;
using CommandLine;
using OpenMcdf;
using System.Text;
using System.IO;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif

namespace HistFileFixer
{
	[Verb("fixup", HelpText = "Fix paths to data files in history files")]
	internal class CmdLineOptions
	{
		[Value(0, HelpText = "Path to a .wksp2 workspace file", MetaName = "workspace file")]
		public string WorkspaceFile { get; set; }

		[Option(Default = false, HelpText = "Only print what would be done, but done change any files")]
		public bool DryRun { get; set; }

		[Option(Default = false, HelpText = "Assume Yes")]
		public bool Yes { get; set; }
	}
	internal class HistFileFixerExe
	{
#if NETFRAMEWORK
		private static string GetFile(string filter)
		{
			Console.WriteLine("Opening a box to get the file");
			using var fd = new OpenFileDialog { InitialDirectory = "C:\\", Filter = filter, CheckFileExists = true };
			while (fd.ShowDialog() != DialogResult.OK) { }
			return fd.FileName;
		}

		private static bool AskOk(string message)
		{
			return MessageBox.Show(message, "", MessageBoxButtons.OKCancel) == DialogResult.OK;
		}
#else
		private static string GetFile(string filter)
		{
			Console.WriteLine($"No file found on the command line, please input a path to a file ({filter}) here:");
			return Console.ReadLine().Trim();
		}

		private static bool AskOk(string message)
		{
			do
			{
				Console.Write($"{message} [Y/n]: ");
				var raw = Console.Read();
				var ch = char.ToLower(Convert.ToChar(raw));
				if (raw == '\n' || ch == 'y') return true;
				if (ch == 'n') return false;
			} while (true);
		}
#endif

		private static string SelectFromMultiple(string key, string[] list)
		{
			Console.WriteLine($"Found multiple files for {key}:");

			for (var i = 0; i < list.Length; i++)
				Console.WriteLine($"{i + 1}: {list[i]}");

			int choice;
			do Console.Write($"Please enter your choice (1-{list.Length})");
			while (!int.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > list.Length);
			return list[choice - 1];
		}

		[STAThread]
		private static void Main(string[] args)
		{
			if (args.Length == 0) args = new[] { GetFile("Analyzer Workspace files|*.wksp2") };
			Parser.Default.ParseArguments<CmdLineOptions>(args)
				.WithParsed(options =>
				{
					var hff = new HistFileFixer
					{
						AskOkFn = options.Yes
							? (AskOk)(message =>
							{
								Console.WriteLine($"Asked '{message}', assuming yes (--yes)");
								return true;
							})
							: AskOk,
						SelectFromMultipleFn = SelectFromMultiple
					};
					HistFileFixer.LoadWorkspace(options.WorkspaceFile, out var rawpath, out var historypath);
					hff.FixupWorkspace(rawpath, historypath, options.DryRun);
				});
		}
	}
}
