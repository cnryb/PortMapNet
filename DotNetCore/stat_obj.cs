using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DotNetCore
{
    class stat_obj
    {
        #region Fields (5)

        /// <summary>
        /// 连接数
        /// </summary>
        public int ConnectCount { get; set; }

        /// <summary>
        /// 输入IP:端口
        /// </summary>
        public string PointIn { get; set; }

        /// <summary>
        /// 输出IP:端口
        /// </summary>
        public string PointOut { get; set; }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 发送的字节数
        /// </summary>
        public long SendBytes { get; set; }

        /// <summary>
        /// 接收到的字节数
        /// </summary>
        public long ReceiveBytes { get; set; }

        /// <summary>
        /// The size suffixes.
        /// </summary>
        private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };


        public const string _print_head = "输入IP              输出IP              状态    连接数    接收/发送";
        #endregion Fields

        #region Constructors (1)


        public stat_obj(WorkGroup work)
        {
            PointIn = work.PointIn.ToString();
            PointOut = work.PointOutHost + ":" + work.PointOutPort;
            IsRunning = true;
        }

        #endregion Constructors



        public override string ToString()
        {
            return string.Format($"{PointIn.PadRight(20, ' ')}{ PointOut.PadRight(20, ' ')}{(IsRunning ? "运行中  " : "启动失败")}{ConnectCount.ToString().PadRight(10, ' ')}{SizeSuffix(ReceiveBytes)}/{SizeSuffix(SendBytes)}");
        }


        /// <summary>
        /// Suffix (Gb,Mb,Kb suffix) of given bytes
        /// </summary>
        /// <param name="value">  size of file in bytes </param>
        /// <returns> suffix of file size </returns>
        private static string SizeSuffix(long value)
        {
            if (value == 0)
                return "0";
            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1 << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }

    }
}
