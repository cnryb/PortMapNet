using DotNetCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    private static StringBuilder _console_buf = new StringBuilder();
    private static readonly Dictionary<int, stat_obj> _stat_info = new Dictionary<int, stat_obj>();

    static void Main(string[] args)
    {
        //支持中文
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var work = new WorkGroup();
        work.PointIn = new IPEndPoint(IPAddress.Any, 8000);


        work.PointOutHost = "localhost";
        work.PointOutPort = 80;

        ThreadPool.QueueUserWorkItem(c =>
        {
            Start(work);
        });

        Console.CursorVisible = false;
        while (true)
        {
            show_stat();
            Thread.Sleep(2000);
        }
    }

    private static void Start(WorkGroup work)
    {
        bool start_error = false;
        try
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _stat_info.Add(work.Id, new stat_obj(work.PointIn.ToString(), work.PointOutHost + ":" + work.PointOutPort, !start_error, 0, 0, 0));
            serverSocket.Bind(work.PointIn);
            serverSocket.Listen(10);
            while (true)
            {
                Socket fromSocket = serverSocket.Accept();
                ThreadPool.QueueUserWorkItem(c =>
                {
                    ++_stat_info[work.Id]._connect_cnt;
                    Socket toSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        toSocket.Connect(work.PointOutHost, work.PointOutPort);
                    }
                    catch (Exception exp)
                    {
                        //_l4n.Warn(exp.Message);
                        try
                        {
                            fromSocket.Shutdown(SocketShutdown.Both);
                            toSocket.Shutdown(SocketShutdown.Both);
                            fromSocket.Dispose();
                            toSocket.Dispose();
                        }
                        catch (Exception) { }
                        --_stat_info[work.Id]._connect_cnt;
                        return;
                    }
                    Thread t_send = new Thread(new ParameterizedThreadStart(send_caller)) { IsBackground = true };
                    Thread t_recv = new Thread(new ParameterizedThreadStart(recv_caller)) { IsBackground = true };
                    t_send.Start(new object[] { fromSocket, toSocket, work.Id });
                    t_recv.Start(new object[] { toSocket, fromSocket, work.Id });
                    t_send.Join();
                    t_recv.Join();
                    --_stat_info[work.Id]._connect_cnt;
                });
            }

        }
        catch (Exception exp)
        {
            //_l4n.Error(exp.Message);
            start_error = true;
        }
    }

    private static void recv_caller(object thread_param)
    {
        object[] param_arr = thread_param as object[];
        Socket sock_cli_remote = param_arr[0] as Socket;
        Socket sock_cli = param_arr[1] as Socket;
        try
        {
            recv_and_send(sock_cli_remote, sock_cli, bytes =>
            {
                stat_obj stat = _stat_info[(int)param_arr[2]];
                stat._bytes_recv += bytes;
            });
        }
        catch (Exception exp)
        {
            //_l4n.Info(exp.Message);
            try
            {
                sock_cli_remote.Shutdown(SocketShutdown.Both);
                sock_cli.Shutdown(SocketShutdown.Both);
                sock_cli_remote.Dispose();
                sock_cli.Dispose();
            }
            catch (Exception) { }
        }
    }

    private static void send_caller(object thread_param)
    {
        object[] param_arr = thread_param as object[];
        Socket from_sock = param_arr[0] as Socket;
        Socket to_sock = param_arr[1] as Socket;
        try
        {
            recv_and_send(from_sock, to_sock, bytes =>
            {
                stat_obj stat = _stat_info[(int)param_arr[2]];
                stat._bytes_send += bytes;
            });
        }
        catch (Exception exp)
        {
            //_l4n.Info(exp.Message);
        }
        finally
        {
            try
            {
                from_sock.Shutdown(SocketShutdown.Both);
                to_sock.Shutdown(SocketShutdown.Both);
                from_sock.Dispose();
                to_sock.Dispose();
            }
            catch (Exception) { }
        }
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
}