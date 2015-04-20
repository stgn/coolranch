using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace CoolRanch
{
    public partial class ConnectForm : Form
    {
        SessionInfoExchanger SIExchanger;

        public ConnectForm(SessionInfoExchanger six)
        {
            SIExchanger = six;
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void ConnectForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            var addresses = Dns.GetHostAddresses(HostnameTextBox.Text);
            SIExchanger.SendChallengeRequest(
                new IPEndPoint(Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork), short.Parse(PortTextBox.Text)));
        }
    }
}
