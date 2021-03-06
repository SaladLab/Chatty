# Chatty

[![Build status](https://ci.appveyor.com/api/projects/status/8o9itepr69a83tf2?svg=true)](https://ci.appveyor.com/project/veblush/chatty)
[![Coverage Status](https://coveralls.io/repos/github/SaladLab/Chatty/badge.svg?branch=master)](https://coveralls.io/github/SaladLab/Chatty?branch=master)

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
