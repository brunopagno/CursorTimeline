using UnityEngine;
using System.Collections;

public class NetworkManager : MonoBehaviour {

    private const string CHANNEL_NAME = "CursorTimeline";
    private const string SERVER_STYLE_NAME = "CursorTimelineServer";
    private HostData[] hostList;

    // "143.54.13.225" UFRGS
    private string ip = "192.168.1.5";
    private int port = 19876; // this is the game port
    private int maxConnections = 4;

    public GameObject networkObject;

    void Start() {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    #region Networking

    private void StartServer() {
        MasterServer.ipAddress = ip;
        MasterServer.port = 23466; // this is the server port
        Network.InitializeServer(maxConnections, port, Network.HavePublicAddress());
        MasterServer.RegisterHost(CHANNEL_NAME, SERVER_STYLE_NAME);
    }

    private void Disconnect() {
        Network.Disconnect();
    }

    private void RefreshHostList() {
        MasterServer.ipAddress = ip;
        MasterServer.port = 23466; // this is the server port
        MasterServer.RequestHostList(CHANNEL_NAME);
    }

    private void JoinServer(HostData hostData) {
        Network.Connect(hostData);
    }

    void OnFailedToConnect(NetworkConnectionError error) { }

    void OnMasterServerEvent(MasterServerEvent msEvent) {
        if (msEvent == MasterServerEvent.HostListReceived) {
            hostList = MasterServer.PollHostList();
        }
    }

    void OnConnectedToServer() {
        Network.Instantiate(networkObject, Vector3.zero, Quaternion.identity, 0);
    }

    #endregion

    void OnGUI() {
        switch (Network.peerType) {
            case NetworkPeerType.Disconnected:
                GUILayout.Label("Offline");
                if (GUI.Button(new Rect(150, 10, 200, 40), "Start Server")) {
                    StartServer();
                }
                if (GUI.Button(new Rect(150, 60, 200, 140), "Refresh hosts")) {
                    RefreshHostList();
                }
                ip = GUI.TextField(new Rect(150, 220, 200, 40), ip);
                if (hostList != null) {
                    for (int i = 0; i < hostList.Length; i++) {
                        if (GUI.Button(new Rect(150, 260, 200, 140), hostList[i].gameName)) {
                            JoinServer(hostList[i]);
                        }
                    }
                }
                break;
            //case NetworkPeerType.Server:
            //    break;
            case NetworkPeerType.Client:
                if (GUI.Button(new Rect(150, 10, 200, 140), "Disconnect")) {
                    Disconnect();
                }
                break;
        }
    }

}
