using System;
using System.Windows.Forms;

namespace CoolRanch
{
    public partial class ConnectForm : Form
    {
        SessionInfoExchanger _broker;

        public ConnectForm(SessionInfoExchanger broker)
        {
            _broker = broker;
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
            _broker.ConnectFromScratch(HostnameTextBox.Text, int.Parse(PortTextBox.Text));
        }
    }
}
