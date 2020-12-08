﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TestServer.Network
{
    public class RemoveUserFromChatRequest
    {
        #region Properties

        public string ClientName { get; }

        public List<string> Users { get; }

        public int Room { get; }

        #endregion Properties

        #region Constructors

        public RemoveUserFromChatRequest(string clientName, List<string> users, int room)
        {
            ClientName = clientName;
            Users = users;
            Room = room;
        }

        #endregion Constructors

        #region Methods

        public MessageContainer GetContainer()
        {
            var container = new MessageContainer
            {
                Identifier = nameof(RemoveUserFromChatRequest),
                Payload = this
            };

            return container;
        }

        #endregion Methods
    }
}
