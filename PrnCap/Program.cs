using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrnCap
{
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    class Program
    {
        static void Main(string[] args)
        {
            WmiWrapper.InstallPrinter();

            var tcpReceiver = new TcpReceiver();

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                tcpReceiver.Stop().WaitOne();
                Thread.Sleep(2000);
            };
            Thread.GetDomain().UnhandledException += (sender, eventArgs) =>
            {
                tcpReceiver.Stop().WaitOne();
                Thread.Sleep(2000);
            };

            tcpReceiver.Listening(new IPEndPoint(IPAddress.Loopback, 9100));
        }
    }
}
