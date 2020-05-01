# Steamworks Networking Example with MonoGame and Unity

I was using MonoGame on one computer and Unity on the other, but both were able to communicate with each other using the [Steamworks.NET library](https://steamworks.github.io/) and Steam's test app ID of 480 (the Spacewar game). For MonoGame implementation, see [Game1.cs](./Game1.cs) and [Steam.cs](./Steam.cs). For Unity, I had attached the [Steam-Unity.cs](./Steam-Unity.cs) script to the main camera.

If you haven't worked with Steamworks before, it's ~~annoying~~ based on a series of callbacks. And these callbacks will only trigger if you continually call `SteamAPI.RunCallbacks()` as part of your game loop. I've included the `GameOverlayActivated_t` callback in my example, because it's probably the easiest of their callbacks to understand and test. It triggers any time the user opens or closes the Steam overlay.

Now for the hard part: lobbies and matchmaking. The basic flow is that a user creates a game lobby, other users join said lobby, and then whenever you're ready to start the game you can switch to sending P2P packets to each other. The first time a P2P packet is sent, it must be accepted as a legitimate session. P2P packets can be consumed as part of your game loop.

Okay, let's go through how that works in my example. Here, the MonoGame instance was acting as the host and was hardcoded to create a new lobby on initialization:

```cs
public static void CreateLobby()
{
    SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
}
```

Either one of these callbacks would give me the lobby ID that was created:

```cs
private static void OnLobbyCreated(LobbyCreated_t result)
{
    Console.WriteLine("lobby id: " + result.m_ulSteamIDLobby);
    lobbyID = result.m_ulSteamIDLobby;
}

private static void OnLobbyEnter(LobbyEnter_t result)
{
    Console.WriteLine("lobby id: " + result.m_ulSteamIDLobby);
    lobbyID = result.m_ulSteamIDLobby;
}
```

I had the guest machine (Unity) join a lobby on initialization, so I had to take the logged ID from the host and plug it in manually before starting the game:

```cs
public static void JoinLobby()
{
    SteamMatchmaking.JoinLobby((CSteamID)109775241110224714);
}
```

When the guest joined, it would trigger the `OnLobbyEnter()` callback for the guest but the `OnLobbyChatUpdate()` for the host. As such, I used the following code in both places so I could let both users record who was in the room with them:

```cs
lobbyMembers = new List<CSteamID>();
int numPlayers = SteamMatchmaking.GetNumLobbyMembers((CSteamID)lobbyID);

for (int i = 0; i < numPlayers; i++)
{
    CSteamID player = SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)lobbyID, i);
    lobbyMembers.Add(player);

    Console.WriteLine(SteamFriends.GetFriendPersonaName(player) + ", " + player);
}
```

I then called the `SendMessage()` method, because I wanted to be able to test out the P2P stuff as soon as possible. I'm not gonna try to explain what's going on in that method. Frankly, it's over my head.

The main thing to know is that, the first time you call `SteamNetworking.SendP2PPacket()`, it will trigger the following callback so your user can accept the session:

```cs
private static void OnSessionRequest(P2PSessionRequest_t result)
{
    Console.WriteLine("session request");

    foreach (CSteamID id in lobbyMembers)
    {
        if (id == result.m_steamIDRemote)
        {
            SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote);

            Console.WriteLine("accepted");
            return;
        }
    }
}
```

From then on, it's simply a matter of making additional `SendMessage()` calls and consuming the P2P packets in your `Update()` method.

Whew! I hope that helps!

---

Huge thanks to the following tutorials/examples for getting me started:

  * <https://github.com/sqrMin1/Steamworks.Net-MonoGame-Integration>

  * <https://github.com/famishedmammal/Steamworks.NET-matchmaking-lobbies-example>

  * <https://blog.theknightsofunity.com/steamworks-and-unity-p2p-multiplayer/>