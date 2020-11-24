using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MyBlackBox
{
    public enum PortStatusEnum
    {
        CLOSED,
        OPENED
    }

    public partial class Form1 : Form
    {
        private MySerialPort com = new MySerialPort();

        private string IP_S;
        private string GetNames_Suffix_S = "/GetNames";
        private string GetSize_Suffix_S = "/GetSize";
        private string GetFile_Suffix_S = "/GetFile";
        private string DelFile_Suffix_S = "/DelFile";

        private bool IsGetFileRunning = false;
        private bool IsStreamRunning = false;
        private bool TerminateStream = false;

        private Thread th;

        public Form1()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Thread1Running()
        {
            while (true)
            {
                if (IsGetFileRunning)
                {
                    GetFileHandle();
                    IsGetFileRunning = false;
                }

                if (IsStreamRunning)
                {
                    StartStream();
                    IsStreamRunning = false;
                }
                Thread.Sleep(100);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FormClosed += MyClosedHandler;

            string[] BaudRate = { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" };
            cbxBaudrate.Items.AddRange(BaudRate);
            cbxBaudrate.SelectedIndex = 3;//9600
            cbxComPort.DataSource = SerialPort.GetPortNames();

            th = new Thread(Thread1Running);
            th.Start();
        }

        protected void MyClosedHandler(object sender, EventArgs e)
        {
            // Handle the Event here.
            th.Abort();
        }

        private void btnOpenPort_Click(object sender, EventArgs e)
        {
            if (com.Status == PortStatusEnum.CLOSED)
            {
                com.Port.PortName = cbxComPort.Text;
                com.Port.BaudRate = Convert.ToInt32(cbxBaudrate.Text);
                do
                {
                    com.Port.Open();
                } while (!com.Port.IsOpen);
                com.Status = PortStatusEnum.OPENED;
                btnOpenPort.Text = "Close";
            }
            else
            {
                do
                {
                    com.Port.Close();
                } while (com.Port.IsOpen);
                com.Status = PortStatusEnum.CLOSED;
                btnOpenPort.Text = "Open";
            }
        }

        private void btnGetName_Click(object sender, EventArgs e)
        {
            lbxFileName.Items.Clear();
            string uri = tbxIP.Text + GetNames_Suffix_S;
            WebClient client = new WebClient();
            try
            {
                string result = client.DownloadString(uri);
                int pos = 0;
                int prepos = 0;
                result += ',';
                do
                {
                    pos = result.IndexOf(',', prepos);
                    if (pos == -1)
                    {
                        break;
                    }
                    else
                    {
                        lbxFileName.Items.Add(result.Substring(prepos, pos - prepos));
                    }
                    prepos = pos + 1;
                } while (true);
                MessageBox.Show("Get Name Done");
            }
            catch
            {
                MessageBox.Show("Get Name Failed");
            }
        }

        private bool Ping()
        {
            string uri = tbxIP.Text;
            int port = 80;
            try
            {
                using (var client = new TcpClient(uri, port))
                    return true;
            }
            catch (SocketException ex)
            {
                return false;
            }
        }

        private int GetSize(string name)
        {
            string uri = tbxIP.Text + GetSize_Suffix_S;

            WebClient client = new WebClient();
            client.QueryString.Add("name", name);

            try
            {
                string result = client.DownloadString(uri);
                return Convert.ToInt32(result);
            }
            catch
            {
                //TODO
                return -1;
            }
        }

        private void lbxFileName_SelectedIndexChanged(object sender, EventArgs e)
        {
            string[] tmp = lbxFileName.GetItemText(lbxFileName.SelectedItem).Split('|');
            if (lbxFileName.SelectedIndex == -1)
            {
                lblFileName.Text = "NULL";
                lblSize.Text = "NULL";
            }
            else
            {
                lblFileName.Text = tmp[0];
                lblSize.Text = tmp[1];
            }
        }

        private string GetFile(string name, int part, int partsize)
        {
            string uri = tbxIP.Text + GetFile_Suffix_S;

            WebClient client = new WebClient();
            client.QueryString.Add("name", name);
            client.QueryString.Add("part", part.ToString());
            client.QueryString.Add("partsize", partsize.ToString());

            try
            {
                string result = client.DownloadString(uri);
                return result;
            }
            catch
            {
                //TODO
                return "";
            }
        }

        delegate void RenameGetFileCallback(string s);

        private void RenameGetFile(string s)
        {
            if (btnGetFile.InvokeRequired)
            {
                RenameGetFileCallback cb = new RenameGetFileCallback(RenameGetFile);
                Invoke(cb, new object[] { s });
            }
            else
            {
                btnGetFile.Text = s;
            }
        }

        private void GetFileHandle()
        {
            string name = lblFileName.Text.Substring(1);
            int size = GetSize(name);

            int size_per_part = 10000;

            int part = (int)(Math.Ceiling((double)size / (double)size_per_part));

            string s;

            if (!System.IO.File.Exists(name))
            {
                File.Create(name).Close();
                using (StreamWriter sw = File.AppendText(name))
                {

                    string result;
                    for (int i = 0; i < part; i++)
                    {
                        s = "GET FILE " + ((int)((double)i / (double)part * 100.0)).ToString() + "%";
                        RenameGetFile(s);
                        result = GetFile(name, i, size_per_part);
                        sw.Write(result);
                    }
                }
                MessageBox.Show("Get File Done");
                s = "GET FILE";
                RenameGetFile(s);
            }
            else
            {
                MessageBox.Show("File already exists");
            }
        }

        private void btnGetFile_Click(object sender, EventArgs e)
        {
            if (IsGetFileRunning == false)
                IsGetFileRunning = true;
        }

        private void btnBrowser_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog
            {
                Title = "Browse Text File",

                CheckFileExists = true,
                CheckPathExists = true,

                InitialDirectory = Directory.GetCurrentDirectory(),
                DefaultExt = "txt",
                Filter = "txt files (*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                tbxFile.Text = openFile.FileName;
            }
        }

        delegate void RenameStartCallback(string s);


        private void RenameStart(string s)
        {
            if (btnStart.InvokeRequired)
            {
                RenameStartCallback cb = new RenameStartCallback(RenameStart);
                Invoke(cb, new object[] { s });
            }
            else
            {
                btnStart.Text = s;
            }
        }

        delegate void AppendTextBoxCallback(string s);

        private void AppendTextBox(string s)
        {
            if (tbxLog.InvokeRequired)
            {
                AppendTextBoxCallback cb = new AppendTextBoxCallback(AppendTextBox);
                Invoke(cb, new object[] { s });
            }
            else
            {
                tbxLog.AppendText(s+"\n");
                tbxLog.ScrollToCaret();
            }
        }

        private void StartStream()
        {
            if (!com.Port.IsOpen)
            {
                MessageBox.Show("Open Com Port First!");
                return;
            }
            
            string fileurl = tbxFile.Text;
            FileInfo fi = new FileInfo(fileurl);
            int size;
            size = (int)fi.Length;
            int cursize = 0;

            using (StreamReader sr = File.OpenText(fileurl))
            {
                string s;
                while (!sr.EndOfStream && !TerminateStream)
                {
                    int process = (int)((double)cursize / (double)(size) * 100.0);
                    s = "Streaming " + process.ToString() + "%";
                    RenameStart(s);
                    string sentence = sr.ReadLine();
                    cursize += sentence.Length;
                    com.Port.WriteLine(sentence);
                    AppendTextBox(sentence);
                    System.Threading.Thread.Sleep(20);
                }
                s = "Start";
                RenameStart(s);
                TerminateStream = false;
            }
            MessageBox.Show("Stream Done");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (IsStreamRunning == false)
            {
                tbxLog.Text = "";
                IsStreamRunning = true;
            }
            else
                return;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string uri = tbxIP.Text + DelFile_Suffix_S;

            try
            {
                WebClient client = new WebClient();
                string name = lblFileName.Text.Substring(1).Split('|')[0];
                client.QueryString.Add("name", name);
                client.DownloadString(uri);

                int index = lbxFileName.SelectedIndex;
                lbxFileName.Items.RemoveAt(index);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete Failed!");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (IsStreamRunning)
                TerminateStream = true;
        }
    }

    public class MySerialPort
    {
        public SerialPort Port = new SerialPort();
        public PortStatusEnum Status = PortStatusEnum.CLOSED;
    }
}
