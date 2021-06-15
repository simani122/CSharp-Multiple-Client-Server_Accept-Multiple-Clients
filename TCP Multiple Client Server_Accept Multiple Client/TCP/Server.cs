using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace TCP_Multiple_Client_Server_Accept_Multiple_Client
{
    public class Server
    {
        #region 변수설정

        private ManualResetEvent _connected = new ManualResetEvent(false);
        // Server socket
        private Socket _server = null;
        public Hashtable _clients;

        private System.Threading.Thread serverWorker = null;

        //use Queue Buffer for Recv Datas
        public System.Collections.Concurrent.ConcurrentQueue<byte> queueRECV_Data = new System.Collections.Concurrent.ConcurrentQueue<byte>();


        public static IPAddress LocalIPAddress { get { return IPAddress.Any; } }
        public static int Port { get { return 8000; } }
        //public static int Port { get { return 1470; } } //테스트 포트
        public static IPEndPoint EndPoint { get { return new IPEndPoint(LocalIPAddress, Port); } }

        private bool _isServeReady = false;

        #endregion

        #region Init & 생성자
        public Server()
        {
            Initialize();
            ThreadStart();
        }

        private void ThreadStart()
        {
            serverWorker = new Thread(new ThreadStart(StartListening));
            serverWorker.IsBackground = true;
            serverWorker.Start();
        }

        public void Initialize()
        {
            _clients = new Hashtable();
        }

        #endregion

        #region Accept Listen


        /// <summary>
        /// Listen for client connections
        /// </summary>
        private void StartListening()
        {
            try
            {
                if (_isServeReady == false)
                {
                    // Create a TCP/IP socket.
                    _server = CreateSocket();
                    _server.Bind(EndPoint);
                    _server.Listen(10);
                    _server.BeginAccept(new AsyncCallback(AcceptCallback), _server);
                    _isServeReady = true;
                }


                while (true)
                {
                    // Set the event to nonsignaled state
                    _connected.Reset();

                    // Start an asynchronous socket to listen for connections
                    _server.BeginAccept(new AsyncCallback(AcceptCallback), _server);

                    // Wait until a connection is made before continuing
                    _connected.WaitOne();
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static Socket CreateSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        /// <summary>
        /// Handler for new connections
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptCallback(IAsyncResult ar)
        {
            //PrintConnectionState("Connection received");
            if (ar == null)
            {
                return;
            }

            // Signal the main thread to continue accepting new connections
            _connected.Set();

            // Accept new client socket connection
            Socket socket = _server.EndAccept(ar);

            // Create a new client connection object and store the socket
            Class_ConnectedObject client = new Class_ConnectedObject();
            client.Socket = socket;

            int _ssid = Common.SSID;
            string[] chk_ip = socket.RemoteEndPoint.ToString().Split(':');

            if (!_clients.ContainsKey(_ssid))
            {
                client.ID = _ssid;
                _clients.Add(_ssid, client);
            }

            // Begin receiving messages from new connection
            try
            {
                client.Socket.BeginReceive(client.Buffer, 0, client.BufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
            }
            catch (SocketException)
            {
                // Client was forcebly closed on the client side
                CloseClient(client, _ssid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        #region Receive 리시브

        object locker = new object();
        /// <summary>
        /// Handler for received messages
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            string err;
            Class_ConnectedObject client;
            int bytesRead;
            byte[] dataBuff;
            int _ssid;
            //client = (Class_ConnectedObject)ar.AsyncState;
            // Check for null values
            if (!CheckState(ar, out err, out client))
            {
                Console.WriteLine(err);
                return;
            }

            _ssid = client.ID;

            //use Check Client is Disconnected? or not?  
            //Socket.Poll is Check Connection
            bool result = client.Socket.Poll(1000000, SelectMode.SelectRead);
            if (result)
            {
                CloseClient(client, _ssid);
            }

            // Read message from the client socket
            try
            {
                bytesRead = client.Socket.EndReceive(ar);
                dataBuff = new byte[bytesRead];
            }
            catch (SocketException)
            {
                // Client was forcebly closed on the client side
                CloseClient(client, _ssid);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            // Check message
            if (bytesRead > 0)
            {
                // Build message as it comes in
                // client.BuildIncomingMessage(bytesRead);
                Array.Copy(client.Buffer, dataBuff, bytesRead);

                foreach (byte data in dataBuff)
                {
                    queueRECV_Data.Enqueue(data);
                }
            }

            // Listen for more incoming messages
            try
            {
                client.Socket.BeginReceive(client.Buffer, 0, client.BufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallback), client);
            }
            catch (SocketException)
            {
                // Client was forcebly closed on the client side
                CloseClient(client, _ssid);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        #endregion

        #region Send 전송


        public void Send(int id, byte[] msg)
        {
            try
            {
                if (_clients.ContainsKey(id))
                {
                    ((Class_ConnectedObject)_clients[id]).Socket.BeginSend(msg, 0, msg.Length, SocketFlags.None, new AsyncCallback(SendReplyCallback), null);
                }
            }
            catch
            {
                return;
            }
        }

        #endregion

        #region Close 연결 종료

        /// <summary>
        /// Closes a client socket and removes it from the client list
        /// </summary>
        /// <param name="client"></param>
        private void CloseClient(Class_ConnectedObject client, int _ssid)
        {
            //PrintConnectionState("Client disconnected" + client);
            client.Close();
            if (_clients.Contains(_ssid))
            {
                _clients.Remove(_ssid);
            }
        }

        /// <summary>
        /// Closes all client and server connections
        /// </summary>
        public void CloseAllSockets()
        {
            // Close all clients
            foreach (Class_ConnectedObject connection in _clients)
            {
                connection.Close();
            }
            // Close server
            _server.Close();
        }


        #endregion

        #region 유틸 Util

        /// <summary>
        /// Checks IAsyncResult for null value
        /// </summary>
        /// <param name="ar"></param>
        /// <param name="err"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private bool CheckState(IAsyncResult ar, out string err, out Class_ConnectedObject client)
        {
            // Initialise
            client = null;
            err = "";

            // Check ar
            if (ar == null)
            {
                err = "Async result null";
                return false;
            }

            // Check client
            client = (Class_ConnectedObject)ar.AsyncState;
            if (client == null)
            {
                err = "Client null";
                return false;
            }

            return true;
        }



        public int ClientListCount()
        {
            int count = _clients.Count;

            return count;
        }

        int[] id = new int[20];
        public int ClientListIP(int i)
        {
            if (_clients.Count != 0)
            {
                if (_clients.ContainsKey(i))
                {
                    id[i] = i;
                }
            }
            return id[i];
        }

        public int ClientID(int i)
        {
            if (_clients.ContainsKey(i))
            {
                int id = i;
                return id;
            }

            return 0;
        }

        public bool ConnectionCheck(int i)
        {
            bool isConnected;
            if (_clients.ContainsKey(i))
            {
                isConnected = true;
            }
            else
            {
                isConnected = false;
            }

            return isConnected;
        }
        #endregion

        #region 안쓰는거 not use 
        /// <summary>
        /// Sends a reply to client
        /// </summary>
        /// <param name="client"></param>
        private void SendReply(Class_ConnectedObject client, int id, byte[] msg)
        {
            if (client == null)
            {
                //Console.WriteLine("Unable to send reply: client null");
                return;
            }

            bool result = client.Socket.Poll(1000000, SelectMode.SelectWrite);
            if (result)
            {
                CloseClient(client, id);
            }

            // Create reply
            client.CreateOutgoingMessage("Message Received");
            //var byteReply = client.OutgoingMessageToBytes();




            // Listen for more incoming messages
            try
            {
                //_clients[id].Socket.BeginSend(msg, 0, msg.Length, SocketFlags.None, new AsyncCallback(SendReplyCallback), client);

            }
            catch (SocketException)
            {
                // Client was forcebly closed on the client side
                CloseClient(client, id);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Handler after a reply has been sent
        /// </summary>
        /// <param name="ar"></param>
        private void SendReplyCallback(IAsyncResult ar)
        {

        }


        /// <summary>
        /// Prints connection 'connected' or 'disconnected' states
        /// </summary>
        /// <param name="msg"></param>
        public void PrintConnectionState(string msg)
        {
            string divider = new String('*', 60);
            Console.WriteLine();
            Console.WriteLine(divider);
            Console.WriteLine(msg);
            Console.WriteLine(divider);
        }



        #endregion

    }
}
