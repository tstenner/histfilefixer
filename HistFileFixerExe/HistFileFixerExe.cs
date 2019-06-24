using System;
using CommandLine;
#if NET472
using System.Windows.Forms;
#endif

namespace HistFileFixerExe
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
#if NET472
        private static string Getfile(string filter)
        {
            Console.WriteLine("Opening a box to get the file");
            var fd = new OpenFileDialog { InitialDirectory = "C:\\", Filter = filter, CheckFileExists = true };
            while (fd.ShowDialog() != DialogResult.OK)
            {
            }
            return fd.FileName;
        }
        /*private static bool AskOk(string message) {
            return MessageBox.Show(message, "", MessageBoxButtons.OKCancel) == DialogResult.OK;
        }*/
#else
        private static string Getfile(string filter)
        {
            Console.WriteLine($"No file found on the command line, please input a path to a file ({filter}) here:");
            return Console.ReadLine().Trim();
        }

#endif
        private static bool AskOk(string message)
        {
            do
            {
                Console.Write($"{message} [Y/n]: ");
                int raw = Console.Read();
                char ch = Char.ToLower(Convert.ToChar(raw));
                if (raw == '\n' || ch == 'y') return true;
                if (ch == 'n') return false;
            } while (true);
        }

        private static string SelectFromMultiple(string key, string[] list)
        {
            Console.WriteLine($"Found multiple files for {key}:");

            for (int i = 0; i < list.Length; i++)
                Console.WriteLine($"{i + 1}: {list[i]}");

            int choice;
            do Console.Write($"Please enter your choice (1-{list.Length})");
            while (!Int32.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > list.Length);
            return list[choice - 1];
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0) args = new[] { Getfile("Analyzer Workspace files|*.wksp2") };
                Parser.Default.ParseArguments<CmdLineOptions>(args)
                    .WithParsed(options =>
                    {
                        var hff = new HistFileFixer.HistFileFixer
                        {
                            AskOkFn = options.Yes
                                ? (HistFileFixer.AskOk)(message =>
                                {
                                    Console.WriteLine($"Asked '{message}', assuming yes (--yes)");
                                    return true;
                                })
                                : AskOk,
                            SelectFromMultipleFn = SelectFromMultiple
                        };
                        hff.LoadWorkspace(options.WorkspaceFile, out var rawpath, out var historypath);
                        hff.FixupWorkspace(rawpath, historypath, options.DryRun);
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
