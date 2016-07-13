using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using Domain;
using Common.Logging;
using UnityEngine;
using UnityEngine.UI;

public class LoginDialog : UiDialog
{
    public InputField ServerInput;
    public Dropdown ServerProtocol;
    public InputField IdInput;
    public InputField PasswordInput;

    private bool _isLoginBusy;
    private IUserEventObserver _userEventObserver;

    private void Awake()
    {
        ServerProtocol.AddOptions(Enum.GetNames(typeof(ChannelType)).ToList());
    }

    public override void OnShow(object param)
    {
        _userEventObserver = (IUserEventObserver)param;

        var loginServer = PlayerPrefs.GetString("LoginServer", "127.0.0.1:9001");
        var loginServerType = PlayerPrefs.GetString("LoginServerType", "Tcp");
        var loginId = PlayerPrefs.GetString("LoginId", "test");
        var loginPassword = PlayerPrefs.GetString("LoginPassword", "1234");

        ServerInput.text = loginServer;
        ServerProtocol.value = ServerProtocol.options.FindIndex(v => v.text.Equals(loginServerType, StringComparison.OrdinalIgnoreCase));
        IdInput.text = loginId;
        PasswordInput.text = loginPassword;
    }

    public void OnLoginButtonClick()
    {
        if (_isLoginBusy)
            return;

        _isLoginBusy = true;

        PlayerPrefs.SetString("LoginServer", ServerInput.text);
        PlayerPrefs.SetString("LoginServerType", ServerProtocol.captionText.text);
        PlayerPrefs.SetString("LoginId", IdInput.text);
        PlayerPrefs.SetString("LoginPassword", PasswordInput.text);

        var channelType = (ChannelType)Enum.Parse(typeof(ChannelType), ServerProtocol.captionText.text, true);

        StartCoroutine(ProcessLogin(ServerInput.text, channelType, IdInput.text, PasswordInput.text));
    }

    private IEnumerator ProcessLogin(string server, ChannelType type, string id, string password)
    {
        try
        {
            IPEndPoint serverEndPoint;
            try
            {
                serverEndPoint = GetEndPointAddress(server);
            }
            catch (Exception e)
            {
                UiMessageBox.Show("Server address error:\n" + e.ToString());
                yield break;
            }

            var communicator = UnityCommunicatorFactory.Create();
            {
                var channelFactory = communicator.ChannelFactory;
                channelFactory.Type = ChannelType.Tcp;
                channelFactory.ConnectEndPoint = serverEndPoint;
                channelFactory.CreateChannelLogger = () => LogManager.GetLogger("Channel");
                channelFactory.PacketSerializer = PacketSerializer.CreatePacketSerializer<DomainProtobufSerializer>();
            }
            var channel = communicator.CreateChannel();

            // connect to gateway

            var t0 = channel.ConnectAsync();
            yield return t0.WaitHandle;
            if (t0.Exception != null)
            {
                UiMessageBox.Show("Connect error:\n" + t0.Exception.Message);
                yield break;
            }

            // Try Login

            var userLogin = channel.CreateRef<UserLoginRef>();
            var observer = communicator.ObserverRegistry.Create(_userEventObserver);
            var t1 = userLogin.Login(id, password, observer);
            yield return t1.WaitHandle;
            if (t1.Exception != null)
            {
                communicator.ObserverRegistry.Remove(observer);
                var re = t1.Exception as ResultException;
                if (re != null)
                    UiMessageBox.Show("Login error:\n" + re.ResultCode.ToString());
                else
                    UiMessageBox.Show("Login error:\n" + t1.Exception.ToString());
                channel.Close();
                yield break;
            }

            var user = (UserRef)t1.Result;
            if (user.IsChannelConnected() == false)
            {
                var t2 = user.ConnectChannelAsync();
                yield return t2.WaitHandle;
                if (t2.Exception != null)
                {
                    UiMessageBox.Show("ConnectToUser error:\n" + t2.Exception.ToString());
                    channel.Close();
                    yield break;
                }
                channel.Close();
            }

            G.Communicator = communicator;
            G.User = user;
            G.UserId = id;
            Hide(id);
        }
        finally
        {
            _isLoginBusy = false;
        }
    }

    public static IPEndPoint GetEndPointAddress(string address)
    {
        var a = address.Trim();

        // use deault if empty string

        if (string.IsNullOrEmpty(a))
        {
            return G.DefaultServerEndPoint;
        }

        // use 192.168.0.num if *.num when local ip address is 192.168.0.~

        if (a.StartsWith("*."))
        {
            var end = int.Parse(a.Substring(2));
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var abytes = ip.GetAddressBytes();
                    abytes[abytes.Length - 1] = (byte)end;
                    return new IPEndPoint(new IPAddress(abytes), G.DefaultServerEndPoint.Port);
                }
            }
        }

        return IPEndPointHelper.Parse(address, G.DefaultServerEndPoint.Port);
    }
}
