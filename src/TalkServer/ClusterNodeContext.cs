﻿using Akka.Actor;

namespace TalkServer
{
    public class ClusterNodeContext
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
        public IActorRef ClusterNodeContextUpdater;

        // quick access point for actors. but these are shared variables.
        // if there is a neat way to avoid this dirty hack, please improve it.
        public IActorRef UserTable;
        public IActorRef UserTableContainer;
        public IActorRef RoomTable;
        public IActorRef RoomTableContainer;
    }
}
