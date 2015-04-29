using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace CoolRanch
{
    public partial class BrowserForm : Form
    {
        private SessionInfoExchanger _broker;

        public BrowserForm(SessionInfoExchanger broker)
        {
            _broker = broker;
            _broker.InfoResponseReceived += _broker_InfoResponseReceived;
            InitializeComponent();
        }

        void _broker_InfoResponseReceived(object sender, InfoResponseEventArgs e)
        {
            AddServerToList(new ServerListItem(e.Info) { EndPoint = e.EndPoint, Challenge = e.Challenge });
        }

        void AddServerToList(ServerListItem server)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ServerListItem>(AddServerToList), server);
            }
            else
            {
                ServerList.Items.Add(server);
            }
        }

        void DownloadList()
        {
            ServerList.Items.Clear();
            statusLabel.Text = "Downloading lobby list from master server...";

            var listdata = new WebClient().DownloadData("http://coolranch.ax.lt:8080/");
            var numServers = listdata.Length/6;

            if (numServers > 0)
            {
                statusLabel.Text = string.Format("Got {0} {1} from master server.", numServers,
                    numServers == 1 ? "lobby" : "lobbies");

                var ms = new MemoryStream(listdata);
                var reader = new BinaryReader(ms);

                var servers = new List<IPEndPoint>();
                while (ms.Position != ms.Length)
                {
                    servers.Add(new IPEndPoint(new IPAddress(reader.ReadBytes(4)), reader.ReadUInt16()));
                }
                new Thread(() => InterrogateGently(servers)).Start();
            }
            else
            {
                statusLabel.Text = "Master server lobby list is empty. Sorry.";
            }
        }

        void InterrogateGently(List<IPEndPoint> servers)
        {
            foreach (var server in servers)
            {
                _broker.ChallengeForInfo(server);
                Thread.Sleep(5);
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            DownloadList();
        }

        void ConnectToSelected(object sender, EventArgs e)
        {
            if (ServerList.SelectedItems.Count == 0)
                return;

            var selected = ServerList.SelectedItems[0] as ServerListItem;
            _broker.ConnectWithChallenge(selected.EndPoint, selected.Challenge);
        }

        private void CancelBrowseButton_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void BrowserForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

        private void BrowserForm_Shown(object sender, EventArgs e)
        {
            DownloadList();
        }
    }

    class ServerListItem : ListViewItem
    {
        public IPEndPoint EndPoint;
        public byte[] Challenge;

        public ServerListItem(Dictionary<string, object> info)
        {
            Text = info.ContainsKey("name") ? info["name"].ToString() : "<missing>";
            SubItems.AddRange(new string[]
            {
                info.ContainsKey("gametype") ? info["gametype"].ToString() : "<missing>", 
                info.ContainsKey("map") ? info["map"].ToString() : "<missing>", 
                "- / -"
            });
        }
    }
}
