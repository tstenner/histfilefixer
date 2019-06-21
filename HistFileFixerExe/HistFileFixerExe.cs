using System;
using System.Linq;
using System.Windows.Forms;

namespace HistFileFixer
{
    internal class HistFileFixerExe
    {
        private static string Getfile(string filter)
        {
            Console.WriteLine("Opening a box to get the file");
            var fd = new OpenFileDialog {InitialDirectory = "C:\\", Filter = filter, CheckFileExists = true};
            while (fd.ShowDialog() != DialogResult.OK)
            {
            }
            return fd.FileName;
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0) args.Append(Getfile("Analyzer Workspace files|*.wksp2"));
                switch (args.Length)
                {
                    case 1:
                        HistFileFixer.FixupWorkspace(args[0]);
                        break;
                    case 2:
                        HistFileFixer.SetHistFileDataPaths(args[0], args[1]);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}