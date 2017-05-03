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
            new TcpReceiver().Listening(new IPEndPoint(IPAddress.Loopback, 9100));
        }
    }
}
