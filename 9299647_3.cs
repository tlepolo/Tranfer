using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace GTCRHRailWayC3.PubUtil
{
    public class CheckNetFolder
    {
        //会记录新的网络连接。
        //
        //
        //状态       本地        远程                      网络
        //
        //-------------------------------------------------------------------------------
        //OK           Y:        \\192.168.1.62\硬盘原始数 Microsoft Windows Network
        //OK           Z:        \\192.168.2.44\工作       Microsoft Windows Network
        //命令成功完成。


        public static bool CheckFolder()
        {
            bool isOK = true;
            //执行DOS命令，获取当前电脑上的网络盘
            List<string> GetCurNet = GetNetDiskInfo().Distinct( ).ToList();
            
            foreach (string pathStr in GlobalVariable.SelectDataPaths)
            { 
                //获取当前数据的驱动器类型

                string dirtype = new DriveInfo(pathStr).DriveType.ToString();
                if (dirtype != "Network") //当前驱动器是网络盘
                    continue;

                string GetIP = "";
                string[] GetIpInfo= GetUNCPath(pathStr).Split('\\');//提取网络盘的IP地址
                foreach (string str in GetIpInfo)
                {
                    if (str.Contains("."))
                    {
                        IPAddress ip = IPAddress.Any; 
                         if (IPAddress.TryParse(str, out ip))//判断这个是否是IP地址
                        {
                            GetIP = str;
                            break;
                        }
                    }
                }

                //======遍历得到的网络盘信息，如果信息中包含网络盘的IP，则判断此IP目前的状态
                for (int i = 0; i < GetCurNet.Count; i++)
                {
                    string NetPath = GetCurNet[i];
                    //只有包含网络才是需要的字段j和\\才是需要的字段
                    if (NetPath.Contains(GetIP))
                    {
                        if (NetPath.Split(' ')[0] != "OK") //包含了，则表示数据源是此网络路径，网络路径不为OK状态，检测失败
                        {
                            isOK = false;
                            break;//只要有一个不满足，其他都不满足
                        } 
                    }
                }
                GetCurNet.Clear();
            }
            return isOK;
        }
        /// <summary>
        /// 获取当前电脑上网络驱动器状态
        /// </summary>
        /// <returns></returns>
        private static string[] GetNetDiskInfo()
        {
            string EndInfo = "";
            Process process = new Process(); //创建一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = @"c:\Windows\System32\net.exe";
            startInfo.Arguments = "use"; //参数
            startInfo.UseShellExecute = false; //不使用系统外壳程序启动
            startInfo.RedirectStandardInput = false; //不重定向入
            startInfo.RedirectStandardOutput = true; //重定向输出
            startInfo.CreateNoWindow = true; //不创建窗口
            process.StartInfo = startInfo;
            try
            {
                process.Start();
                EndInfo = process.StandardOutput.ReadToEnd();
            }
            catch
            {
            }
            finally
            {
                if (process != null)
                {
                    process.Close();
                }
            }
            string[] Data = EndInfo.Split('\n');
            return Data;
        }


        //结构体
        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string provider;
        }


        //映射
        [DllImport("mpr.dll", CharSet = CharSet.Ansi)]
        private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flag);
        //断开映射
        [DllImport("mpr.dll", CharSet = CharSet.Ansi)]
        private static extern int WNetCancelConnection2(string lpname, int flag, bool force);
        //获取映射信息
        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WNetGetConnection([MarshalAs(UnmanagedType.LPTStr)] string localName,[MarshalAs(UnmanagedType.LPTStr)] StringBuilder remoteName,ref int length);
       
        /// <summary>
        /// 给定一个路径，返回的网络路径或原始路径。
        /// 例如：给定路径 P:\2008年2月29日(P:为映射的网络驱动器名)，可能会返回：“//networkserver/照片/2008年2月9日”
        /// </summary> 
        /// <param name="originalPath">指定的路径</param>
        /// <returns>如果是本地路径，返回值与传入参数值一样；如果是本地映射的网络驱动器</returns> 
        public static string GetUNCPath(string originalPath)
        {
            StringBuilder sb = new StringBuilder(512);
            int size = sb.Capacity;
            if (originalPath.Length > 2 && originalPath[1] == ':')
            {
                char c = originalPath[0];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    int error = WNetGetConnection(originalPath.Substring(0, 2),
                        sb, ref size);
                    if (error == 0)
                    {
                        DirectoryInfo dir = new DirectoryInfo(originalPath);
                        string path = System.IO.Path.GetFullPath(originalPath)
                            .Substring(System.IO.Path.GetPathRoot(originalPath).Length);
                        return System.IO.Path.Combine(sb.ToString().TrimEnd(), path);
                    }
                }
            }
            return originalPath;

        }

        /// <summary>
        /// 映射网络驱动器
        /// </summary>
        /// <param name="localName">本地盘符 如U:</param>
        /// <param name="remotePath">远程路经 如\\\\172.18.118.106\\f</param>
        /// <param name="userName">远程服务器用户名</param>
        /// <param name="password">远程服务器密码</param>
        /// <returns>true映射成功，false映射失败</returns>
        public static bool WNetReflectDrive(string localName, string remotePath, string userName, string password)
        {
            NetResource netResource = new NetResource();
            netResource.dwScope = 2;
            netResource.dwType = 0x1;
            netResource.dwDisplayType = 3;
            netResource.dwUsage = 1;
            netResource.LocalName = localName;
            netResource.RemoteName = remotePath;
            netResource.provider = null;
            int ret = WNetAddConnection2(netResource, password, userName, 0);
            if (ret == 0)
                return true;
            return false;
        }

        /// <summary>
        /// 断开网路驱动器
        /// </summary>
        /// <param name="lpName">映射的盘符</param>
        /// <param name="flag">true时如果打开映射盘文件夹，也会断开,返回成功 false时打开映射盘文件夹，返回失败</param>
        /// <returns></returns>
        public static bool WNetDisconnectDrive(string lpName, bool flag)
        {
            int ret = WNetCancelConnection2(lpName, 0, flag);
            if (ret == 0)
                return true;
            return false;
        }

    }
}
