using System;
using System.Linq;
using BrainVision.AnalyzerAutomation;
using BrainVision.Interfaces;

namespace HistFileFixer
{
	[AddIn("{8cf0d017-e7dc-4c20-90cb-b42c0a692c59}", "Data Path Fixer", "Fix the data paths", 0, 1000000)]
	public class HistFileFixerAddin : IAnalyzerAddIn
	{
		public void Execute()
		{
			var app = AutomationSupport.Application;
			try
			{
				IWorkspace ws = app.CurrentWorkspace;
				var datapath = ws.RawFileFolder;
				var pb = app.CreateProgressBar("Fixing data files", $"Finding data files in {datapath}...");

				var hff = new HistFileFixer
				{
					AskOkFn = message => app.AskOKCancel(message) == MessageButton.OK,
					SelectFromMultipleFn = (key, list) =>
					{
						var res = list.First();
						if (list.Length == 1) return res;
						return app.AskYesNo(
								   $"Found {list.Length} files for {key}:\n{string.Join("\n", list)}\n\nSelecting {res}.\n\nContinue?") ==
							   MessageButton.Yes ? res : null;
					}
				};
				var matches = hff.MatchHistFiles(datapath, app.HistoryFiles.Select(hf => hf.Name)).ToArray();

				pb.Text = "Fixing data files";
				pb.SetRange(0, app.HistoryFiles.Length);
				pb.Position = 1;
				pb.CancelEnabled = true;
				foreach (var (hf, basename, header, raw) in app.HistoryFiles.Zip(matches, (hf, match) => (hf, basename: match.histfile, match.header, raw: match.rawfile)))
				{
					if (pb.UserCanceled) break;
					pb.Text = basename;
					hf.Open();
					var needed = !hf[0].DataAvailable;
					hf.Close();
					if (needed)
						try
						{
							if (header == null)
								app.Warning("No header found for " + basename);
							if (raw == null)
							{
								app.Error("No raw file found for " + basename);
								break;
							}

							Console.WriteLine($"{basename} -> {raw}, {header}");
							if (app.AskYesNo($"Set paths for {hf.Name} to {header} / {raw}") == MessageButton.Yes)
								HistFileFixer.SetHistFileDataPaths(hf.FullName, raw, header);
						}
						catch (Exception e)
						{
							app.Error(e.ToString());
						}
					pb.Position++;
					if (pb.UserCanceled) break;
				}

				pb.Text = "Done";
				pb.Dispose();
			}
			catch (Exception e)
			{
				app.Error(e.Message);
			}
		}

		public object GetInfo(string sInfo, object other)
		{
			return sInfo switch
			{
				ComponentInfos.MenuText => "Fix Data Paths",
				ComponentInfos.HelpText => "Try to find the header / data files again",
				ComponentInfos.AutomationName => "none",
				ComponentInfos.WindowTitle => "Superfenstertitel",
				ComponentInfos.Visible => true,
				ComponentInfos.AddInCategory => "Superkategorie",
				_ => "",
			};
		}

		public bool InitContext(IAnalyzerContext context) => true;
	}
}
