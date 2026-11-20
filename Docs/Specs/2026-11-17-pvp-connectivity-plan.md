# PvP Connectivity Implementation Plan (#11)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two PC VR clients join one 1v1 session over the internet through a Unity Relay join code and receive distinct archer roles.

**Architecture:** Pure C# rules (roles, seats, join codes, auth profile, status text) sit beside thin Unity adapters: a Relay/NGO connection controller, a networked presence object and a local rig binder. An Editor menu generates the test scene, the player prefab and a Windows player, so no scene YAML is hand-written.

**Tech Stack:** Unity 6000.3.25f1, Netcode for GameObjects 2.14.0, Unity Gaming Services Multiplayer 2.5.0 (Relay + Authentication), Unity Transport, OpenXR 1.16.1, NUnit (Unity Test Framework).

**Spec:** `Docs/Specs/2026-11-17-pvp-connectivity-design.md`

## Global Constraints

- Editor 6000.3.25f1 is not installed on the authoring Mac: code is written without compiling. Every "Run" step happens on the Windows lab PC (or any machine with that Editor) before the PR leaves draft.
- Packages: `com.unity.netcode.gameobjects` `2.14.0`, `com.unity.services.multiplayer` `2.5.0`.
- Namespace `BowMan.Networking`; runtime assembly `BowMan.Networking` under `Assets/_Game/Scripts/Networking/`.
- One-to-two-line purpose comment above every function (AGENTS.md, D007).
- Host = Archer 1 at `(0, 0, -10)` facing +Z; joining client = Archer 2 at `(0, 0, 10)` facing -Z.
- Relay allocation for exactly 1 remote connection, protocol `"dtls"`.
- No dependency on unmerged #8 code; do not modify existing scenes or `EditorBuildSettings`.

## Review Focus

1. Two instances on one PC (Editor + player) share the anonymous-auth cache, so both get the same player ID and Relay refuses the join → each process signs in with its own auth profile (`AuthProfile` tests in Task 1).
2. Join code typed in lowercase, with spaces or pasted with a newline → it still joins (`JoinCode` tests in Task 1).
3. A third client, or two clients racing for the seat → exactly one gets Archer 2 and the other is refused with "Match is full" (allocator tests in Task 1).
4. The client leaves and joins again with a new client ID → it gets Archer 2 again (allocator release test in Task 1).
5. Relay not enabled, no internet or a wrong code → the panel shows a readable error and Host/Join work again with no restart (`ConnectionStatus` tests in Task 2).

---

### Task 1: Packages, assemblies and pure connection rules

**Files:**
- Modify: `Packages/manifest.json` (add the two packages)
- Create: `Assets/_Game/Scripts/Networking/BowMan.Networking.asmdef`
- Create: `Assets/_Game/Scripts/Networking/ArcherRole.cs`, `ArcherSeatAllocator.cs`, `JoinCode.cs`, `AuthProfile.cs`
- Create: `Assets/_Game/Tests/Networking/BowMan.Networking.EditorTests.asmdef`
- Test: `Assets/_Game/Tests/Networking/ConnectionRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum ArcherRole { None, Archer1, Archer2 }`
  - `class ArcherSeatAllocator { bool TryAssign(ulong clientId, out ArcherRole role); bool Release(ulong clientId); ArcherRole RoleOf(ulong clientId); int Occupied { get; } void Clear(); const string FullReason = "Match is full"; }`. It assigns the lowest free seat, Archer1 before Archer2, and is idempotent for a client that already holds a seat.
  - `static class JoinCode { string Normalize(string raw); bool IsValid(string normalized); }`. It removes all whitespace and upper-cases the code (invariant culture). A valid code is non-empty, 6 to 12 characters, ASCII letters and digits only.
  - `static class AuthProfile { string ForProcess(int processId, string suffix = null); }`. It returns `"p" + processId` plus `"-" + suffix` when given, keeps only `[A-Za-z0-9_-]` and truncates to 30 characters.
