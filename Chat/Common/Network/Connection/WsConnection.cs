﻿namespace Common.Network
{
    using System;
    using WebSocketSharp;
    using WebSocketSharp.Server;
    using Newtonsoft.Json;
    using System.Threading;
    using System.Collections.Concurrent;
    using Packets;

    public class WsConnection : WebSocketBehavior
    {
        #region Fields

        private WsServer _server;
        private readonly ConcurrentQueue<MessageContainer> _sendQueue;
        private int _sending;
        private IHandlerRequestFromClient _handlerRequestFromClient;

        #endregion Fields

        #region Properties

        public Guid Id { get; set; }

        public string Login { get; set; }

        public bool IsConnected => Context.WebSocket?.ReadyState == WebSocketState.Open;

        #endregion Properties

        #region Constructors

        public WsConnection()
        {
            _sendQueue = new ConcurrentQueue<MessageContainer>();
            _sending = 0;

            Id = Guid.NewGuid();
        }

        #endregion Constructors

        #region Methods

        public void AddServer(WsServer server)
        {
            _server = server;
        }
        public void AddParserPacket(IHandlerRequestFromClient handlerRequestFromClient)
        {
            _handlerRequestFromClient = handlerRequestFromClient;
        }

        public void Send(MessageContainer container)
        {
            if (!IsConnected)
                return;

            _sendQueue.Enqueue(container);
            if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
                SendImpl();
        }
        protected override void OnOpen()
        {
            Console.WriteLine("Connect");
            _server.AddConnection(this);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("Disconnect");
            _server.FreeConnection(Id);
            if (Login != null)
            {
                //Очень странная вещь, но напрямую MessageContainer не работает
                string serializedMessages = JsonConvert.SerializeObject(Container.GetContainer(nameof(DisconnectNotice), 
                                                                                               new DisconnectNotice(Login)));
                var message = JsonConvert.DeserializeObject<MessageContainer>(serializedMessages);
                _handlerRequestFromClient.ParsePacket(Id, message);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Console.WriteLine("Message");
            if (e.IsText)
            {
                var message = JsonConvert.DeserializeObject<MessageContainer>(e.Data);
                _handlerRequestFromClient.ParsePacket(Id, message);
            }
        }
        private void SendCompleted(bool completed)
        {
            // При отправке произошла ошибка.
            if (!completed)
            {
                _server.FreeConnection(Id);
                Context.WebSocket.CloseAsync();
                return;
            }

            SendImpl();
        }

        private void SendImpl()
        {
            if (!IsConnected)
                return;

            if (!_sendQueue.TryDequeue(out var message) && Interlocked.CompareExchange(ref _sending, 0, 1) == 1)
                return;

            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            string serializedMessages = JsonConvert.SerializeObject(message, settings);
            SendAsync(serializedMessages, SendCompleted);
        }

        #endregion Methods
    }
}
