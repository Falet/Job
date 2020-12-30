﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Model
{
    public class RemovedClientsFromChatForVMEventArgs
    {
        #region Properties

        public string NameOfRemover { get; }

        public Dictionary<string, bool> Clients { get; }

        public int NumberChat { get; }

        #endregion Properties

        #region Constructors

        public RemovedClientsFromChatForVMEventArgs(string clientName, int numberChat, Dictionary<string, bool> clients)
        {
            NameOfRemover = clientName;
            Clients = clients;
            NumberChat = numberChat;
        }

        #endregion Constructors
    }
}
