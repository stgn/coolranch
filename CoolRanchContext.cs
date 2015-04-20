using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoolRanch
{
    class CoolRanchContext : ApplicationContext
    {
        private ElDorado _game;
        private SessionInfoExchanger _broker;
        private ConnectForm _connectForm;

        private NotifyIcon _trayIcon;

        private ToolStripMenuItem _connectItem, _exitItem;

        public CoolRanchContext(ElDorado game, SessionInfoExchanger broker)
        {
            _game = game;
            _broker = broker;
            _connectForm = new ConnectForm(_broker);

            _game.ProcessLaunched += _game_ProcessLaunched;
            _game.ProcessClosed += _game_ProcessClosed;
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

            InitializeComponent();
            _trayIcon.Visible = true;
            UpdateTray(false);
            _game.MonitorProcesses();
        }

        void UpdateTray(bool gameIsRunning)
        {
            var parent = _trayIcon.ContextMenuStrip;
            if (parent.InvokeRequired)
            {
                parent.Invoke(new Action<bool>(UpdateTray), new object[] { gameIsRunning });
            }
            else
            {
                _trayIcon.Text = String.Format("CoolRanch (game {0}running)", gameIsRunning ? "" : "not ");
                _connectItem.Enabled = gameIsRunning;
                _connectForm.Visible = gameIsRunning && _connectForm.Visible;
            }
        }

        void _game_ProcessLaunched(object sender, EventArgs e)
        {
            UpdateTray(true);
        }

        void _game_ProcessClosed(object sender, EventArgs e)
        {
            UpdateTray(false);
        }

        private void InitializeComponent()
        {
            _connectItem = new ToolStripMenuItem("Connect");
            _connectItem.Click += connectItem_Click;

            _exitItem = new ToolStripMenuItem("Exit");
            _exitItem.Click += exitItem_Click;

            _trayIcon = new NotifyIcon()
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items = {
                        _connectItem,
                        new ToolStripSeparator(),
                        _exitItem
                    }
                }
            };
        }

        void connectItem_Click(object sender, EventArgs e)
        {
            _connectForm.Show();
        }

        void exitItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Environment.Exit(0);
        }
    }
}
