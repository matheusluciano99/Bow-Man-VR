using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public enum ImpactSound : byte { Ground, Stone, Wood, Body }

    public sealed class DuelAudio : MonoBehaviour
    {
        private const float StretchThreshold = 0.60f;
        private const string VolumeKey = "BowMan.DuelVolume";
        private Bow bow;
        private OpponentBowAction opponent;
        private int localSeat;
        private ArcherModel firstVoice, secondVoice;
        private readonly Transform[] voiceHeads = new Transform[2];
        private DuelTurnFlow flow;
        private AudioSource creak, passing;
        private AudioClip nock, release;
        private readonly AudioClip[] impacts = new AudioClip[4];
        private InputAction volume;
        private int lastShot = -1;
        private float previousDraw = -1f;
        private ArenaSetting setting;
        public string VolumeHint => $"X: volume {AudioListener.volume:P0}";

        // Attaches the sounds and volume control to a single local or online round.
        public void Bind(DuelTurnFlow turns, Bow equipped, OpponentBowAction other, ArcherHealth local,
            ArcherHealth remote, ArenaSetting arena, int localIndex, ArcherModel first, ArcherModel second)
        {
            flow = turns;
            opponent = other;
            localSeat = localIndex;
            BindBow(equipped, arena, first, second);
            voiceHeads[localIndex] = Breathing(local, localIndex == 0 ? first : second, 0f);
            voiceHeads[1 - localIndex] = Breathing(remote, localIndex == 0 ? second : first, 0.43f);
            turns.ReactionRequested += VoiceOnImpact;
            turns.ShotAccepted += Accepted;
        }

        // Loads the common bow, impact and range sounds for the active game mode.
        public void BindBow(Bow equipped, ArenaSetting arena, ArcherModel first, ArcherModel second)
        {
            firstVoice = first;
            secondVoice = second;
            bow = equipped;
            setting = arena;
            nock = Clip("Nock");
            release = Clip("Release");
            foreach (ImpactSound sound in System.Enum.GetValues(typeof(ImpactSound)))
                impacts[(int)sound] = Clip(sound.ToString());
            var ambience = Emitter(transform, Clip(arena.ToString()), 0.14f, true);
            ambience.spatialBlend = 0f;
            ambience.Play();
            creak = Emitter(transform, Clip("Draw"), 0f, true);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.75f));
            volume = new InputAction("Duel volume", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            if (DesktopBowTest.DesktopAvailable) volume.AddBinding("<Keyboard>/v");
            volume.AddBinding("<WebXRController>{LeftHand}/buttonA");
            volume.Enable();
        }

        // Loads the imported recording shared by local and network presentation.
        private static AudioClip Clip(string name) => Resources.Load<AudioClip>("DuelAudio/" + name);

        // Gives each emitter independent placement, falloff and lifetime inside its round.
        private static AudioSource Emitter(Transform parent, AudioClip clip, float gain, bool loop)
        {
            var go = new GameObject(clip.name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 45f;
            source.volume = gain;
            source.loop = loop;
            return source;
        }

        // Places quiet breathing at the animated head and ends it when the archer dies.
        private static Transform Breathing(ArcherHealth archer, ArcherModel model, float offset)
        {
            var head = archer.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var voice = Emitter(head, Clip(model == ArcherModel.Erika ? "Erika/Breath" : "Breath"), 0.10f, true);
            voice.pitch = model == ArcherModel.English ? 0.8f : 1f;
            voice.time = offset * voice.clip.length;
            voice.Play();
            archer.Defeated += voice.Stop;
            return head;
        }

        // Plays the selected performer's reaction when the turn authority confirms an injury.
        private void VoiceOnImpact(int target, BodyImpact hit, bool lethal, int shot)
        {
            var model = target == 0 ? firstVoice : secondVoice;
            OneShot(Clip(model + (lethal ? "/Defeat" : "/Hurt")), voiceHeads[target].position, 0.6f);
        }

        // Captures a physical arrow after the rules accept it and resolves its first solid contact once.
        private void Accepted(ArrowFlight arrow)
        {
            Flight(flow.ShotId, arrow);
            arrow.Hit += hit => Impact(hit.point, Surface(hit.collider));
        }

        // Plays one release at origin and moves the pass sound along the actual or replicated flight.
        public void Flight(int shot, ArrowFlight arrow)
        {
            if (shot <= lastShot) return;
            lastShot = shot;
            var grip = flow == null ? arrow.transform.position :
                flow.ActiveArcher == localSeat ? bow.transform.position : opponent.GripPosition;
            int shooter = flow != null ? flow.ActiveArcher : shot % 2;
            var model = shooter == 0 ? firstVoice : secondVoice;
            OneShot(Clip(model + "/Effort"), voiceHeads[shooter] != null ? voiceHeads[shooter].position : grip, 0.26f);
            OneShot(release, grip, 0.95f);
            passing = Emitter(arrow.transform, Clip("Flight"), 0.36f, true);
            passing.Play();
        }

        // Uses the collision surfaces authored by the arena builder and the current arena's base floor.
        public ImpactSound Surface(Collider collider)
        {
            if (collider.TryGetComponent<BodyHitRegion>(out _)) return ImpactSound.Body;
            return collider.name switch
            {
                "medieval_wood collision" => ImpactSound.Wood,
                "castle_brick_01 collision" or "plastered_wall_02 collision" or "cobblestone_floor_02 collision" => ImpactSound.Stone,
                "brown_mud_03 collision" or "forest_ground_04 collision" => ImpactSound.Ground,
                _ => setting == ArenaSetting.CastleCourtyard ? ImpactSound.Stone : ImpactSound.Ground
            };
        }

        // Ends the moving sound and leaves the impact at its world-space point through the decay.
        public void Impact(Vector3 point, ImpactSound kind)
        {
            if (passing == null) return;
            passing.Stop();
            Destroy(passing.gameObject);
            passing = null;
            OneShot(impacts[(int)kind], point, 0.95f);
        }

        // Plays a spatial transient independently of the projectile's remaining lifetime.
        private void OneShot(AudioClip clip, Vector3 point, float gain)
        {
            var source = Emitter(transform, clip, gain, false);
            source.transform.position = point;
            source.Play();
            Destroy(source.gameObject, clip.length + 0.1f);
        }

        // Follows the active draw and lets the local player cycle the saved game volume including mute.
        private void LateUpdate()
        {
            if (volume == null) return;
            if (volume.WasPressedThisFrame())
            {
                AudioListener.volume = (Mathf.RoundToInt(AudioListener.volume * 4f) + 1) % 5 * 0.25f;
                PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
                PlayerPrefs.Save();
            }
            var remoteDraw = !bow.IsDrawing && opponent != null && opponent.IsNocked;
            var drawing = bow.IsDrawing || remoteDraw;
            var draw = remoteDraw ? opponent.DrawStrength : bow.DrawStrength;
            var position = remoteDraw ? opponent.StringPoint : bow.StringPosition;
            if (drawing && previousDraw < 0f) OneShot(nock, position, 0.52f);
            creak.transform.position = position;
            UpdateStretch(drawing ? draw : 0f);
            previousDraw = drawing ? draw : -1f;
        }

        // Starts the recording above the draw threshold and keeps a softer creak under steady tension.
        private void UpdateStretch(float draw)
        {
            if (draw <= StretchThreshold)
            {
                if (creak.isPlaying) creak.Stop();
                creak.volume = 0f;
                return;
            }
            var tension = Mathf.InverseLerp(StretchThreshold, 1f, draw);
            var speed = previousDraw < 0f ? 0f : Mathf.Abs(draw - previousDraw) / Mathf.Max(Time.deltaTime, 0.001f);
            var gain = Mathf.Lerp(0.10f, 0.32f, tension) + 0.18f * Mathf.Clamp01(speed);
            creak.pitch = Mathf.Lerp(0.90f, 1.18f, tension);
            creak.volume = Mathf.MoveTowards(creak.volume, gain, 4f * Time.deltaTime);
            if (!creak.isPlaying) creak.Play();
        }

        // Disconnects from the reused local turn authority as soon as its old round is disabled.
        private void OnDisable()
        {
            if (flow != null)
            {
                flow.ShotAccepted -= Accepted;
                flow.ReactionRequested -= VoiceOnImpact;
            }
            volume?.Dispose();
            volume = null;
        }
    }
}
