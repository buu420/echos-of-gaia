using System;
using System.Windows.Forms;

namespace EchosOfGaia.Installer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new InstallerForm(AppDomain.CurrentDomain.BaseDirectory,
                    SteamLocator.FindGameFolder(SteamLocator.GetSteamRoots())));
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Echos of Gaia installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
