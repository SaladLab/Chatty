using System;
using System.Collections;
using System.Net;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Domain;
using Akka.Interfaced.SlimSocket.Client;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;
using TypeAlias;

public class LoginDialog : UiDialog
{
    public InputField ServerInput;
    public InputField IdInput;
    public InputField PasswordInput;
    public Text MessageText;

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

        SetMessage(null);
    }

    public void OnLoginButtonClick()
    {
        if (_isLoginBusy)
            return;

        _isLoginBusy = true;
        SetMessage(null);

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
                serverEndPoint = CreateIPEndPoint(server);
            }
            catch (Exception e)
            {
                SetMessage(e.ToString());
                yield break;
            }

            G.Comm = CommunicatorHelper.CreateCommunicator<DomainProtobufSerializer>(G.Logger, serverEndPoint);
            G.Comm.Start();

            // Try Login

            var userLogin = G.Comm.CreateRef<UserLoginRef>();
            var observer = G.Comm.CreateObserver<IUserEventObserver>(_userEventObserver);
            var t1 = userLogin.Login(id, password, observer);
            yield return t1.WaitHandle;
            if (t1.Exception != null)
            {
                var re = t1.Exception as ResultException;
                if (re != null)
                {
                    SetMessage(re.ResultCode.ToString());
                }
                else
                {
                    SetMessage(t1.Exception.ToString());
                }
                yield break;
            }

            G.User = (UserRef)t1.Result;
            G.UserId = id;
            Hide(id);
        }
        finally
        {
            _isLoginBusy = false;
        }
    }

    private void SetMessage(string message)
    {
        TweenHelper.KillAllTweensOfObject(MessageText);

        if (string.IsNullOrEmpty(message))
        {
            MessageText.text = "";
        }
        else
        {
            MessageText.text = message;
            MessageText.DOFade(1f, 0.5f);
            MessageText.DOFade(0f, 0.5f).SetDelay(5);
        }
    }

    // http://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
    private static IPEndPoint CreateIPEndPoint(string endPoint)
    {
        string[] ep = endPoint.Split(':');
        if (ep.Length < 2) throw new FormatException("Invalid endpoint format");
        IPAddress ip;
        if (ep.Length > 2)
        {
            if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
            {
                throw new FormatException("Invalid ip-adress");
            }
        }
        else
        {
            if (!IPAddress.TryParse(ep[0], out ip))
            {
                throw new FormatException("Invalid ip-adress");
            }
        }
        int port;
        if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
        {
            throw new FormatException("Invalid port");
        }
        return new IPEndPoint(ip, port);
    }
}