- asmdef `BowMan.Networking` references: `Unity.Netcode.Runtime`, `Unity.Networking.Transport`, `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Multiplayer`, `Unity.XR.Management`, `Unity.InputSystem`.
- asmdef `BowMan.Networking.EditorTests`: Editor-only, `optionalUnityReferences: ["TestAssemblies"]`, references `BowMan.Networking`, `autoReferenced: false`.

- [ ] **Step 1: Write failing tests** in `ConnectionRulesTests`:
  - `FirstClientIsArcher1SecondIsArcher2`: assign 0 → Archer1, assign 1 → Archer2.
  - `ThirdClientIsRefused`: after 0 and 1, `TryAssign(2)` is false, role None, `Occupied == 2`.
  - `ReassigningSameClientKeepsSeat`: `TryAssign(1)` twice → Archer2 both times, `Occupied == 2`.
  - `ReleasedSeatGoesToNextClient`: assign 0 and 1, release 1, assign 5 → Archer2.
  - `ReleaseUnknownClientReturnsFalse`.
  - `NormalizeStripsSpacesAndUppercases`: `" k7q x2p\n"` → `"K7QX2P"`.
  - `IsValidRejectsEmptyShortAndSymbols`: `""`, `"ABC"`, `"K7Q-X2"` → false; `"K7QX2P"` → true.
  - `ProfilesDifferPerProcessAndStayLegal`: `ForProcess(100) != ForProcess(200)`; `ForProcess(int.MaxValue, "editor/clone #2")` matches `^[A-Za-z0-9_-]{1,30}$`.
- [ ] **Step 2: Add the packages and asmdefs; implement the four files** to the interfaces above.
- [ ] **Step 3: Run** Test Runner → EditMode → `BowMan.Networking.EditorTests` on the Windows Editor. Expected: 8 passed. Also confirm the package import produced no compile errors and that `BowMan.VR.EditorTests` still passes.
- [ ] **Step 4: Commit** `Add networking packages and pure 1v1 connection rules`.

### Task 2: Relay connection controller and status

