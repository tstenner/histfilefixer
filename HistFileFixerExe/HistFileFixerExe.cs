using System;
using System.Linq;
using System.Windows.Forms;

namespace HistFileFixerExe
{
    internal class HistFileFixerExe
    {
        private static string Getfile(string filter)
        {
            Console.WriteLine("Opening a box to get the file");
            var fd = new OpenFileDialog { InitialDirectory = "C:\\", Filter = filter, CheckFileExists = true };
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
                var hff = new HistFileFixer.HistFileFixer((title, message)=>
                MessageBox.Show(title, message) == DialogResult.OK);
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