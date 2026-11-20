# PvP connectivity design (#11)

## Intent

Let two PC VR clients join the same 1v1 session over the internet, from different
locations and networks, and give each a distinct archer role. This is the
foundation for PvP synchronization (#12) and departure/rematch (#13).

## Goals

- Host creates a session and receives a short join code; the second player enters
  the code and connects, with no shared LAN/Wi-Fi and no port forwarding.
- Host is Archer 1 (`Archer1Spawn`), the joining client is Archer 2 (`Archer2Spawn`).
- A third connection attempt is rejected; the session is strictly 1v1.
- Each player sees the opponent's head and hands as simple markers at the
  opponent's spawn, proving the live connection in the headset.
- Connection steps are documented so both developers can reproduce the test.

## Non-goals

- Synchronizing shots, hits, health, turns or the winner (#12).
- Leave/rematch flow beyond showing a "disconnected" status (#13).
- Mode selection and final in-headset match UI (#14).
- Full archer avatar presentation, matchmaking lists or dedicated servers.

## Anti-goals

- Requiring both players on the same network or opening router ports.
- Touching the local-duel code from the unmerged #8 stack; the connectivity
  layer stays independent so #12 can integrate it after #8 merges.

## Constraints

- Unity 6000.3.25f1, OpenXR 1.16.1, Quest 2 over Quest Link on Windows.
- Only one classroom Quest 2: the second client must also run without a headset
  (desktop fallback) for the internet test.
- The project is already linked to Unity Cloud (`cloudProjectId` in
  ProjectSettings); Relay must be enabled for that project in the Unity dashboard.
- Free Unity Gaming Services tier.

## Approaches

| Approach | Trade-off |
| --- | --- |
| **Netcode for GameObjects + Unity Relay, join code (chosen)** | Official Unity stack, project already linked to Unity Cloud, Relay traverses NAT; needs UGS sign-in and Relay enabled in the dashboard. |
| Photon Fusion | Mature, free 20 CCU; adds an external account/App ID and a different API. |
| Mirror/FishNet direct IP | No service dependency; different networks need port forwarding or a VPN, which breaks the "no shared network" criterion. |

Chosen because it meets the cross-network criterion without router changes and
reuses the existing Unity Cloud link.

## Design

- **Packages:** `com.unity.netcode.gameobjects` and `com.unity.services.multiplayer`
  (Relay + anonymous Authentication), at versions compatible with Unity 6000.3.
- **Scene:** new `Assets/_Game/Scenes/PvPConnectionTest.unity` with the arena
  layout, the existing `XRPlayer` rig, a `NetworkManager` using Unity Transport,
  and a connection panel.
- **Session flow:** sign in anonymously → *Host* allocates a 1-slot Relay
  allocation, starts the NGO host and displays the join code → *Join* takes the
  code, joins the allocation and starts the NGO client.
- **Roles:** a pure C# role allocator maps host → Archer 1 and first client →
  Archer 2; NGO connection approval rejects any further client. The role moves the
  local rig to the matching spawn.
- **Presence:** a networked player object per client replicates head and both
  controller poses (owner-authoritative) and renders the opponent as markers.
- **UI:** desktop-window panel (Host / code field / Join / status) typed on the
  PC keyboard; an in-headset text shows role, join code and connection status.
  Desktop runs work without a headset for the second client.
- **Docs:** connection steps in AGENTS.md (and/or `Docs/`), decision entry in
  `Docs/Decisions.md`.
- **Tests:** EditMode tests for role allocation and approval rules; PlayMode test
  starting a local host over Unity Transport (no Relay) to verify roles and the
  1v1 limit. The real internet test is two Windows players on different networks
  (e.g. one on a phone hotspot).

## Left to the builder

- Exact package versions, transport timeouts and marker visuals.
- Join-code input normalization (uppercase, trimming).
