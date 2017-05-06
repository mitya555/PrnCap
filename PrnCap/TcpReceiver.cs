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
        string destPath = Path.Combine(Path.GetTempPath(), ".prncap");
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

				WaitForAllConnections();

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
            }
        }

		private void WaitForAllConnections()
		{
			var waitHandles = connectionWaitHandles.ToArray();
			while (waitHandles.Length > 0)
			{
				WaitHandle.WaitAll(waitHandles);
				foreach (var wh in waitHandles)
				{
					wh.Close();
				}
				lock (connectionWaitHandles)
				{
					waitHandles = connectionWaitHandles.ToArray();
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
			// create and register connection WaitHandle
			var connectionWaitHandle = AsyncWaitState.NewHandle();
			connectionWaitHandles.Add(connectionWaitHandle);

            // Signal the main thread to continue.  
            listenerWaitHandle.Set();

            // Get the socket that handles the client request.  
            var tcpConnectionHandler = listener.EndAccept(ar);

            // Create the state object.  
            var state = new ReadState() { workSocket = tcpConnectionHandler, AsyncWaitHandle = connectionWaitHandle };
			tcpConnectionHandler.BeginReceive(state.buffer, 0, ReadState.BufferSize, 0, ReadCallback, state);
        }
		class AsyncWaitState
		{
			// WaitHandle
			public ManualResetEvent AsyncWaitHandle { get; set; }
			public static ManualResetEvent NewHandle() { return new ManualResetEvent(false); }
		}
        // State object for reading client data asynchronously  
        class ReadState : AsyncWaitState
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
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            var state = (ReadState)ar.AsyncState;
            var connectionHandler = state.workSocket;

            // Read data from the client socket.
            var bytesRead = connectionHandler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There might be more data, so store the data received so far.  
                state.data.Write(state.buffer, 0, bytesRead);

                // Not all data received. Get more.  
                connectionHandler.BeginReceive(state.buffer, 0, ReadState.BufferSize, 0, ReadCallback, state);
            }
            else
            {
				// All data received. Shutdown the connection
				connectionHandler.Shutdown(SocketShutdown.Both);
				connectionHandler.Close();

				Directory.CreateDirectory(destPath);
				var _file = Path.Combine(destPath, NowWithTimezone().ToString("yyyy-MM-dd_HH-mm-ss.fffzzz").Replace(":", ""));

				//var _data = state.data/*.GetBuffer()*/.ToArray();
				////Console.Write(Encoding.UTF8.GetString(_data));
				//File.WriteAllBytes(_file, _data);

				var _data = state.data.GetBuffer();
				var _len = (int)state.data.Length;
				var fileStream = new FileStream(_file, FileMode.CreateNew, FileAccess.Write);
				fileStream.BeginWrite(_data, 0, _len, EndWriteCallback,
					new WriteState() { fileStream = fileStream, buffer = _data, AsyncWaitHandle = state.AsyncWaitHandle });
			}
		}
		class WriteState : AsyncWaitState
		{
			// Write FileStream.
			public FileStream fileStream;
			// Source buffer.
			public byte[] buffer;
		}
		void EndWriteCallback(IAsyncResult ar)
		{
			var state = (WriteState)ar.AsyncState;
			using (var fileStream = state.fileStream)
			{
				fileStream.EndWrite(ar);
			}
			// remove signalled WaitHandle
			SignalWaitHandle(state.AsyncWaitHandle);
		}

		private void SignalWaitHandle(ManualResetEvent manualResetEvent)
		{
			lock (connectionWaitHandles)
			{
				// signal WaitHandle
				manualResetEvent.Set();
				// remove WaitHandle
				connectionWaitHandles.Remove(manualResetEvent);
			}
		}
	}
}
