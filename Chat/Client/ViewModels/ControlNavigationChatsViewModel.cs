﻿using Client.Model;
using Common.Network;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Client.ViewModels
{
    public class ControlNavigationChatsViewModel : BindableBase
    {
        private Visibility _visibilityView = Visibility.Visible;
        private ObservableCollection<ChatViewModel> _chatCollection;
        private IHandlerMessages _handlerMessages;
        private IHandlerChats _handlerChats;
        private IHandlerConnection _handlerConnection;
        private ChatViewModel _selectedItemChat;
        private string _textButtonChangeViewClients;
        private ChatViewModel _currentViewModelChat;
        public Visibility VisibilityNavigationChat
        {
            get => _visibilityView;
            set => SetProperty(ref _visibilityView, value);
        }
        public ObservableCollection<ChatViewModel> ChatCollection
        {
            get => _chatCollection;
            set => SetProperty(ref _chatCollection, value);
        }
        public ChatViewModel SelectedChat
        {
            get => _selectedItemChat;
            set => SetProperty(ref _selectedItemChat, value, () => ChangeViewModelOfViewChat());
        }
        public string TextButtonChangeViewClients
        {
            get => _textButtonChangeViewClients;
            set => SetProperty(ref _textButtonChangeViewClients, value);
        }
        
        public ChatViewModel CurrentViewModelChat
        {
            get => _currentViewModelChat;
            set => SetProperty(ref _currentViewModelChat, value);
        }
        public DelegateCommand CreateChat { get; }
        public DelegateCommand SelectChange { get; }
        public ControlNavigationChatsViewModel(IHandlerConnection handlerConnection, IHandlerMessages handlerMessages, IHandlerChats handlerChats)
        {
            _chatCollection = new ObservableCollection<ChatViewModel>();

            _handlerChats = handlerChats;
            _handlerChats.AddedChat += OnCreateChat;
            _handlerChats.RemovedChat += OnRemovedChat;
            _handlerConnection = handlerConnection;
            _handlerMessages = handlerMessages;
        }
        private void ChangeViewModelOfViewChat()
        {
            if (SelectedChat != null)
            {
                CurrentViewModelChat = SelectedChat;
                if (!CurrentViewModelChat._chatIsLoad)
                {
                    _handlerMessages.ConnectToChat(CurrentViewModelChat.NumberChat);
                }
            }
        }
        private void OnCreateChat(object sender, AddedChatEventArgs container)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                AccessableClientForAddViewModel allClientViewModel = new AccessableClientForAddViewModel(_handlerConnection, _handlerChats, 
                                                                                       container.AccessNameClientForAdd, container.NumberChat);
                ClientsAtChatViewModel clientsAtChat = new ClientsAtChatViewModel(_handlerConnection, _handlerChats,
                                                                                  container.NumberChat, container.NameOfClientsForAdd);
                ChatViewModel newChat = new ChatViewModel(allClientViewModel, clientsAtChat, _handlerMessages, container.NumberChat);

                ChatCollection.Add(newChat);
            });
        }
        private void OnRemovedChat(object sender, RemovedChatEventArgs container)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                foreach(var item in ChatCollection.ToList())
                {
                    if(item.NumberChat == container.NumberChat)
                    {
                        ChatCollection.Remove(item);
                    }
                }
            });
        }
    }
}
