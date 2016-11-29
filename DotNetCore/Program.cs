using DotNetCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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


        work.PointOutHost = "10.0.0.111";
        work.PointOutPort = 80;

        ThreadPool.QueueUserWorkItem(c =>
        {
            Task.WaitAll(Start(work));
        });

        Console.CursorVisible = false;
        while (true)
        {
            show_stat();
            Thread.Sleep(2000);
        }
    }

    private static async Task Start(WorkGroup work)
    {
        bool start_error = false;
        try
        {
            TcpListener listner = new TcpListener(work.PointIn);
            listner.Start();
            _stat_info.Add(work.Id, new stat_obj(work.PointIn.ToString(), work.PointOutHost + ":" + work.PointOutPort, !start_error, 0, 0, 0));
            while (true)
            {
                var  fromClient = await listner.AcceptTcpClientAsync();
                ThreadPool.QueueUserWorkItem(c =>
                {
                    ++_stat_info[work.Id]._connect_cnt;
                    TcpClient toClient = new TcpClient();
                    try
                    {
                        toClient.ConnectAsync(work.PointOutHost, work.PointOutPort);
                    }
                    catch (Exception exp)
                    {
                        //_l4n.Warn(exp.Message);
                        try
                        {
                            fromClient.Dispose();
                            toClient.Dispose();
                        }
                        catch (Exception) { }
                        --_stat_info[work.Id]._connect_cnt;
                        return;
                    }
                    Thread t_send = new Thread(new ParameterizedThreadStart(send_caller)) { IsBackground = true };
                    Thread t_recv = new Thread(new ParameterizedThreadStart(recv_caller)) { IsBackground = true };
                    t_send.Start(new object[] { fromClient, toClient, work.Id });
                    t_recv.Start(new object[] { toClient, fromClient, work.Id });
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
        var sock_cli_remote = param_arr[0] as TcpClient;
        var sock_cli = param_arr[1] as TcpClient;
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
                sock_cli_remote.Dispose();
                sock_cli.Dispose();
            }
            catch (Exception) { }
        }
    }

    private static void send_caller(object thread_param)
    {
        object[] param_arr = thread_param as object[];
        var from_sock = param_arr[0] as TcpClient;
        var to_sock = param_arr[1] as TcpClient;
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
                from_sock.Dispose();
                to_sock.Dispose();
            }
            catch (Exception) { }
        }
    }


    private static void recv_and_send(TcpClient fromClient, TcpClient toClient, Action<int> send_complete)
    {
        var fns=fromClient.GetStream();
        var tns = toClient.GetStream();
        byte[] buff = new byte[4096];
        int recv_len;
        while ((recv_len = fns.Read(buff,0,buff.Length)) > 0)
        {
            tns.Write(buff, 0, recv_len);
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