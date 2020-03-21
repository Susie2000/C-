using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Client
{
    public partial class FrmClient : Form
    {
        TcpClient tcpClient;//与服务器的连接
        private NetworkStream Stream;//与服务器交互的流通道
        private static string CLOSED = "closed";
        private static string CONNECTED = "connected";
        private string state = "closed";
        private bool stopFlag;
        private Color color;//保存当前客户端显示的颜色

        //传送文件
        static Stream fs = null;
        int nameLen = 0;
        string name;
        long contentLen = 0;

        public FrmClient()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void FrmClient_Load(object sender, EventArgs e)
        {
            //IP与端口固定
            txtHost.AppendText("192.168.213.1");
            txtPort.AppendText("6666");
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (state == CONNECTED)
                return;
            if (this.username.TextLength == 0)
            {
                MessageBox.Show(" 请输入你的昵称！", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                this.username.Focus();//为控件设置焦点
                return;
            }
            try
            {
                //创建一个客户端套接字，他是Login的一个公共属性
                tcpClient = new TcpClient();

                tcpClient.Connect(IPAddress.Parse(txtHost.Text), Int32.Parse(txtPort.Text));//向指定的IP地址服务器发出连接请求
                Stream = tcpClient.GetStream(); //获得与服务器数据交互的流通道 NetworksStream
                //启动一个新的线程，执行方法this.ServerResponse()，以便来响应从服务器发回的信息
                Thread thread1 = new Thread(new ThreadStart(this.ServerResponse));
                thread1.Start();
                //向服务器发送CONN请求命令
                //此命令的格式与服务器端的定义的格式一致
                //命令格式为：命令标志符CONN|发送者的用户名
                string cmd = "CONN|" + this.username.Text + "|";
                //将字符串转化为字符数组
                Byte[] outbytes = System.Text.Encoding.Default.GetBytes(cmd.ToCharArray());
                Stream.Write(outbytes, 0, outbytes.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                if ((!this.cbPrivate.Checked) && (!this.cbMulti.Checked))            //多对多消息
                {
                    //此时命令的格式是：命令标识符CHAT|发送者的用户名：发送内容|
                    string message = "CHAT|" + this.username.Text + ":" + tbSendContent.Text;
                    tbSendContent.Text = "";
                    tbSendContent.Focus();
                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());    //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else if ((this.cbPrivate.Checked) && (!this.cbMulti.Checked)) //私聊
                {
                    if (lstUsers.SelectedItems.Count != 1)
                    {
                        MessageBox.Show("请在列表中选择一个用户", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    string receiver = lstUsers.SelectedItem.ToString();
                    //消息的格式是：命令标识符PRIV|发送者的用户名|接收者的用户名|发送内容

                    string message = "PRIV|{" + this.username.Text + "|" + receiver + "|" + tbSendContent.Text + "|";
                    tbSendContent.Text = "";
                    tbSendContent.Focus();

                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());   //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else if ((!this.cbPrivate.Checked) && (this.cbMulti.Checked))  //群聊
                {
                    if (lstUsers.SelectedItems.Count < 2)
                    {
                        MessageBox.Show("请在列表中选择至少两个用户", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    int i = 0;
                    string message;
                    string receivers = null;
                    for (i = 0; i < lstUsers.SelectedItems.Count; i++)
                    {
                        receivers += lstUsers.SelectedItems[i].ToString() + "*";//以“*”来分割用户名
                    }
                    receivers += "|" + lstUsers.SelectedItems.Count.ToString();
                    message = "MUTI|{" + this.username.Text + "|" + receivers + "|" + tbSendContent.Text + "|";
                    tbSendContent.Text = "";
                    tbSendContent.Focus();

                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());   //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else
                {
                    MessageBox.Show("不能同时选择‘私聊’和‘多人聊天’！", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }
            catch
            {
                this.rtbMsg.AppendText("网络发生错误！");
            }
        }
        //this.ServerResponse()方法用于接收从服务器发回的信息，根据不同的命令，执行相应的操作
        private void ServerResponse()
        {
            //定义一个byte数组，用于接收从服务器端发来的数据
            //每次所能接受的数据包的最大长度为1024个字节
            byte[] buff = new byte[1024];
            string msg;
            int len;
            try
            {
                if (Stream.CanRead == false)
                {
                    return;
                }
                stopFlag = false;
                while (!stopFlag)
                {
                    //从流中得到数据，并存入到buff字符数组中
                    len = Stream.Read(buff, 0, buff.Length);
                    if (len < 1)
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    //将字符数组转化为字符串
                    msg = System.Text.Encoding.Default.GetString(buff, 0, len);
                    msg.Trim();
                    string[] tokens = msg.Split(new char[] { '|' });
                    //tokens[0]中保存了命令标志符LIST JOIN QUIT
                    if (tokens[0].ToUpper() == "OK")
                    {
                        //处理响应
                        add("命令执行成功！");
                        label9.Text = "欢迎进入聊天系统！  " + this.username.Text;
                    }
                    else if (tokens[0].ToUpper() == "ERR")
                    {
                        add("命令执行错误：" + tokens[1]);
                    }
                    else if (tokens[0] == "LIST")
                    {
                        //此时从服务器返回的消息格式：命令标志符LIST|用户名1|用户名2|。。（所有在线用户名）
                        //add（“获得用户列表”），更新在线用户列表
                        lstUsers.Items.Clear();
                        add("获得用户列表");
                        for (int i = 1; i < tokens.Length - 1; i++)
                        {
                            lstUsers.Items.Add(tokens[i].Trim());
                        }
                    }
                    else if (tokens[0] == "JOIN")
                    {
                        this.lstUsers.Items.Add(tokens[1]);
                        if (this.username.Text == tokens[1])
                        {
                            this.state = CONNECTED;
                        }
                    }
                    else if (tokens[0] == "PIC")
                    {
                        label7.Text = "用户" + tokens[2] + "在公共聊天室发了张图片:";
                        picBox.Load(tokens[1]);
                        add("用户" + tokens[2] + "在公共聊天室发了张图片————————————————————→");
                    }
                    else if (tokens[0] == "PRIVPIC")
                    {
                        label7.Text = "用户" + tokens[1] + "向" + tokens[2] +"发了张图片:";
                        picBox.Load(tokens[3]);
                        add("用户" + tokens[1] + "向" + tokens[2] + "发了张图片:————————————————————→");
                    }
                    else if (tokens[0] == "MUTIPIC")
                    {
                        label7.Text = "用户" + tokens[1] + "在群聊里发了张图片:";
                        picBox.Load(tokens[2]);
                        add("用户" + tokens[1] + "在群聊里发了张图片————————————————————→");
                    }
                    else if (tokens[0] == "QUIT")
                    {
                        if (this.lstUsers.Items.IndexOf(tokens[1]) > -1)
                        {
                            this.lstUsers.Items.Remove(tokens[1]);
                        }
                        add("用户：" + tokens[1] + "已经离开");
                        
                    }
                    else
                    {
                        //如果从服务器返回的其他消息格式，则在ListBox控件中直接显示
                        add(msg);
                    }
                }
                //关闭连接
                tcpClient.Close();
            }
            catch
            {
                add("网络发生错误");
            }
        }
        //将“EXIT”命令发送给服务器，此命令格式要与服务器端的命令格式一致
        private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
        {

            btnExit_Click(sender, e);
        }
        //设置字体颜色
        //向显示消息的rtbMsg中添加信息是通过add函数完成的
        private void add(string msg)
        {
            if (!color.IsEmpty)
            {
                this.rtbMsg.SelectionColor = color;
            }
            this.rtbMsg.SelectedText = msg + "\n";
        }
        
        private void btnExit_Click(object sender, EventArgs e)
        {
            if (true)
            {
                string message = "EXIT|" + this.username.Text + "|";
                //将字符串转化为字符数组
                byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());
                Stream.Write(outbytes, 0, outbytes.Length);
                this.state = CLOSED;
                this.stopFlag = true;
                this.lstUsers.Items.Clear();

                //当前用户退出
                add("您已退出");
                username.Text = "";
                username.Refresh();
                label7.Text = "";
                label7.Refresh();
                label9.Text = "您好！";
                label9.Refresh();
                picBox.Image = Image.FromFile(@"C:\JiWangProject\empty.jpg");
                picBox.Refresh();
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void btnPic_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "只能发送图片文件|*.png;*.jpg;*.gif|All Files|*.*";
            if (DialogResult.OK == dlg.ShowDialog())
            {
                fs = dlg.OpenFile();
                txtPath.Text = dlg.FileName;
                
                //文件名长度
                nameLen = Path.GetFileName(dlg.FileName).Length;
                //文件名内容
                Encoding encoding = new UTF8Encoding();
                //读取文件名
                name = dlg.FileName;
                picBox.Load(dlg.FileName);

            }
        }

        private void btnSendPic_Click(object sender, EventArgs e)
        {
            //将图片发送给服务端
            //实例化一个基于TCP/IP的基于流的套接字
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint endPoint = new IPEndPoint(IPAddress.Parse("192.168.213.1"), 10000);
            sock.Connect(endPoint);

            //组建数据包
            //文件长度
            contentLen = fs.Length;
            //文件内容         
            sock.Send(BitConverter.GetBytes(contentLen));
            //循环发送文件内容
            while (true)
            {
                byte[] bits = new byte[256];
                int r = fs.Read(bits, 0, bits.Length);
                if (r <= 0) break; //如果从流中读取完毕,则break;
                sock.Send(bits, r, SocketFlags.None);
            }
            sock.Close();
            //由于读取操作会是文件指针产生偏移,最后读取结束之后,要将指针置为0;
            fs.Position = 0;

            //对收到图片的客户端进行区分处理：群聊 or 私聊 or 公众聊天
            try
            {
                if ((!this.cbPrivate.Checked) && (!this.cbMulti.Checked))            //公众聊天
                {
                   
                    string message = "PIC|" + name + "|" + this.username.Text + "|";
                    tbSendContent.Text = "";
                    tbSendContent.Focus();

                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());   //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else if ((this.cbPrivate.Checked) && (!this.cbMulti.Checked))  //私聊图片
                {
                    if (lstUsers.SelectedItems.Count != 1)
                    {
                        MessageBox.Show("请在列表中选择一个用户", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    string receiver = lstUsers.SelectedItem.ToString();
                    //消息的格式是：命令标识符PRIVPIC|发送者的用户名|接收者的用户名|发送内容

                    string message = "PRIVPIC|" + this.username.Text + "|" + receiver + "|" + name + "|";
                    tbSendContent.Text = "";
                    tbSendContent.Focus();

                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());   //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else if ((!this.cbPrivate.Checked) && (this.cbMulti.Checked))   //群聊图片
                {
                    if (lstUsers.SelectedItems.Count < 2)
                    {
                        MessageBox.Show("请在列表中选择至少两个用户", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    int i = 0;
                    string message;
                    string receivers = null;
                    for (i = 0; i < lstUsers.SelectedItems.Count; i++)
                    {
                        receivers += lstUsers.SelectedItems[i].ToString() + "*";//以“*”来分割用户名
                    }
                    receivers += "|" + lstUsers.SelectedItems.Count.ToString();
                    message = "MUTIPIC|" + this.username.Text + "|" + receivers + "|" + name + "|";
                    tbSendContent.Text = "";
                    tbSendContent.Focus();

                    byte[] outbytes = System.Text.Encoding.Default.GetBytes(message.ToCharArray());   //将字符串转化为字符数组
                    Stream.Write(outbytes, 0, outbytes.Length);
                }
                else
                {
                    MessageBox.Show("不能同时选择‘私聊’和‘多人聊天’！", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }
            catch
            {
                this.rtbMsg.AppendText("网络发生错误！");
            }

        }

        private void cbPrivate_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnColor_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog1 = new ColorDialog();
            colorDialog1.Color = this.rtbMsg.SelectionColor;
            if (colorDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                colorDialog1.Color != this.rtbMsg.SelectionColor)
            {
                this.rtbMsg.SelectionColor = colorDialog1.Color;
                color = colorDialog1.Color;
            }
        }
    }
}
