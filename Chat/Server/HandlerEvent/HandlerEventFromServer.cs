﻿namespace Server.Network
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Concurrent;
	using System.Threading.Tasks;
	using Common.Network;
	using Common.Network.Packets;
    using System.Linq;
	using DataBase;
    public class HandlerRequestFromClient
	{
		#region Const

		private const int NumberGeneralChat = 1;

        #endregion Const

        #region Fields

        private ConcurrentDictionary<string, ClientProperties> _cachedClientProperies;//Ключ - имя пользователя
		private ConcurrentDictionary<int, InfoChat> _infoAllChat;//Ключ - номер комнаты
		private ConcurrentDictionary<int, List<MessageInfo>> _MessagesAtChat;//Ключ - номер комнаты
		private IHandlerRequestToData _data;
		private ITransportServer _server;

		#endregion Fields

		#region Constructors

		public HandlerRequestFromClient(ITransportServer server, IHandlerRequestToData data)
		{
			_server = server;

			_server.ClientConnected += OnConnect;
			_server.ClientDisconnected += OnDisconnect;
			_server.MessageReceived += OnMessage;
			_server.ConnectedToChat += OnChatOpened;
			_server.AddedChat += OnAddedChat;
			_server.RemovedChat += OnRemovedChat;
			_server.AddedClientsToChat += OnAddedClientsToChat;
			_server.RemovedClientsFromChat += OnRemovedClientsFromChat;

			_data = data;

			_cachedClientProperies = _data.GetInfoAboutLinkClientToChat();
			_infoAllChat = _data.GetInfoAboutAllChat();
			_MessagesAtChat = _data.GetAllMessageFromChats();
		}

		#endregion Constructors

		#region Methods

		//Везде доделать обновления списка с информацией о клиенте
		public async void OnConnect(object sender, ClientConnectedEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.ClientName,out ClientProperties clientProperties))
			{
				if(clientProperties.IdConnection == Guid.Empty)
				{
					await Task.Run(() => _server.Send(new List<Guid>() { clientProperties.IdConnection }, 
										 new ConnectionResponse(ResultRequest.Ok, "Пользователь подключен", 
																new Dictionary<int, string>(), 
																new Dictionary<string, bool>()).GetContainer()));
					clientProperties.IdConnection = container.ClientId;
					await Task.Run(() => _server.SendAll(new ConnectionNoticeForClients(container.ClientName).GetContainer()));
				}
				else
				{
					await Task.Run(() => _server.Send(new List<Guid>() { clientProperties.IdConnection }, 
										 new ConnectionResponse(ResultRequest.Failure, "Такой пользователь уже есть",
																new Dictionary<int, string>(),
																new Dictionary<string, bool>()).GetContainer()));
					await Task.Run(() => _server.FreeConnection(clientProperties.IdConnection));
				}
			}
			else
			{
				//Первый аргумент номер комнаты, второй словарь клиентов и их активность
				Dictionary<string, bool> chatInfoForClient = new Dictionary<string, bool>();
				if(_infoAllChat.TryGetValue(NumberGeneralChat, out InfoChat infoGeneralChat))
				{
					foreach (var nameClientAtChat in infoGeneralChat.NameOfClients)
					{
						if (_cachedClientProperies.TryGetValue(nameClientAtChat, out ClientProperties clientAtChat))
                        {
							if(clientAtChat.IdConnection != Guid.Empty)
                            {
								chatInfoForClient.Add(nameClientAtChat, true);
							}
                            else
                            {
								chatInfoForClient.Add(nameClientAtChat, false);
							}
						}
					}
				}
				await Task.Run(() => _server.Send(new List<Guid>() { clientProperties.IdConnection }, 
									 new ConnectionResponse(ResultRequest.Ok, "Новый пользователь зарегистрирован",
															new Dictionary<int, string>() { { NumberGeneralChat, infoGeneralChat.OwnerChat } }, 
															chatInfoForClient).GetContainer()));

				_cachedClientProperies.TryAdd(container.ClientName, new ClientProperties { IdConnection = container.ClientId, NumbersChat = new List<int>() { NumberGeneralChat } });

				await Task.Run(() => _server.SendAll(new ConnectionNoticeForClients(container.ClientName).GetContainer()));

				if (!await Task.Run(() => _data.AddNewClient(new ClientInfo { NameOfClient = container.ClientName})))
				{
					//Ошибка, не получилось записать
				}
			}
		}

		//Если не будет существовать общего чата, то нужно будет рассылать сообщение о подключении только тем пользователям, 
		//которые находятся в одном чате с подключившимся
		/*private async Task ClientNotice(string nameClientConnected)//Доделать:рассылку другим пользователям, что зашел пользователь
		{
			List<Guid> ChatForNotice = new List<Guid>();
			if(_cachedClientProperies.TryGetValue(nameClientConnected, out ClientProperties clientProperties))
            {
				foreach(var numberChat in clientProperties.NumbersChat)
                {
					if(_infoAllChat.TryGetValue(numberChat, out InfoChat infoChat))
                    {
						foreach(var nameClient in infoChat.NameClients)
                        {
							if(_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientPropertiesForNotice) && clientPropertiesForNotice.IdConnection != Guid.Empty)
                            {
								ChatForNotice.Add(clientPropertiesForNotice.IdConnection);
							}	
						}
					}
                }
				var DistinctListClients = ChatForNotice.Distinct().ToList();
				await Task.Run(() => _server.Send(DistinctListClients, new ConnectionNoticeForClients(nameClientConnected).GetContainer()));
			}
		}*/

		public async void OnDisconnect(object sender, ClientDisconnectedEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.NameOfClient,out ClientProperties clientProperties) && clientProperties.IdConnection != Guid.Empty)
			{
				await Task.Run(() => _server.FreeConnection(clientProperties.IdConnection));

				await Task.Run(() => _server.SendAll(new DisconnectRequest(container.NameOfClient).GetContainer()));

				_cachedClientProperies.TryUpdate(container.NameOfClient, new ClientProperties{ IdConnection = Guid.Empty, NumbersChat = clientProperties.NumbersChat }, clientProperties);
			}
		}

		public async void OnMessage(object sender, MessageReceivedEventArgs container)
		{
			if(_cachedClientProperies.ContainsKey(container.NameOfClient)
			   && _infoAllChat.TryGetValue(container.NumberChat, out InfoChat infoChat))
			{
				List<Guid> idClientsForSendMessage = new List<Guid>();//Создание списка id для рассылки им сообщений
				foreach (var nameClient in infoChat.NameOfClients)
				{
					if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientProperties) && clientProperties.IdConnection != Guid.Empty)
					{
						idClientsForSendMessage.Add(clientProperties.IdConnection);
					}
				}
				await Task.Run(() =>  _server.Send(idClientsForSendMessage, new MessageResponse(container.NameOfClient, container.Message, container.NumberChat).GetContainer()));

				DateTime time = DateTime.Now;

				if(_MessagesAtChat.TryGetValue(container.NumberChat,out List<MessageInfo> allMessageAtChat))
                {
					var lastValueMessages = allMessageAtChat;
					allMessageAtChat.Add(new MessageInfo { FromMessage = container.NameOfClient, Text = container.Message, Time = time });
					_MessagesAtChat.TryUpdate(container.NumberChat, allMessageAtChat, lastValueMessages);
				}

				if (!await Task.Run(() => _data.AddNewMessage(new MessageInfoForDb { NumberChat = container.NumberChat, 
																					 FromMessage = container.NameOfClient, 
																					 Text = container.Message, Time = time })))
				{
					//Сообщение не удалось добавить, сигнал серверу на запрет приема сообщений до добавления сообщения
				}
			}
			else
			{
				//Ошибка, не существует либо пользователя, либо комнаты
			}
		}

		public async void OnChatOpened(object sender, ConnectionToChatEventArgs container)
		{
			if(_cachedClientProperies.TryGetValue(container.NameOfClient,out ClientProperties clientProperties)
			   && _infoAllChat.TryGetValue(container.NumberChat, out InfoChat infoChat))
			{
				foreach(var nameClientAtChat in infoChat.NameOfClients)
				{
					if(nameClientAtChat == container.NameOfClient)
					{
						if(_MessagesAtChat.TryGetValue(container.NumberChat, out List<MessageInfo> messages) && clientProperties.IdConnection != Guid.Empty)
						{
							await Task.Run(() => _server.Send(new List<Guid>() { clientProperties.IdConnection }, new ConnectToChatResponse(container.NumberChat, messages).GetContainer()));
						}
					}
				}
			}
			else
			{
				//Ошибка
				return;
			}
		}

		public async void OnAddedChat(object sender, AddedChatEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.NameOfClientSender, out ClientProperties clientSenderProperties))
			{
				int numberChat = await Task.Run(() => _data.CreatNewChat(new CreatingChatInfo { NameOfClientSender = container.NameOfClientSender, NameOfClients = container.NameOfClientsForAdd }));
				if (numberChat != -1)
				{
					List<Guid> idClientsForSendMessage = new List<Guid>();//Создание списка id для рассылки им сообщений
					foreach (var nameClient in container.NameOfClientsForAdd)
					{
						if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientOfChat))
						{
							if (clientOfChat.IdConnection != Guid.Empty)
							{
								idClientsForSendMessage.Add(clientOfChat.IdConnection);
							}
							var lastValueChatsClient = clientOfChat;
							clientOfChat.NumbersChat.Add(numberChat);
							_cachedClientProperies.TryUpdate(nameClient, clientOfChat, lastValueChatsClient);
						}
					}
					if (clientSenderProperties.IdConnection != Guid.Empty)
					{
						idClientsForSendMessage.Add(clientSenderProperties.IdConnection);
					}

					await Task.Run(() => _server.Send(idClientsForSendMessage, new AddNewChatResponse(numberChat, container.NameOfClientsForAdd).GetContainer()));
					_infoAllChat.TryAdd(numberChat, new InfoChat { OwnerChat = container.NameOfClientSender, NameOfClients = container.NameOfClientsForAdd });
				}
				else
				{
					//Ошибка добавления
				}
			}
			else
			{
				//Ошибка, не существует клиента
			}
		}
		public async void OnRemovedChat(object sender, RemovedChatEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.NameOfClient, out ClientProperties clientProperties)
				&& _infoAllChat.TryGetValue(container.NumberChat,out InfoChat infoChat))
			{
				if(container.NameOfClient == infoChat.OwnerChat)
				{
					List<Guid> idClientsForSendMessage = new List<Guid>();//Создание списка id для рассылки им сообщений
					foreach (var nameClient in infoChat.NameOfClients)
					{
						if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientOfChat) && clientOfChat.IdConnection != Guid.Empty)
						{
							idClientsForSendMessage.Add(clientOfChat.IdConnection);
						}
						var lastValueChatsClient = clientOfChat;
						clientOfChat.NumbersChat.Remove(container.NumberChat);
						_cachedClientProperies.TryUpdate(nameClient, clientOfChat, lastValueChatsClient);
					}

					await Task.Run(() => _server.Send(idClientsForSendMessage, new RemoveChatResponse(container.NumberChat).GetContainer()));

					if (!_infoAllChat.TryRemove(container.NumberChat, out InfoChat infoRemovedChat))
                    {
						//Не нашел такую комнату
                    }

					if (!await Task.Run(() => _data.RemoveChat(container.NumberChat)))
					{
						//Ошибка на удаление в БД
					}
				}
			}
			else
			{
				//Ошибка
				return;
			}
		}

		public async void OnAddedClientsToChat(object sender, AddedClientsToChatEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.NameOfClientSender, out ClientProperties clientProperties)
				&& _infoAllChat.TryGetValue(container.NumberChat, out InfoChat infoChat))
			{
				if(container.NameOfClientSender == infoChat.OwnerChat)
				{
					InfoChat buffer = infoChat;
					List<Guid> idClientsForSendMessage = new List<Guid>();//Создание списка id для рассылки им сообщений

					
					foreach (var nameClient in container.NameOfClients)
					{
						if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientOfChat))
						{
							if(clientOfChat.IdConnection != Guid.Empty)
                            {
								idClientsForSendMessage.Add(clientOfChat.IdConnection);
							}
							var lastValueChatsClient = clientOfChat;
							clientOfChat.NumbersChat.Add(container.NumberChat);
							_cachedClientProperies.TryUpdate(nameClient, clientOfChat, lastValueChatsClient);
						}
						else
                        {
							container.NameOfClients.Remove(nameClient);
						}
					}

					infoChat.NameOfClients.Union(container.NameOfClients);
					foreach (var nameClient in infoChat.NameOfClients)
					{
						if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientOfChat))
						{
							if (clientOfChat.IdConnection != Guid.Empty)
							{
								idClientsForSendMessage.Add(clientOfChat.IdConnection);
							}
						}
                    }

					await Task.Run(() => _server.Send(idClientsForSendMessage, new AddNewClientToChatResponse(container.NameOfClientSender, container.NameOfClients, container.NumberChat).GetContainer()));

					_infoAllChat.TryUpdate(container.NumberChat, infoChat, buffer);

					if (!await Task.Run(() => _data.AddClientToChat(new AddClientToChat { NumberChat = container.NumberChat, NameOfClients = container.NameOfClients })))
					{
						//Ошибка на добавление в БД
					}
				}
			}
			else
			{
				//Ошибка
				return;
			}
		}
		public async void OnRemovedClientsFromChat(object sender, RemovedClientsFromChatEventArgs container)
		{
			if (_cachedClientProperies.TryGetValue(container.NameOfClient, out ClientProperties clientProperties)
				&& _infoAllChat.TryGetValue(container.NumberChat, out InfoChat infoChat))
			{
				if (container.NameOfClient == infoChat.OwnerChat)
				{
					InfoChat buffer = infoChat;
					List<Guid> idClientsForSendMessage = new List<Guid>();//Создание списка id для рассылки им сообщений
					foreach (var nameClient in infoChat.NameOfClients)
					{
						if (_cachedClientProperies.TryGetValue(nameClient, out ClientProperties clientOfChat) && clientOfChat.IdConnection != Guid.Empty)
						{
							idClientsForSendMessage.Add(clientOfChat.IdConnection);
						}
					}

					foreach(var nameClientForRemove in container.NameOfClients)
                    {
						infoChat.NameOfClients.Remove(nameClientForRemove);
						var lastValueChatsClient = clientProperties;
						clientProperties.NumbersChat.Remove(container.NumberChat);
						_cachedClientProperies.TryUpdate(nameClientForRemove, clientProperties, lastValueChatsClient);
					}

					await Task.Run(() => _server.Send(idClientsForSendMessage, new RemoveClientFromChatResponse(container.NameOfClient, container.NameOfClients, container.NumberChat).GetContainer()));

					_infoAllChat.TryUpdate(container.NumberChat, infoChat, buffer);

					if (!await Task.Run(() => _data.RemoveClientFromChat(new RemoveClientFromChat {NumberChat = container.NumberChat, NameOfClients = container.NameOfClients})))
					{
						//Ошибка на удаление в БД
					}
				}
			}
			else
			{
				//Ошибка
				return;
			}
		}

		#endregion Methods
	}
}
