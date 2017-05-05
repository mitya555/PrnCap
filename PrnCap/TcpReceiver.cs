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
    class TcpReceiver
    {
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ManualResetEvent listenerWaitHandle = new ManualResetEvent(false);
        bool signalStop = false;
        ManualResetEvent exitWaitHandle;
        public WaitHandle Stop()
        {
            signalStop = true;
            listenerWaitHandle.Set();
            return (exitWaitHandle = new ManualResetEvent(false));
        }
        HashSet<WaitHandle> connectionWaitHandles = new HashSet<WaitHandle>();
        string destPath = Path.Combine(Path.GetTempPath(), "PrnCap");
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
                    listenerWaitHandle.Reset();

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine(NowWithTimezone().ToString("o") + "\tListening...");
                    listener.BeginAccept(AcceptCallback, null);

                    // Wait until a connection is made before continuing.  
                    listenerWaitHandle.WaitOne();
                    if (signalStop)
                    {
                        break;
                    }
                }

                WaitHandle.WaitAll(connectionWaitHandles.ToArray());

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                if (exitWaitHandle != null)
                {
                    exitWaitHandle.Set();
                }
                foreach (var wh in connectionWaitHandles)
                {
                    wh.Close();
                }
            }
        }

        private static DateTimeOffset NowWithTimezone()
        {
            var _now = DateTime.Now;
            var _now_w_tz = new DateTimeOffset(_now, TimeZoneInfo.Local.GetUtcOffset(_now));
            return _now_w_tz;
        }

        void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            listenerWaitHandle.Set();

            // Get the socket that handles the client request.  
            var tcpConnectionHandler = listener.EndAccept(ar);

            // Create the state object.  
            var state = new StateObject() { workSocket = tcpConnectionHandler };
            var res = tcpConnectionHandler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
            connectionWaitHandles.Add(res.AsyncWaitHandle);
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
            // signal whoever is waiting to continue
            using (var wh = ar.AsyncWaitHandle)
            {
                connectionWaitHandles.Remove(wh);
            }

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            var state = (StateObject)ar.AsyncState;
            var tcpConnectionHandler = state.workSocket;

            // Read data from the client socket.
            var bytesRead = tcpConnectionHandler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.data.Write(state.buffer, 0, bytesRead);

                // Not all data received. Get more.  
                var res = tcpConnectionHandler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
                connectionWaitHandles.Add(res.AsyncWaitHandle);
            }
            else
            {
                var _data = state.data/*.GetBuffer()*/.ToArray();
                //Console.Write(Encoding.UTF8.GetString(_data));
                Directory.CreateDirectory(destPath);
                File.WriteAllBytes(Path.Combine(destPath, NowWithTimezone().ToString("yyyy-MM-dd_HH-mm-ss.fffzzz").Replace(":", "")), _data);
            }
        }
    }
}
