using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TCP_Multiple_Client_Server_Accept_Multiple_Client
{
    public partial class MainFrame : Form
    {
        public Server tcp_Server = null;

        public MainFrame()
        {
            InitializeComponent();
            OpenServer();   //open server
        }

        private void OpenServer()
        {
            tcp_Server = new Server();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int clientListCount = tcp_Server.ClientListCount();
            textBox1.AppendText(clientListCount.ToString() + " Clients is Connected");
        }
    }
}
