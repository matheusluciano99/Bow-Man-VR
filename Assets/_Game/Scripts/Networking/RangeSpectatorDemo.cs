#if UNITY_EDITOR
using System.Collections;
using UnityEditor.SceneManagement;
using BowMan;
using BowMan.Networking;
using UnityEditor;
using UnityEngine;

public sealed class RangeSpectatorDemo : MonoBehaviour
{
    private readonly OpponentBowAction[] actions = new OpponentBowAction[2];
    private readonly int[] score = new int[2];
    private readonly string[] names = { "English Archer", "Erika Archer" };
    private Transform line, face;
    private Camera closeup;
    private RenderTexture inset;
    private int shots, active;
    private string status;
    private ArrowFlight arrowPrefab;
    private Vector3? lastImpact;
    private string lastShot;
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.1f, 0.94f);
    private const string Pending = "BowMan.OpenRangeDemo";

    // Opens the target range in the Editor and starts its two-archer demonstration.
    [MenuItem("Bow Man VR/Arenas/Watch target competition")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/TargetRange.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    // Attaches the spectator and focuses the Game view once the requested scene is playing.
    [InitializeOnLoadMethod]
    private static void Listen() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.EraseBool(Pending);
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.Focus();
        new GameObject("Tiro ao alvo spectator").AddComponent<RangeSpectatorDemo>();
    };

    // Frames both competitors and the scoring face in the real woodland scene.
    private IEnumerator Start()
    {
        line = GameObject.Find("Archer1Spawn").transform;
        face = GameObject.Find("Scoring face").transform;
        foreach (var c in Camera.allCameras) c.transform.root.gameObject.SetActive(false);
        var lobby = FindAnyObjectByType<OnlineTargetMatch>();
        if (lobby != null) lobby.gameObject.SetActive(false);
        foreach (var p in FindObjectsByType<ConnectionPanel>(FindObjectsSortMode.None)) p.enabled = false;
        foreach (var label in FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) label.gameObject.SetActive(false);
        var observer = new GameObject("Range overview").AddComponent<Camera>();
        observer.stereoTargetEye = StereoTargetEyeMask.None;
        observer.fieldOfView = 39;
        observer.transform.position = line.TransformPoint(new Vector3(0f, 1.86f, -3.75f));
        observer.transform.LookAt(line.TransformPoint(new Vector3(0f, 1.24f, 4f)));
        if (FindAnyObjectByType<AudioListener>() == null) observer.gameObject.AddComponent<AudioListener>();
        inset = new RenderTexture(320, 320, 24);
        closeup = new GameObject("Target close-up").AddComponent<Camera>();
        closeup.stereoTargetEye = StereoTargetEyeMask.None;
        closeup.targetTexture = inset;
        closeup.orthographic = true;
        closeup.orthographicSize = 1.07f;
        closeup.transform.position = face.position - line.forward * 2.6f;
        closeup.transform.LookAt(face.position);
        var bow = AssetDatabase.LoadAssetAtPath<Bow>("Assets/_Game/Prefabs/Bow.prefab");
        arrowPrefab = AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab");
        var idle = gameObject.AddComponent<DuelTurnFlow>();
        var actors = new ArcherHealth[2];
        for (int i = 0; i < 2; i++)
        {
            string file = i == 0 ? "EnglishArcher" : "ErikaArcher";
            var actor = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/" + file + ".prefab"));
            actor.transform.position = line.TransformPoint(new Vector3(i == 0 ? -0.96f : .96f, 0, 0));
            actor.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(face.position-actor.transform.position, Vector3.up));
            actors[i] = actor.GetComponent<ArcherHealth>() ?? actor.AddComponent<ArcherHealth>();
            actions[i] = actor.AddComponent<OpponentBowAction>();
        }
        idle.Initialize(actors[0], actors[1]);
        for (int i = 0; i < 2; i++)
        {
            actors[i].GetComponent<ArcherReactionAnimation>().Connect(idle, i, actors[i]);
            actions[i].Bind(bow, arrowPrefab);
        }
        while (true)
        {
            score[0] = score[1] = 0;
            lastImpact = null;
            lastShot = "Waiting for the first arrow";
            for (shots = 0; shots < 2 * OnlineTargetMatch.ArrowsPerSeat; shots++)
            {
                active = shots % 2;
                status = names[active] + " to shoot";
                yield return new WaitForSeconds(.60f);
                var destination = face.TransformPoint(new Vector3(.30f * Mathf.Sin(2.7f * shots + 0.5f), .31f * Mathf.Cos(2.18f * shots - 0.8f), 0));
                var action = actions[active];
                for (float t = 0f; t < 1.7f; t += Time.deltaTime)
                {
                    var approximate = action.transform.position + Vector3.up * 1.45f;
                    ArrowBallistics.TryVelocity(approximate, destination, bow.MaximumLaunchSpeed, out var draw);
                    action.PrepareAI(Mathf.Clamp01(t / 1.4f), draw.normalized);
                    yield return null;
                }
                var start = action.ArrowTip;
                if (!ArrowBallistics.TryVelocity(start, destination, bow.MaximumLaunchSpeed, out var speed)) yield break;
                var flight = Instantiate(arrowPrefab, start, Quaternion.identity);
                int points = -1;
                Vector3? impact = null;
                flight.Hit += hit =>
                {
                    if (hit.collider.transform == face) impact = hit.point;
                    points = impact.HasValue ? OnlineTargetMatch.RingScore(face.InverseTransformPoint(hit.point)) : 0;
                };
                flight.Expired += () => points = 0;
                flight.Launch(speed, actions[active].transform);
                action.Release();
                yield return new WaitUntil(() => points >= 0);
                score[active] += points;
                lastImpact = impact;
                lastShot = names[active] + (points > 0 ? $"   +{points}" : "   Miss");
                status = lastShot;
                yield return new WaitForSeconds(1.35f);
            }
            status = score[0] == score[1] ? "Tie" : names[score[0] > score[1] ? 0 : 1] + " wins";
            yield return new WaitForSeconds(5);
        }
    }

    // Shows the score, current shooter and a live close-up of the target rings.
    private void OnGUI()
    {
        if (inset == null) return;
        float unit = Mathf.Clamp(Screen.height / 800f, .65f, 1.8f);
        var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(25 * unit), fontStyle = FontStyle.Bold };
        var text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * unit) };
        title.normal.textColor = text.normal.textColor = Color.white;
        var left = new Rect(14 * unit, 14 * unit, 340 * unit, 124 * unit);
        GUI.color = PanelColor;
        GUI.DrawTexture(left, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(29 * unit, 18 * unit, left.width, 37 * unit), "Tiro ao alvo", title);
        GUI.Label(new Rect(29 * unit, 53 * unit, left.width, 32 * unit), $"English   {score[0]}     •     Erika   {score[1]}", text);
        GUI.Label(new Rect(29 * unit, 86 * unit, left.width, 38 * unit), status, text);
        float size = 225f * unit;
        var panel = new Rect(Screen.width - size - 18f * unit, 16f * unit, size, size + 36f * unit);
        GUI.color = PanelColor;
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        var target = new Rect(panel.x + 3f * unit, panel.y + 3f * unit, size - 6f * unit, size - 6f * unit);
        GUI.DrawTexture(target, inset);
        GUI.Label(new Rect(panel.x + 9f * unit, panel.y + size, size, 34f * unit), lastShot, text);
        if (lastImpact.HasValue)
        {
            var point = closeup.WorldToViewportPoint(lastImpact.Value);
            var pixel = new Vector2(target.x + point.x * target.width, target.yMax - point.y * target.height);
            DrawCross(pixel, unit, 6, Color.black);
            DrawCross(pixel, unit, 2, Color.white);
        }
    }

    // Draws four contrast-backed ticks with an open center at the exact collision pixel.
    private static void DrawCross(Vector2 p, float scale, int width, Color color)
    {
        float gap = 4f * scale, length = 12f * scale, stroke = width * scale;
        GUI.color = color;
        GUI.DrawTexture(new Rect(p.x - gap - length, p.y - stroke / 2f, length, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x + gap, p.y - stroke / 2f, length, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x - stroke / 2f, p.y - gap - length, stroke, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x - stroke / 2f, p.y + gap, stroke, length), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // Releases the offscreen target image when Play mode stops.
    private void OnDestroy()
    {
        if (inset == null) return;
        if (closeup != null) closeup.targetTexture = null;
        inset.Release();
        Destroy(inset);
    }
}
#endif
