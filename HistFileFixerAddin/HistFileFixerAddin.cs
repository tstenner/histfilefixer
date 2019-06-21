using System;
using System.Collections.Generic;
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
                string datapath = ws.RawFileFolder;
                var pb = app.CreateProgressBar("Fixing data files", $"Finding data files in {datapath}...");

                var headers = HistFileFixer.EnumerateHeaders(datapath);
                var raws = HistFileFixer.RawPathsFromHeaders(headers);


                pb.Text = "Fixing data files";
                pb.SetRange(0, app.HistoryFiles.Length);
                pb.Position = 1;
                pb.CancelEnabled = true;
                foreach (var hf in app.HistoryFiles)
                {
                    if (pb.UserCanceled) break;
                    pb.Text = hf.Name;
                    hf.Open();
                    var needed = !hf[0].DataAvailable;
                    hf.Close();
                    if (needed)
                        try
                        {
                            var basename = hf.Name;
                            if (!headers.TryGetValue(basename, out var header))
                                app.Warning("No header found for " + basename);
                            if (!raws.TryGetValue(basename, out var raw))
                            {
                                app.Error("No raw file found for " + basename);
                                continue;
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
                return;
            }
        }

        public object GetInfo(string sInfo, object other)
        {
            switch (sInfo)
            {
                case ComponentInfos.MenuText:
                    return "Fix Data Paths";
                case ComponentInfos.HelpText:
                    return "Try to find the header / data files again";
                case ComponentInfos.AutomationName:
                    return "none";
                case ComponentInfos.WindowTitle:
                    return "Superfenstertitel";
                case ComponentInfos.Visible:
                    return true;
                case ComponentInfos.AddInCategory:
                    return "Superkategorie";
                default:
                    return "";
            }
        }

        public bool InitContext(IAnalyzerContext context)
        {
            return true;
        }
    }
}