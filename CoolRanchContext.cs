using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace CoolRanch
{
    class CoolRanchContext : ApplicationContext
    {
        private ElDorado _game;
        private SessionInfoExchanger _broker;

        private ConnectForm _connectForm;
        private BrowserForm _browserForm;

        private NotifyIcon _trayIcon;
        private ToolStripMenuItem _connectItem, _browseItem, _allowJoinsItem, _announceItem, _exitItem;

        private bool _gameRunning, _allowJoins, _announceSession;

        private Thread _announceThread;

        public CoolRanchContext(ElDorado game, SessionInfoExchanger broker)
        {
            _game = game;
            _broker = broker;

            _connectForm = new ConnectForm(_broker);
            _browserForm = new BrowserForm(_broker);

            new Thread(_broker.ReceiveLoop).Start();

            _game.ProcessLaunched += _game_ProcessLaunched;
            _game.ProcessClosed += _game_ProcessClosed;
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

            InitializeComponent();
            _trayIcon.Visible = true;
            UpdateState();
            _game.MonitorProcesses();
        }

        void UpdateState()
        {
            var parent = _trayIcon.ContextMenuStrip;
            if (parent.InvokeRequired)
            {
                parent.Invoke(new Action(UpdateState));
            }
            else
            {
                _trayIcon.Text = string.Format("CoolRanch (game {0}running)", _gameRunning ? "" : "not ");

                if (_gameRunning)
                {
                    _connectItem.Enabled = 
                        _browseItem.Enabled = 
                        _allowJoinsItem.Enabled = true;
                    _allowJoinsItem.Checked = _allowJoins;
                    _announceItem.Enabled = _allowJoins;
                    _announceItem.Checked = _announceSession;
                }
                else
                {
                    _connectItem.Enabled = 
                        _browseItem.Enabled = 
                        _allowJoinsItem.Enabled = 
                        _announceItem.Enabled = false;
                }
            }
        }

        void _game_ProcessLaunched(object sender, EventArgs e)
        {
            _gameRunning = true;
            UpdateState();
        }

        void _game_ProcessClosed(object sender, EventArgs e)
        {
            _gameRunning = false;
            UpdateState();
        }

        private void InitializeComponent()
        {
            _connectItem = new ToolStripMenuItem("Connect to session");
            _connectItem.Click += connectItem_Click;

            _browseItem = new ToolStripMenuItem("Browse public sessions");
            _browseItem.Click += _browseItem_Click;

            _allowJoinsItem = new ToolStripMenuItem("Allow join requests");
            _allowJoinsItem.CheckOnClick = true;
            _allowJoinsItem.CheckedChanged += _allowJoinsItem_CheckedChanged;

            _announceItem = new ToolStripMenuItem("Announce session");
            _announceItem.CheckOnClick = true;
            _announceItem.CheckedChanged += _announceItem_CheckedChanged;

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
                        _browseItem,
                        new ToolStripSeparator(),
                        _allowJoinsItem,
                        _announceItem,
                        new ToolStripSeparator(),
                        _exitItem
                    }
                }
            };

            _trayIcon.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                    mi.Invoke(_trayIcon, null);
                }
            };
        }

        void _browseItem_Click(object sender, EventArgs e)
        {
            _browserForm.Show();
        }

        void connectItem_Click(object sender, EventArgs e)
        {
            _connectForm.Show();
        }

        void _allowJoinsItem_CheckedChanged(object sender, EventArgs e)
        {
            _allowJoins = _allowJoinsItem.Checked;
            _broker.AllowJoins = _allowJoins;
            UpdateState();
        }

        void _announceItem_CheckedChanged(object sender, EventArgs e)
        {
            _announceSession = _announceItem.Checked;
            _broker.Announcing = _announceSession;
            if (_announceSession && (_announceThread == null || !_announceThread.IsAlive))
            {
                _announceThread = new Thread(_broker.AnnounceLoop);
                _broker.Announcing = true;
                _announceThread.Start();
            }
            else if (_announceThread.IsAlive)
            {
                _broker.Announcing = false;
            }
            UpdateState();
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
