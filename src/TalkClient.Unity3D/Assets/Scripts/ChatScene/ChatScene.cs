using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Domain;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Client;

public class ChatScene : MonoBehaviour, IUserEventObserver
{
    public ControlPanel ControlPanel;
    public GameObject ContentPanel;
    public GameObject ChatPanelTemplate;

    private bool _isBusy;
    private string _currentRoomName;

    private class RoomItem
    {
        public ChatPanel ChatPanel;
        public OccupantRef Occupant;
        public RoomObserver Observer;
        public IChannel Channel;
    }
    private Dictionary<string, RoomItem> _roomItemMap = new Dictionary<string, RoomItem>();

    private void Start()
    {
        UiManager.Initialize();

        ControlPanel.LogoutButtonClicked = OnLogoutButtonClick;
        ControlPanel.RoomButtonClicked = OnRoomButtonClick;
        ControlPanel.RoomItemSelected = OnRoomItemClick;

        ChatPanelTemplate.SetActive(false);
        CheckLoginedOrTryToLogin();
    }

    private void CheckLoginedOrTryToLogin()
    {
        if (G.User != null || _isBusy)
            return;

        _isBusy = true;
        StartCoroutine(ProcessLogin());
    }

    private IEnumerator ProcessLogin()
    {
        try
        {
            var loginDialog = UiManager.Instance.ShowModalRoot<LoginDialog>(this);
            yield return StartCoroutine(loginDialog.WaitForHide());

            G.Communicator.Channel.StateChanged += (_, state) => { if (state == ChannelStateType.Closed) ChannelEventDispatcher.Post(OnChannelClose, _); };

            if (loginDialog.ReturnValue != null)
            {
                var result = (string)loginDialog.ReturnValue;
                ControlPanel.SetUserName(result);

                yield return StartCoroutine(ProcessEnterRoom(G.User, "#general"));
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void OnChannelClose(object channel)
    {
        // clear rooms

        foreach (var item in _roomItemMap)
        {
            ControlPanel.DeleteRoomItem(item.Key);
            DestroyObject(item.Value.ChatPanel.gameObject);
        }
        _roomItemMap.Clear();

        // clear global connection state and try to reconnect

        G.Communicator.Channel.Close();
        G.Communicator = null;
        G.User = null;
        G.UserId = null;

        CheckLoginedOrTryToLogin();
    }

    private void OnRoomTextClick()
    {
        if (G.User == null || _isBusy)
            return;

        _isBusy = true;
        StartCoroutine(ProcessSelectRoomAndEnter());
    }

    private void OnLogoutButtonClick()
    {
        if (G.Communicator.Channel != null)
        {
            G.Communicator.Channel.Close();
        }
    }

    private void OnRoomButtonClick()
    {
        if (G.User == null || _isBusy)
            return;

        _isBusy = true;
        StartCoroutine(ProcessSelectRoomAndEnter());
    }

    private IEnumerator ProcessSelectRoomAndEnter()
    {
        try
        {
            var t1 = G.User.GetRoomList();
            yield return t1.WaitHandle;

            if (t1.Exception != null)
            {
                UiMessageBox.ShowMessageBox("GetRoomList error:\n" + t1.Exception.Message);
                yield break;
            }

            var roomDialog = UiManager.Instance.ShowModalRoot<RoomDialog>(
                new RoomDialog.Argument
                {
                    CurrentRoomName = _currentRoomName,
                    RoomList = t1.Result
                });
            yield return StartCoroutine(roomDialog.WaitForHide());

            if (roomDialog.ReturnValue == null)
                yield break;

            var roomName = (string)roomDialog.ReturnValue;
            if (_roomItemMap.ContainsKey(roomName))
            {
                OnRoomItemClick(roomName);
                yield break;
            }

            yield return StartCoroutine(ProcessEnterRoom(G.User, roomName));
        }
        finally
        {
            _isBusy = false;
        }
    }

    private IEnumerator ProcessEnterRoom(UserRef user, string roomName)
    {
        // Spawn new room item

        var go = UiHelper.AddChild(ContentPanel, ChatPanelTemplate);
        var chatPanel = go.GetComponent<ChatPanel>();

        // Try to enter the room

        var observer = G.Communicator.ObserverRegistry.Create<IRoomObserver>(chatPanel);
        observer.GetEventDispatcher().Pending = true;
        observer.GetEventDispatcher().KeepingOrder= true;
        var t1 = user.EnterRoom(roomName, observer);
        yield return t1.WaitHandle;

        if (t1.Status != TaskStatus.RanToCompletion)
        {
            G.Communicator.ObserverRegistry.Remove(observer);
            DestroyObject(go);
            UiMessageBox.ShowMessageBox("Enter room error:\n" + t1.Exception);
            yield break;
        }

        // Spawn new room item

        var occupant = (OccupantRef)t1.Result.Item1;
        if (occupant.IsChannelConnected() == false)
        {
            yield return occupant.ConnectChannelAsync().WaitHandle;
        }
        chatPanel.SetOccupant(occupant);
        chatPanel.SetRoomInfo(t1.Result.Item2);
        chatPanel.ExitButtonClicked = () => OnRoomExitClick(roomName);

        var item = new RoomItem
        {
            ChatPanel = chatPanel,
            Occupant = occupant,
            Observer = (RoomObserver)observer,
            Channel = (IChannel)occupant.RequestWaiter
        };
        _roomItemMap.Add(roomName, item);
        ControlPanel.AddRoomItem(roomName);
        observer.GetEventDispatcher().Pending = false;

        item.Channel.StateChanged += (_, state) =>
        {
            ChannelEventDispatcher.Post(o =>
            {
                if (state == ChannelStateType.Closed)
                {
                    if (_roomItemMap.ContainsKey(roomName))
                        OnRoomExitClick(roomName);
                }
            });
        };

        // Select

        OnRoomItemClick(roomName);
    }

    private void OnRoomItemClick(string roomName)
    {
        foreach (var item in _roomItemMap)
        {
            item.Value.ChatPanel.gameObject.SetActive(item.Key == roomName);
        }

        ControlPanel.SelectRoomItem(roomName);
        _currentRoomName = roomName;
    }

    private void OnRoomExitClick(string roomName)
    {
        if (roomName == "#general")
            return;

        _isBusy = true;
        StartCoroutine(ProcessExitFromRoom(G.User, roomName));
    }

    private IEnumerator ProcessExitFromRoom(UserRef user, string roomName)
    {
        try
        {
            var t1 = user.ExitFromRoom(roomName);
            yield return t1.WaitHandle;
            if (t1.Status != TaskStatus.RanToCompletion)
            {
                UiMessageBox.ShowMessageBox("Exit room error:\n" + t1.Exception.ToString());
                yield break;
            }

            RoomItem item;
            if (_roomItemMap.TryGetValue(roomName, out item) == false)
                yield break;

            _roomItemMap.Remove(roomName);
            Destroy(item.ChatPanel.gameObject);
            G.Communicator.ObserverRegistry.Remove(item.Observer);
            ControlPanel.DeleteRoomItem(roomName);
            if (item.Channel != G.Communicator.Channel)
                item.Channel.Close();

            OnRoomItemClick("#general");
        }
        finally
        {
            _isBusy = false;
        }
    }

    void IUserEventObserver.Whisper(ChatItem chatItem)
    {
        if (string.IsNullOrEmpty(_currentRoomName))
            return;

        _roomItemMap[_currentRoomName].ChatPanel.AppendChatMessage(string.Format(
            "@ <color=#800080ff><b>{0}</b></color>: {1}",
            chatItem.UserId, chatItem.Message));
    }

    void IUserEventObserver.Invite(string invitorUserId, string roomName)
    {
        if (string.IsNullOrEmpty(_currentRoomName))
            return;

        _roomItemMap[_currentRoomName].ChatPanel.AppendChatMessage(string.Format(
            "! <color=#800080ff><b>{0}</b></color> invites you from {1}",
            invitorUserId, roomName));
    }
}
