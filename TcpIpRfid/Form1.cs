using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Threading;

namespace TcpIpRfid
{
    public partial class Form1 : Form
    {
        TcpClient tcpClient = new TcpClient();
        NetworkStream networkStream;

        const int CMD_REGISTER = 1;
        const int CMD_UNREGISTER = 2;
        const int CMD_ENCODEKEDLCL = 3;
        const int CMD_RETURNKCDLCL = 5;
        const int CMD_VERIFYKCDLCL = 12;

        int returnKeyDtaSize = 0, hdrSize = 0, socketReceiveSize = 0;
        byte[] readBytes;

        string cardSerialNum = "048FCB924E6880", cardUniqueId = "EB0D8F1F";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifHdr
        {
            public uint ui32Synch1;
            public uint ui32Synch2;
            public ushort ui16Version;
            public int ui32Cmd;
            public int ui32BodySize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifRegisterMsg
        {
            public SPMSifHdr hdr1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] szLicense;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] szApplName;
            public int nRet;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifUnRegisterMsg
        {
            public SPMSifHdr hdr1;
            public int nRet;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifEncodeKcdLclMsg
        {
            public SPMSifHdr hdr1;
            public byte ff;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] Dta;
            public bool Debug;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] szOpId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpFirst;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpLast;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifReturnKcdLclMsg
        {
            public SPMSifHdr hdr1;
            public byte ff;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] Dta;
            public bool Debug;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] szOpId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpFirst;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpLast;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SPMSifVerifyKcdLclMsg
        {
            public SPMSifHdr hdr1;
            public byte ff;
            public byte gg;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 266)]
            public byte[] Kcd;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] Dta;
            public bool Debug;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] szOpId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpFirst;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szOpLast;
        }

        private SPMSifHdr SetHeader(int TCmd) {
            SPMSifHdr hdr = new SPMSifHdr();
            hdr.ui32Synch1 = 0x55555555;
            hdr.ui32Synch2 = 0xAAAAAAAA;
            hdr.ui16Version = 1;
            hdr.ui32Cmd = TCmd;
            hdr.ui32BodySize = GetSize(TCmd) - Marshal.SizeOf(hdr);
            return hdr;
        }

        private int GetSize(int TCmd) {
            int result = Marshal.SizeOf(typeof(SPMSifRegisterMsg));
            switch (TCmd) {
                case CMD_UNREGISTER:
                    result = Marshal.SizeOf(typeof(SPMSifUnRegisterMsg));
                    break;
                case CMD_ENCODEKEDLCL:
                    result = Marshal.SizeOf(typeof(SPMSifEncodeKcdLclMsg));
                    break;
                case CMD_RETURNKCDLCL:
                    result = Marshal.SizeOf(typeof(SPMSifReturnKcdLclMsg));
                    break;
                case CMD_VERIFYKCDLCL:
                    result = Marshal.SizeOf(typeof(SPMSifVerifyKcdLclMsg));
                    break;
            }
            return result;
        }

        private void BuildDataFrame(out string Dta)
        {
            char code = (char)30;

            Dta = code.ToString() + "R" + txtRoom.Text + code.ToString() + "T" + txtUserType.Text;

            if (txtFirstName.Text != "")
            {
                Dta = Dta + code.ToString() + "F" + txtFirstName.Text;
            }

            if (txtLastName.Text != "")
            {
                Dta = Dta + code.ToString() + "N" + txtLastName.Text;
            }

            if (txtUserGroup.Text != "")
            {
                Dta = Dta + code.ToString() + "U" + txtUserGroup.Text;
            }

            if (txtStartDate.Text != "")
            {
                Dta = Dta + code.ToString() + "D" + txtStartDate.Text;
            }

            if (txtEndDate.Text != "")
            {
                Dta = Dta + code.ToString() + "O" + txtEndDate.Text;
            }

            if (txtAdditionalInfo.Text != "")
            {
                Dta = Dta + code.ToString() + txtAdditionalInfo.Text;
            }

            Dta = Dta + code.ToString() + "J1" + code.ToString() + "S" + cardSerialNum + code.ToString() + "V" + cardUniqueId;
        }

        private void StrPCopy(string str, byte[] dta) {
            char[] strChar = str.ToCharArray();
            for (int i = 0; i < dta.Length; i++)
            {
                if (i < strChar.Length)
                {
                    dta[i] = Convert.ToByte(strChar[i]);
                }
                else
                {
                    dta[i] = 0;
                }
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            hdrSize = Marshal.SizeOf(typeof(SPMSifHdr));
            returnKeyDtaSize = Marshal.SizeOf(typeof(SPMSifReturnKcdLclMsg)) - hdrSize;
            socketReceiveSize = Marshal.SizeOf(typeof(SPMSifVerifyKcdLclMsg));

            readBytes = new byte[socketReceiveSize];
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try {
                tcpClient.Connect(txtIpAddress.Text, 3015);
                lblConnectStatus.Text = "Connected";
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            SPMSifRegisterMsg RegMsg = new SPMSifRegisterMsg();
            RegMsg.hdr1 = SetHeader(CMD_REGISTER);

            RegMsg.szLicense = new byte[20];
            StrPCopy(txtLicenseCode.Text, RegMsg.szLicense);

            RegMsg.szApplName = new byte[20];
            StrPCopy(txtAppName.Text, RegMsg.szApplName);

            RegMsg.nRet = 0;

            var bufferSize = Marshal.SizeOf(typeof(SPMSifRegisterMsg));
            var byteArray = new byte[bufferSize];

            IntPtr handle = Marshal.AllocHGlobal(bufferSize);
            try
            {
                Marshal.StructureToPtr(RegMsg, handle, true);
                Marshal.Copy(handle, byteArray, 0, bufferSize);
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }

            networkStream = tcpClient.GetStream();
            if (networkStream.CanWrite)
            {
                networkStream.Write(byteArray, 0, byteArray.Length);
                txtRegResult.Text = "0";
            }
            else {
                txtRegResult.Text = "1";
                tcpClient.Close();
            }
        }

        private async Task SendReturnKeyAsync(byte[] sendBytes) {
            for(int ii=0; ii< socketReceiveSize; ii++)
            {
                readBytes[ii] = 1; 
            }

            networkStream = tcpClient.GetStream();
            if (networkStream.CanWrite)
            {
                await networkStream.WriteAsync(sendBytes, 0, sendBytes.Length);
                await networkStream.FlushAsync();

                if (networkStream.CanRead)
                {
                    await networkStream.ReadAsync(readBytes, 0, socketReceiveSize, CancellationToken.None);

                    int cmdInt = int.Parse(readBytes[hdrSize].ToString());
                    char cmdChar = (char)cmdInt;
                    txtActionResult.Text = cmdChar.ToString();

                    int dtaSize = socketReceiveSize - hdrSize;
                    if (dtaSize > returnKeyDtaSize) dtaSize = returnKeyDtaSize;

                    string res = "";
                    for (int i = 0; i < dtaSize; i++)
                    {
                        int btCode = int.Parse(readBytes[i + hdrSize].ToString());
                        char ch = (char)btCode;
                        res += ch + " ";
                    }
                    txtActionResultData.Text = res;
                }
                else
                {
                    txtActionResult.Text = "1";
                    tcpClient.Close();
                }
            }
            else if (!networkStream.CanWrite)
            {
                txtActionResult.Text = "2";
                tcpClient.Close();
            }
        }

        private async void btnReturnKey_ClickAsync(object sender, EventArgs e)
        {
            SPMSifReturnKcdLclMsg RetnMsg = new SPMSifReturnKcdLclMsg();
            RetnMsg.hdr1 = SetHeader(CMD_RETURNKCDLCL);

            string Tmp = txtCommand.Text;
            RetnMsg.ff = Convert.ToByte(Tmp[0]);

            string dta;
            BuildDataFrame(out dta);

            RetnMsg.Dta = new byte[512];
            StrPCopy(dta, RetnMsg.Dta);

            RetnMsg.Debug = false;

            RetnMsg.szOpId = new byte[10];
            StrPCopy(txtSysId.Text, RetnMsg.szOpId);

            RetnMsg.szOpFirst = new byte[16];
            StrPCopy(txtSysFirstname.Text, RetnMsg.szOpFirst);

            RetnMsg.szOpLast = new byte[16];
            StrPCopy(txtSysLastName.Text, RetnMsg.szOpLast);

            var bufferSize = Marshal.SizeOf(typeof(SPMSifReturnKcdLclMsg));
            var byteArray = new byte[bufferSize];

            IntPtr handle = Marshal.AllocHGlobal(bufferSize);
            try
            {
                Marshal.StructureToPtr(RetnMsg, handle, true);
                Marshal.Copy(handle, byteArray, 0, bufferSize);
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }

            await SendReturnKeyAsync(byteArray);
            await SendReturnKeyAsync(byteArray);

            string encodedKey = "";
            if(txtActionResult.Text == "0")
            {
                for(int i=3; i<99; i++)
                {
                    int btCode = int.Parse(readBytes[i + hdrSize].ToString());
                    char ch = (char)btCode;
                    encodedKey += ch;
                }
                txtEncodedKey.Text = encodedKey;
            }
        }
    }
}
