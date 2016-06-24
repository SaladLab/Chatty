﻿using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using Domain;

public static class G
{
    static G()
    {
        _logger = LogManager.GetLogger("G");
    }

    // Channel

    private static IChannel _channel;

    public static IChannel Channel
    {
        get { return _channel; }
        set { _channel = value; }
    }

    // Logger

    private static readonly ILog _logger;

    public static ILog Logger
    {
        get { return _logger; }
    }

    // Chat specific data

    public static UserRef User
    {
        get; set;
    }

    public static string UserId
    {
        get; set;
    }
}
