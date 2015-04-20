using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoolRanch
{
    static class Program
    {
        static bool AllowConnections, AnnounceSession;
        static ConnectForm ConnectForm;
        static SessionInfoExchanger SIExchanger;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var ConnectItem = new ToolStripMenuItem("Connect");
            ConnectItem.Click += (s, e) => { ConnectForm.Show(); };

            var ExitItem = new ToolStripMenuItem("Exit");
            ExitItem.Click += (s, e) => { Environment.Exit(0); };

            var AllowConnectionsItem = new ToolStripMenuItem("Allow connections") { Checked = AllowConnections, CheckOnClick = true };
            var AnnounceSessionItem = new ToolStripMenuItem("Announce to master") { Checked = AnnounceSession, CheckOnClick = true };

            AllowConnectionsItem.CheckedChanged += (s, e) =>
            {
                var check = (s as ToolStripMenuItem).Checked;
                AllowConnections = check;
                AnnounceSessionItem.Enabled = check;
            };

            AnnounceSessionItem.CheckedChanged += (s, e) =>
            {
                AnnounceSession = (s as ToolStripMenuItem).Checked;
            };

            var ni = new NotifyIcon()
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items = {
                        ConnectItem,
                        //AllowConnectionsItem,
                        //AnnounceSessionItem,
                        new ToolStripSeparator(),
                        ExitItem
                    }
                }
            };

            var Game = new ElDorado();
            Game.MonitorProcesses();

            SIExchanger = new SessionInfoExchanger(Game);

            ConnectForm = new ConnectForm(SIExchanger);

            new Thread(SIExchanger.ReceiveLoop).Start();
            Application.Run();
            ni.Visible = false;
        }
    }
}
