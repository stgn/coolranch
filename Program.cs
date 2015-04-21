using System;
using System.Windows.Forms;

namespace CoolRanch
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var game = new ElDorado();
            var broker = new SessionInfoExchanger(game);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CoolRanchContext(game, broker));
        }
    }
}
