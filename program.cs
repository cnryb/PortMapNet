using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using log4net;

namespace portmap_net
{
    internal class program
    {
        #region Fields (4)

        private static StringBuilder _console_buf = new StringBuilder();
        private static readonly ILog _l4n = LogManager.GetLogger(typeof(program));
        private static readonly Dictionary<int, stat_obj> _stat_info = new Dictionary<int, stat_obj>();
        #endregion Fields

        #region Methods (8)

        static void Main(string[] args)
        {
            var mg = new WorkGroup();
            mg.PointIn = new IPEndPoint(IPAddress.Any, 8000);

            
            mg.PointOutHost = "localhost";
            mg.PointOutPort = 80;

            map_start(mg);
            Console.CursorVisible = false;
            while (true)
            {
                show_stat();
                Thread.Sleep(2000);
            }
        }

        private static void map_start(WorkGroup work)
        {
            Socket sock_svr = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool start_error = false;
            try
            {
                sock_svr.Bind(work.PointIn);
                sock_svr.Listen(10);
                sock_svr.BeginAccept(on_local_connected, new object[] { sock_svr, work });
            }
            catch (Exception exp)
            {
                _l4n.Error(exp.Message);
                start_error = true;
            }
            finally
            {
                _stat_info.Add(work.Id, new stat_obj(work.PointIn.ToString(), work.PointOutHost + ":" + work.PointOutPort, !start_error, 0, 0, 0));
            }
        }

        private static void on_local_connected(IAsyncResult ar)
        {
            object[] ar_arr = ar.AsyncState as object[];
            Socket sock_svr = ar_arr[0] as Socket;
            WorkGroup work = (WorkGroup)ar_arr[1];

            ++_stat_info[work.Id]._connect_cnt;
            Socket sock_cli = sock_svr.EndAccept(ar);
            sock_svr.BeginAccept(on_local_connected, ar.AsyncState);
            Socket sock_cli_remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock_cli_remote.Connect(work.PointOutHost, work.PointOutPort);
            }
            catch (Exception exp)
            {
                _l4n.Warn(exp.Message);
                try
                {
                    sock_cli.Shutdown(SocketShutdown.Both);
                    sock_cli_remote.Shutdown(SocketShutdown.Both);
                    sock_cli.Dispose();
                    sock_cli_remote.Dispose();
                }
                catch (Exception) { }
                --_stat_info[work.Id]._connect_cnt;
                return;
            }
            Thread t_send = new Thread(new ParameterizedThreadStart(recv_and_send_caller)) { IsBackground = true };
            Thread t_recv = new Thread(new ParameterizedThreadStart(recv_and_send_caller)) { IsBackground = true };
            t_send.Start(new object[] { sock_cli, sock_cli_remote, work.Id, true });
            t_recv.Start(new object[] { sock_cli_remote, sock_cli, work.Id, false });
            t_send.Join();
            t_recv.Join();
            --_stat_info[work.Id]._connect_cnt;
        }

        private static void recv_and_send(Socket from_sock, Socket to_sock, Action<int> send_complete)
        {
            byte[] recv_buf = new byte[4096];
            int recv_len;
            while ((recv_len = from_sock.Receive(recv_buf)) > 0)
            {
                to_sock.Send(recv_buf, 0, recv_len, SocketFlags.None);
                send_complete(recv_len);
            }
        }

        private static void recv_and_send_caller(object thread_param)
        {
            object[] param_arr = thread_param as object[];
            Socket sock1 = param_arr[0] as Socket;
            Socket sock2 = param_arr[1] as Socket;
            try
            {
                recv_and_send(sock1, sock2, bytes =>
                {
                    stat_obj stat = _stat_info[(int)param_arr[2]];
                    if ((bool)param_arr[3])
                        stat._bytes_send += bytes;
                    else
                        stat._bytes_recv += bytes;
                });
            }
            catch (Exception exp)
            {
                _l4n.Info(exp.Message);
                try
                {
                    sock1.Shutdown(SocketShutdown.Both);
                    sock2.Shutdown(SocketShutdown.Both);
                    sock1.Dispose();
                    sock2.Dispose();
                }
                catch (Exception) { }
            }
        }

        private static void show_stat()
        {
            StringBuilder curr_buf = new StringBuilder();
            curr_buf.AppendLine(stat_obj._print_head);
            foreach (KeyValuePair<int, stat_obj> item in _stat_info)
            {
                curr_buf.AppendLine(item.Value.ToString());
            }
            if (_console_buf.Equals(curr_buf))
                return;
            Console.Clear();
            Console.WriteLine(curr_buf);
            _console_buf = curr_buf;
        }

        #endregion Methods


    }
}
