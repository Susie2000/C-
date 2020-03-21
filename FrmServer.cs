using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace Server
{
    public partial class FrmServer : Form
    {
        internal static Hashtable clients = new Hashtable();//clients数组保存当前在线用户的Client对象
        private TcpListener listener;//该服务器默认的监听端口号
        static int MaxNum = 100;//服务器可以支持的客户端最大连接数
        internal static bool ServiceFlag = false;//开始服务的标志
        public FrmServer()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }
        //服务器监听的端口号通过getValidPort()函数获得
        private int getValidPort(string port)
        {
            int lport;
            //测试端口号是否有效
            try
            {
                //是否为空
                if (port == "")
                {
                    throw new ArgumentException("端口号为空，不能启动服务器");
                }
                lport = System.Convert.ToInt32(port);
            }
            catch (Exception e)
            {
                Console.WriteLine("无效的端口号：" + e.ToString());
                this.rtbMessage.AppendText("无效的端口号：" + e.ToString() + "\n");
                return -1;
            }
            return lport;
        }
        private void FrmServer_Load(object sender, EventArgs e)
        {
            //固定为本机IP
            txtIPAddress.AppendText("192.168.213.1");
            
        }

        public void RC()
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint point = new IPEndPoint(IPAddress.Parse("192.168.213.1"), 10000);
            sock.Bind(point);
            sock.Listen(10);

            while (true)
            {
                Socket client = sock.Accept();
                byte[] bitLen = new byte[8];
                client.Receive(bitLen, bitLen.Length, SocketFlags.None);
                //第一步接收文件的大小
                long contentLen = BitConverter.ToInt64(bitLen, 0);
                int size = 0;
                MemoryStream ms = new MemoryStream();
                //循环接收文件的内容,如果接收完毕,则break;
                while (size < contentLen)
                {
                    //分多次接收,每次接收256个字节,
                    byte[] bits = new byte[256];
                    int r = client.Receive(bits, bits.Length, SocketFlags.None);
                    if (r <= 0) break;
                    ms.Write(bits, 0, r);
                    size += r;
                }
                client.Close();
                //接收到就显示,然后关闭当前连接,继续监听
                wangle(ms);
            }

        }
        public void wangle(MemoryStream ms)
        {
            Image img = Image.FromStream(ms);
            picBox.Image = null;
            picBox.Image = img;
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            //确认端口号是有效的，根据TCP协议，范围应该在-65535之间
            int port = getValidPort(txtPort.Text);
            if (port < 0)
            {
                return;
            }

            string ip = txtIPAddress.Text;
            try
            {
                IPAddress ipAdd = IPAddress.Parse(ip);
                listener = new TcpListener(ipAdd, port);//创建服务器套接字
                listener.Start(); //开始监听服务器端口
                this.rtbMessage.Text = "";
                this.rtbMessage.AppendText("Socket服务器已经启动!\n正在监听"
                    + ip + "\n端口号：" + this.txtPort.Text + "\n");
                //启动一个新的线程，执行方法this.StartSocketListen,
                //以便在一个独立的进程中执行确认与客户端Socket连接的操作
                FrmServer.ServiceFlag = true;
                Thread thread = new Thread(new ThreadStart(this.StartSocketListen));
                thread.Start();
                this.btnStart.Enabled = false;
                this.btnStop.Enabled = true;

                //接收文件
                Thread th = new Thread(new ThreadStart(RC));
                th.Start();
                th.Join();
            }
            catch (Exception ex)
            {
                this.rtbMessage.AppendText(ex.Message.ToString() + "\n");
            }
        }
        //在新的线程中的操作，它主要用于当接收到一个客户端请求时，确认与客户端的链接
        //并且立刻启动一个新的线程来处理和该客户端的信息交互

        private void StartSocketListen()
        {
            while (FrmServer.ServiceFlag)
            {
                try
                {
                    if (listener.Pending())
                    {
                        Socket socket = listener.AcceptSocket();
                        if (clients.Count >= MaxNum)
                        {
                            this.rtbMessage.AppendText("已经达到了最大连接数：" + MaxNum + ",拒绝新的连接\n");
                            socket.Close();
                        }
                        else
                        {
                            //启动一个新的线程，执行方法this.ServiceClient，处理用户相应的请求
                            Client client = new Client(this, socket);
                            Thread clientService = new Thread(new ThreadStart(client.ServiceClient));
                            clientService.Start();
                        }
                    }
                    //这句话能使系统性能大大提高
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    this.rtbMessage.AppendText(ex.Message.ToString() + "\n");
                }
            }
        }

        private void txtPort_TextChanged(object sender, EventArgs e)
        {
            if (this.txtPort.Text != "")
            {
                this.btnStart.Enabled = true;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            FrmServer.ServiceFlag = false;
            Thread.Sleep(300);
            rtbMessage.Text += txtIPAddress.Text + "的服务已经停止!" + "\r\n";

            //204. 控制按钮的可用性
            this.btnStart.Enabled = true;
            this.btnStop.Enabled = false;
        }
        public void AddUser(string username)
        {
            this.rtbMessage.AppendText(username + "已经加入\n");//将刚连接的用户加入到当前在向用户列表中
            this.userlist.Items.Add(username);
            this.usernum.Text = Convert.ToString(clients.Count);
        }
        public void RemoveUser(string username)
        {
            this.rtbMessage.AppendText(username + "已经离开\n");//将刚连接的用户加入到当前在向用户列表中
            this.userlist.Items.Remove(username);
            this.usernum.Text = Convert.ToString(clients.Count);
        }
        public string GetUserList()
        {
            string rtn = "";
            for (int i = 0; i < userlist.Items.Count; i++)
            {
                rtn += userlist.Items[i].ToString() + "|";//必须有|，否则更新在线或用户失败
            }
            return rtn;

        }
        public void updateUI(string msg)
        {
            this.rtbMessage.AppendText(msg + "\n");
        }
        private void FrmServer_FormClosing(object sender, FormClosingEventArgs e)
        {
            FrmServer.ServiceFlag = false;
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
        public void SetPicMsg(string msg)
        {
            this.label6.Text = msg;
        }
    }
}