**Files:**
- Create: `Assets/_Game/Scripts/Networking/ConnectionStatus.cs`, `RelayConnector.cs`, `PvPConnectionController.cs`
- Test: `Assets/_Game/Tests/Networking/ConnectionStatusTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  - `enum ConnectionPhase { Offline, SigningIn, CreatingMatch, WaitingForOpponent, Joining, Connected, Failed, Disconnected }`
  - `readonly struct ConnectionStatus(ConnectionPhase phase, ArcherRole role, string joinCode, string error)` with `string Describe()` and `bool CanStart` (true for Offline, Failed, Disconnected).
    `Describe()` produces this exact copy:
    - Offline → `"Offline — Host or enter a code to Join"`
    - SigningIn → `"Signing in…"`
    - CreatingMatch → `"Creating match…"`
    - WaitingForOpponent → `"Archer 1 — share code {code}, waiting for opponent"`
    - Joining → `"Joining {code}…"`
    - Connected → `"Connected as Archer {1|2}"`, plus `" — code {code}"` when the code is non-empty
    - Failed → `"Connection failed: {error}"`
    - Disconnected → `"Disconnected{: error when present}"`
  - `class RelayConnector` (plain C#): `Task EnsureSignedInAsync(string profile)` (initializes `UnityServices` with `InitializationOptions.SetProfile(profile)` only once, then `SignInAnonymouslyAsync` if not signed in); `Task<string> HostAsync(UnityTransport transport)` (`CreateAllocationAsync(1)`, `GetJoinCodeAsync`, `transport.SetRelayServerData(allocation.ToRelayServerData("dtls"))`, returns the code); `Task JoinAsync(UnityTransport transport, string code)`.
  - `class PvPConnectionController : MonoBehaviour`. Serialized fields: `NetworkManager networkManager`, `UnityTransport transport`. Public: `ConnectionStatus Status { get; }`, `event Action<ConnectionStatus> StatusChanged`, `ArcherSeatAllocator Seats { get; }`, `Task HostAsync()`, `Task JoinAsync(string rawCode)`, `void Leave()`.
- Behavior to pin:
  - Before `StartHost`, set `NetworkConfig.ConnectionApproval = true` and an approval callback: `TryAssign` → `Approved`, `CreatePlayerObject = true`, otherwise `Approved = false`, `Reason = ArcherSeatAllocator.FullReason`.
  - The server releases the seat on `OnConnectionEvent` `ClientDisconnected`.
  - The client maps its own disconnect to `Disconnected` with `NetworkManager.DisconnectReason`, and `OnTransportFailure` to `Failed`.
  - Any exception from auth, Relay or Start* → `Failed` with `ex.Message`; call `networkManager.Shutdown()` and `Seats.Clear()` so Host/Join work again.
  - An invalid code → `Failed` with `"Enter the 6-character join code"`, before any network call.
  - Auth profile is `AuthProfile.ForProcess(Process.GetCurrentProcess().Id, Application.isEditor ? "editor" : null)`.

- [ ] **Step 1: Write failing tests** in `ConnectionStatusTests`: one assertion per phase on `Describe()`, using code `"K7QX2P"` and error `"timeout"`, with exact strings as above. Add `CanStartOnlyWhenIdle`: true for Offline, Failed and Disconnected; false for the other five.
- [ ] **Step 2: Implement** `ConnectionStatus`, then `RelayConnector` and `PvPConnectionController`.
- [ ] **Step 3: Run** EditMode `BowMan.Networking.EditorTests`. Expected: all pass. Check the Console for no compile errors.
- [ ] **Step 4: Commit** `Connect two clients through a Unity Relay join code`.

### Task 3: Networked presence and local rig placement

**Files:**
- Create: `Assets/_Game/Scripts/Networking/ArcherSpawnPoints.cs`, `LocalRigBinder.cs`, `PlayerPresence.cs`, `ConnectionPanel.cs`

**Interfaces:**
- Consumes: `ArcherRole`, `PvPConnectionController.Seats`/`Status`/`StatusChanged`/`HostAsync`/`JoinAsync`/`Leave`.
- Produces:
  - `ArcherSpawnPoints : MonoBehaviour`. Fields `Transform archer1`, `Transform archer2`; `Transform For(ArcherRole role)` (null for None).
  - `LocalRigBinder : MonoBehaviour`. Fields `Transform rigRoot`, `Transform cameraOffset`, `Transform head`, `Transform leftHand`, `Transform rightHand`, `ArcherSpawnPoints spawns`; `void PlaceAt(ArcherRole role)` copies the spawn's position and Y rotation to `rigRoot`.
    - In `Awake`: when the command line contains `-desktop`, stop and deinitialize the XR loader through `XRGeneralSettings.Instance.Manager`.
    - When no XR device is active (`XRSettings.isDeviceActive == false`), set `cameraOffset.localPosition.y = 1.6f`.
  - `PlayerPresence : NetworkBehaviour`.
    - `NetworkVariable<ArcherRole> Role` (server write). The server sets it from `Seats.RoleOf(OwnerClientId)` in `OnNetworkSpawn`.
    - Three owner-write `NetworkVariable<Pose>`-equivalent pairs for head, left and right (position `Vector3` + rotation `Quaternion`), written by the owner each `Update` from `LocalRigBinder`.
    - The owner calls `PlaceAt(Role.Value)` on spawn and on `Role.OnValueChanged` when the value is not None.
    - Non-owners show child markers `Head`, `Left`, `Right` (assigned in the prefab) at the replicated poses; the owner hides its own markers.
  - `ConnectionPanel : MonoBehaviour`.
    - IMGUI desktop panel (like `ArenaShotTest`) at `Rect(16, 16, 360, 140)`: title `"PvP connection test"`, `Status.Describe()`, a Host button, a code text field, a Join button and a Leave button.
    - Host/Join are enabled only when `CanStart`; Leave is enabled otherwise.
    - It also writes `Describe()` to an optional in-headset `TextMesh headsetLabel` on every `StatusChanged`.

- [ ] **Step 1: Implement** the four components.
- [ ] **Step 2: Run** the Windows Editor compile (no Console errors) and EditMode tests again (still green).
- [ ] **Step 3: Commit** `Place each archer at its spawn and show the opponent's head and hands`.

