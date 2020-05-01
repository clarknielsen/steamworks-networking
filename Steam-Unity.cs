using System.Collections;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Steamworks;

public class Steam : MonoBehaviour
{
    public static bool isRunning = false;
    public static bool isOverlay = false;

    private static ulong lobbyID;
    private static List<CSteamID> lobbyMembers;

    private static Callback<GameOverlayActivated_t> m_overlayActivated;

    // networking callbacks
    private static Callback<LobbyCreated_t> m_lobbyCreated;
    private static Callback<LobbyEnter_t> m_lobbyEnter;
    private static Callback<LobbyChatUpdate_t> m_lobbyChatUpdate;
    private static Callback<P2PSessionRequest_t> m_sessionRequest;

    void Start()
    {
        try
        {
            // attempt connecting to steam
            if (!SteamAPI.Init())
            {
                Debug.Log("SteamAPI.Init() failed!");
            }
            else
            {
                Debug.Log("SteamAPI.Init() connected!");
                isRunning = true;

                // register event listeners
                m_overlayActivated = Callback<GameOverlayActivated_t>.Create(OverlayActivated);
                m_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                m_lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
                m_lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                m_sessionRequest = Callback<P2PSessionRequest_t>.Create(OnSessionRequest);

                JoinLobby();
            }
        }
        catch (System.Exception e)
        {
            Debug.Log(e);
        }
    }

    public static void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
    }

    public static void JoinLobby()
    {
        // need to change hardcoded lobby ID
        SteamMatchmaking.JoinLobby((CSteamID)109775241110224714);
    }

    public static void SendMessage()
    {
        string hello = "Hey!";

        // allocate new bytes array and copy string characters as bytes
        byte[] bytes = new byte[hello.Length * sizeof(char)];
        Buffer.BlockCopy(hello.ToCharArray(), 0, bytes, 0, bytes.Length);

        int numPlayers = SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyID);

        for (int i = 0; i < numPlayers; i++)
        {
            CSteamID player = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)lobbyID, i);

            // send message to everyone but yourself
            if (player != SteamUser.GetSteamID())
            {
                SteamNetworking.SendP2PPacket(player, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable);
            }
        }
    }

    void Update()
    {
        if (isRunning)
        {
            SteamAPI.RunCallbacks();

            uint size;

            // check for P2P messages
            while (SteamNetworking.IsP2PPacketAvailable(out size))
            {
                // allocate buffer and needed variables
                var buffer = new byte[size];
                uint bytesRead;
                CSteamID remoteId;

                // read the message into the buffer
                if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId))
                {
                    // convert to string
                    char[] chars = new char[bytesRead / sizeof(char)];
                    Buffer.BlockCopy(buffer, 0, chars, 0, buffer.Length);

                    string message = new string(chars, 0, chars.Length);
                    Debug.Log("Received a message: " + message);
                }
            }
        }
    }

    /* STEAM CALLBACKS */

    // user opens the steam overlay
    private static void OverlayActivated(GameOverlayActivated_t result)
    {
        isOverlay = (result.m_bActive == 1);
    }

    private static void OnLobbyCreated(LobbyCreated_t result)
    {
        Debug.Log("lobby id: " + result.m_ulSteamIDLobby);
        lobbyID = result.m_ulSteamIDLobby;
    }

    private static void OnLobbyEnter(LobbyEnter_t result)
    {
        Debug.Log("lobby id: " + result.m_ulSteamIDLobby);
        lobbyID = result.m_ulSteamIDLobby;

        // save all member IDs
        lobbyMembers = new List<CSteamID>();

        int numPlayers = SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyID);

        for (int i = 0; i < numPlayers; i++)
        {
            CSteamID player = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)lobbyID, i);

            lobbyMembers.Add(player);

            Debug.Log(SteamFriends.GetFriendPersonaName(player) + ", " + player);
        }

        // hardcoded here to send out a P2P message after this user joins the lobby
        SendMessage();
    }

    // triggered when someone new joins the lobby
    private static void OnLobbyChatUpdate(LobbyChatUpdate_t result)
    {
        Debug.Log("lobby chat update");
    }

    // triggered on the first P2P message
    private static void OnSessionRequest(P2PSessionRequest_t result)
    {
        Debug.Log("session request");

        CSteamID remoteID = result.m_steamIDRemote;

        foreach (CSteamID id in lobbyMembers)
        {
            // make sure request came from someone in the same lobby as you
            if (id == remoteID)
            {
                SteamNetworking.AcceptP2PSessionWithUser(remoteID);

                Debug.Log("accepted");
                return;
            }
        }
    }
}
