using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using Domain;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;

public class LoginDialog : UiDialog
{
    public InputField ServerInput;
    public InputField IdInput;
    public InputField PasswordInput;

    private bool _isLoginBusy;
    private IUserEventObserver _userEventObserver;

    public override void OnShow(object param)
    {
        _userEventObserver = (IUserEventObserver)param;

        var loginServer = PlayerPrefs.GetString("LoginServer", "127.0.0.1:9001");
        var loginId = PlayerPrefs.GetString("LoginId", "test");
        var loginPassword = PlayerPrefs.GetString("LoginPassword", "1234");

        ServerInput.text = loginServer;
        IdInput.text = loginId;
        PasswordInput.text = loginPassword;
    }

    public void OnLoginButtonClick()
    {
        if (_isLoginBusy)
            return;

        _isLoginBusy = true;

        PlayerPrefs.SetString("LoginServer", ServerInput.text);
        PlayerPrefs.SetString("LoginId", IdInput.text);
        PlayerPrefs.SetString("LoginPassword", PasswordInput.text);

        StartCoroutine(ProcessLogin(ServerInput.text, IdInput.text, PasswordInput.text));
    }

    private IEnumerator ProcessLogin(string server, string id, string password)
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
                UiMessageBox.ShowMessageBox("Server address error:\n" + e.ToString());
                yield break;
            }

            var channelFactory = ChannelFactoryBuilder.Build<DomainProtobufSerializer>(
                endPoint: serverEndPoint,
                createChannelLogger: () => LogManager.GetLogger("Channel"));
            channelFactory.Type = ChannelType.Tcp;
            var channel = channelFactory.Create();

            // connect to gateway

            var t0 = channel.ConnectAsync();
            yield return t0.WaitHandle;
            if (t0.Exception != null)
            {
                UiMessageBox.ShowMessageBox("Connect error:\n" + t0.Exception.Message);
                yield break;
            }

            // Try Login

            var userLogin = channel.CreateRef<UserLoginRef>();
            var observer = channel.CreateObserver(_userEventObserver);
            var t1 = userLogin.Login(id, password, observer);
            yield return t1.WaitHandle;
            if (t1.Exception != null)
            {
                channel.RemoveObserver(observer);
                var re = t1.Exception as ResultException;
                if (re != null)
                    UiMessageBox.ShowMessageBox("Login error:\n" + re.ResultCode.ToString());
                else
                    UiMessageBox.ShowMessageBox("Login error:\n" + t1.Exception.ToString());
                channel.Close();
                yield break;
            }

            G.Channel = channel;
            G.User = (UserRef)t1.Result;
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