### Task 4: Generated test scene, player prefab and Windows player

**Files:**
- Create: `Assets/_Game/Scripts/Networking/Editor/BowMan.Networking.Editor.asmdef` (Editor-only; references `BowMan.Networking`, `Unity.Netcode.Runtime`, `Unity.Networking.Transport`)
- Create: `Assets/_Game/Scripts/Networking/Editor/PvPConnectionSceneBuilder.cs`
- Generated by running the menu (committed afterwards): `Assets/_Game/Scenes/PvPConnectionTest.unity`, `Assets/_Game/Prefabs/Networking/NetworkArcherPresence.prefab`

**Interfaces:**
- Consumes: every Task 2–3 component.
- Produces:
  - Menu `Bow Man VR/PvP/Build Connection Test Scene`. It regenerates both assets idempotently, overwriting earlier output, and runs in a fresh scene from `NewSceneSetup.EmptyScene` with:
    - a directional light
    - a 12×1×28 floor cube with its top at Y=0
    - `Archer1Spawn` at `(0,0,-10)`, rotation 0
    - `Archer2Spawn` at `(0,0,10)`, Y rotation 180
    - an `XRPlayer` prefab instance with a `LocalRigBinder` wired to its `Camera Offset`, `Main Camera`, `Left Controller` and `Right Controller`
    - a headset `TextMesh` 1.5 m in front of the camera
    - `NetworkManager` + `UnityTransport` + `PvPConnectionController` + `ConnectionPanel`, with `PlayerPrefab` set to the presence prefab
  - The presence prefab is a root with `NetworkObject` and `PlayerPresence`, plus collider-free sphere markers: head 0.25 m, hands 0.08 m, in a red unlit material.
  - Menu `Bow Man VR/PvP/Build Windows Connection Player`. It builds only that scene to `Builds/PCVRPvP/BowManVR-PvP.exe` (`StandaloneWindows64`) without changing `EditorBuildSettings`.

- [ ] **Step 1: Implement** the builder.
- [ ] **Step 2: Run** the first menu item on Windows. Expected: the scene and prefab exist, and entering Play shows the panel with `"Offline — Host or enter a code to Join"`.
- [ ] **Step 3: Smoke test on one PC.**
  - In the Editor, Host: a code appears and the status reads `"Archer 1 — share code …"`.
  - The Windows player started with `-desktop` joins with that code: it reads `"Connected as Archer 2"`, and each instance sees the other's markers at the opposite spawn.
  - A third instance gets `"Disconnected: Match is full"` (or a Relay join refusal).
- [ ] **Step 4: Commit** the builder plus the generated scene, prefab, `packages-lock.json` and `.meta` files: `Generate the PvP connection test scene and player`.

### Task 5: Cross-network acceptance and documentation

**Files:**
- Modify: `AGENTS.md` (new section "Testing PvP connectivity"; update "Game modes")
- Modify: `Docs/Decisions.md` (new D008 — Unity Relay + NGO for online PvP)

- [ ] **Step 1: Write the docs.**
  - The AGENTS section covers:
    - enabling Relay for the linked Unity Cloud project
    - regenerating the scene
    - building the player
    - Host/Join steps
    - the `-desktop` flag
    - the second PC on a different network (e.g. a phone hotspot)
    - the known limits: no shot sync (#12), no rematch (#13)
  - D008 records the choice, the reason (NAT traversal without port forwarding, existing Unity Cloud link) and the consequence (UGS sign-in and a Relay quota dependency).
- [ ] **Step 2: Acceptance.** Two Windows PCs on different networks connect and show distinct roles, one of them in VR on the Quest 2. Record the result on issue #11.
- [ ] **Step 3: Commit** `Document PvP connectivity setup and the Relay decision`. Then open a PR to `main` linked to #11 and move the card to Review / Test.
