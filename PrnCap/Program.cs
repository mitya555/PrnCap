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
            new Server().Listening(new IPEndPoint(IPAddress.Loopback, 9100));
        }
    }
    class Server
    {
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ManualResetEvent handlerObtained = new ManualResetEvent(false);
        string tempPath = Path.GetTempPath();
        public void Listening(EndPoint endPoint, int backlog = 100)
        {
            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(endPoint);
                listener.Listen(backlog);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    handlerObtained.Reset();

                    // Start an asynchronous socket to listen for connections.
                    DateTime _now = DateTime.Now;
                    DateTimeOffset _now_w_tz = new DateTimeOffset(_now, TimeZoneInfo.Local.GetUtcOffset(_now));
                    Console.WriteLine(_now_w_tz.ToString("o") + "\tListening...");
                    listener.BeginAccept(AcceptCallback, null);

                    // Wait until a connection is made before continuing.  
                    handlerObtained.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        void AcceptCallback(IAsyncResult ar)
        {
            // Get the socket that handles the client request.  
            Socket handler = listener.EndAccept(ar);

            // Signal the main thread to continue.  
            handlerObtained.Set();

            // Create the state object.  
            StateObject state = new StateObject() { workSocket = handler };
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
        }
        // State object for reading client data asynchronously  
        class StateObject
        {
            // Client  socket.  
            public Socket workSocket = null;
            // Size of receive buffer.  
            public const int BufferSize = 1024;
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];
            // Received data. 
            public MemoryStream data = new MemoryStream();
        }
        void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.   
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.data.Write(state.buffer, 0, bytesRead);

                // Not all data received. Get more.  
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
            }
            else
            {
                byte[] _data = state.data/*.GetBuffer()*/.ToArray();
                //Console.Write(Encoding.UTF8.GetString(_data));
                Directory.CreateDirectory(Path.Combine(tempPath, "PrnCap"));
                DateTime _now = DateTime.Now;
                DateTimeOffset _now_w_tz = new DateTimeOffset(_now, TimeZoneInfo.Local.GetUtcOffset(_now));
                File.WriteAllBytes(Path.Combine(Path.Combine(tempPath, "PrnCap"),
                    _now_w_tz.ToString("yyyy-MM-dd_HH-mm-ss.fffzzz").Replace(":", "")), _data);
            }
        }
    }
}
