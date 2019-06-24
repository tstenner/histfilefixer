using System;
using System.Linq;
#if NET472
using System.Windows.Forms;
#endif

namespace HistFileFixerExe
{
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
	private static bool AskOk(string title, string message) {
		return MessageBox.Show(title, message) == DialogResult.OK;
	}
#else
        private static string Getfile(string filter)
        {
            Console.WriteLine($"No file found on the command line, please input a path to a file ({filter}) here:");
            return Console.ReadLine().Trim();
        }

	private static bool AskOk(string title, string message) {
		do {
			Console.Write($"{title}\n{message} [Y/n]: ");
			int raw = Console.Read();
			char ch = Char.ToLower(Convert.ToChar(raw));
			if(raw=='\n' || ch == 'y') return true;
			if(ch=='n') return false;
		} while(true);
	}

#endif

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0) args.Append(Getfile("Analyzer Workspace files|*.wksp2"));
                var hff = new HistFileFixer.HistFileFixer(AskOk);
                if (args.Length == 1)
                    hff.FixupWorkspace(args[0]);
                else if (args.Length == 2) hff.SetHistFileDataPaths(args[0], args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
