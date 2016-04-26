# Chatty

Reference chatting application for using Akka.Interfaced and Akka.Interfaced.SlimSocket.

![Screenshot](https://raw.githubusercontent.com/SaladLab/Chatty/master/docs/ScreenShot.jpg)

## How to run

### Prerequisites

- Redis 2.8 or later
- Visual Studio 2015 or later (it's not mandatory if you can build projects)
- Unity 5.3 or later

### Steps

- Make sure Redis is running well.
  - Server connects to local Redis.
  Run Server
  - Open Chatty.sln with Visual Studio.
  - Run TalkServer.
- Run Client (Unity version)
  - Open src/TalkClient.Unity3D with Unity
  - Open Scene/ChatScene and run.
- Run Client (Console version)
  - Open Chatty.sln with Visual Studio.
  - Run TalkClient.Console.

## Known issues

#### Client cannot connect to Server sometimes

Please retry to connect. It's caused by SlimSocket and will be fixed.
