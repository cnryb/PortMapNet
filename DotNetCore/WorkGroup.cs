using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DotNetCore
{
    class WorkGroup
    {
        public int Id { get; set; }

        /// <summary>
        /// 检测端口
        /// </summary>
        public IPEndPoint PointIn { get; set; }

        /// <summary>
        /// 转发到的主机
        /// </summary>
        public string PointOutHost { get; set; }

        /// <summary>
        /// 转发到的端口
        /// </summary>
        public ushort PointOutPort;
    }
}
