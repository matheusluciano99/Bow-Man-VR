using System.Linq;
using UnityEngine;

namespace BowMan
{
    public sealed class OpponentBowAction : MonoBehaviour
    {
        private ArcherReactionAnimation reaction;
        private Animator rig;
        private Transform equipment, arrow;
        private LineRenderer bowString;
        private Vector3 top, rest, bottom;
        private float maxDraw, arrowLength, sampleTime, draw, weight, releasedAt;
        private bool left = true, posing, nocked, releasing, overlaid;
        private Quaternion aim;
        private Vector3? trackedGrip;
        private Vector3 pullOffset;
        private Transform[] upper;
        private Quaternion[] baseRotations;
        private Vector3[] basePositions;

        public Vector3 GripPosition => equipment.position;
        public float DrawStrength => draw;
        public Vector3 ArrowTip => arrow.position;
        public bool IsNocked => arrow != null && arrow.gameObject.activeSelf;
        public Vector3 StringPoint => bowString.GetPosition(1);

        // Builds a rendering-only copy of the equipped bow and arrow from their existing mesh children.
        public void Bind(Bow source, ArrowFlight projectile)
        {
            reaction = GetComponent<ArcherReactionAnimation>();
            rig = GetComponentInChildren<Animator>();
            var spine = rig.GetBoneTransform(HumanBodyBones.Spine);
            upper = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => rig.GetBoneTransform((HumanBodyBones)i))
                .Where(t => t != null && (t == spine || t.IsChildOf(spine))).ToArray();
            baseRotations = new Quaternion[upper.Length];
            basePositions = new Vector3[upper.Length];
            equipment = new GameObject("Opponent bow").transform;
            equipment.SetParent(transform, false);
            source.CopyVisuals(equipment, out bowString, out top, out rest, out bottom);
            arrowLength = source.ArrowLength;
            maxDraw = source.MaximumDrawDistance;
            arrow = new GameObject("Nocked arrow visual").transform;
            arrow.SetParent(equipment, false);
            foreach (Transform child in projectile.transform)
                if (child.TryGetComponent<MeshFilter>(out _)) Instantiate(child.gameObject, arrow, false);
            arrow.gameObject.SetActive(false);
            aim = transform.rotation;
            reaction.UseBowPresentation(this);
        }

        // Drives the captured reach, nock and draw from the AI's actual pre-shot clock.
        public void PrepareAI(float progress, Vector3 direction)
        {
            progress = Mathf.Clamp01(progress);
            posing = true;
            releasing = false;
            left = true;
            trackedGrip = null;
            nocked = progress >= 0.5f;
            draw = Mathf.Clamp01(progress * 2f - 1f);
            sampleTime = nocked ? 1.5f + 0.5f * draw : 3f * progress;
            aim = Quaternion.LookRotation(direction, transform.up);
        }

        // Fits the sampled performance to the network owner's grip, aim and current draw distance.
        public void PreparePlayer(bool bowLeft, float strength, Vector3 grip, Quaternion rotation, Vector3 pull)
        {
            left = bowLeft;
            if (strength < 0f) { Rest(); return; }
            posing = nocked = true;
            releasing = false;
            pullOffset = pull;
            trackedGrip = grip;
            aim = rotation;
            draw = Mathf.Clamp01(strength);
            sampleTime = 1.5f + 0.5f * draw;
        }

        // Removes the preview on the authorized flight and lets the drawing arm follow through.
        public void Release()
        {
            nocked = posing = false;
            arrow.gameObject.SetActive(false);
            Bow.SetString(bowString, equipment.TransformPoint(top), equipment.TransformPoint(rest), equipment.TransformPoint(bottom));
            releasing = true;
            releasedAt = Time.time;
        }

        // Cancels an unfinished nock without emitting a projectile or starting a release.
        public void Rest()
        {
            nocked = posing = false;
        }

        // Restores the bones beneath the overlay before idle or injury owns the next frame.
        internal void Restore()
        {
            if (!overlaid) return;
            for (var i = 0; i < upper.Length; i++)
                upper[i].SetLocalPositionAndRotation(basePositions[i], baseRotations[i]);
            overlaid = false;
        }

        // Hides the equipment for injuries and defeats, including any interrupted draw.
        internal void Interrupt()
        {
            posing = releasing = nocked = false;
            weight = 0f;
            arrow.gameObject.SetActive(false);
            equipment.gameObject.SetActive(false);
        }

        // Samples the action over the breathing pose, then places the prop and the moving string at the hands.
        internal void Present()
        {
            equipment.gameObject.SetActive(true);
            if (releasing)
            {
                var elapsed = Time.time - releasedAt;
                sampleTime = 2f + Mathf.Min(0.75f, elapsed * 2.5f);
                releasing = elapsed < 0.3f;
            }
            weight = Mathf.MoveTowards(weight, posing || releasing ? 1f : 0f, Time.deltaTime * 8f);
            if (weight > 0f)
            {
                for (var i = 0; i < upper.Length; i++)
                {
                    basePositions[i] = upper[i].localPosition;
                    baseRotations[i] = upper[i].localRotation;
                }
                reaction.BowAction(left).SampleAnimation(gameObject, sampleTime);
                for (var i = 0; i < upper.Length; i++)
                {
                    upper[i].localRotation = Quaternion.Slerp(baseRotations[i], upper[i].localRotation, weight);
                    upper[i].localPosition = Vector3.Lerp(basePositions[i], upper[i].localPosition, weight);
                }
                overlaid = true;
            }
            var rotation = Quaternion.Slerp(transform.rotation * Quaternion.Euler(0f, 0f, left ? 22f : -22f), aim, weight);
            if (trackedGrip.HasValue && weight > 0f)
                FitHand(left, trackedGrip.Value, false, weight);
            equipment.SetPositionAndRotation(Contact(left, false), rotation);
            var offset = trackedGrip.HasValue ? pullOffset : -equipment.forward * (draw * maxDraw);
            var middle = equipment.TransformPoint(rest) + (nocked ? offset : Vector3.zero);
            if (nocked) FitHand(!left, middle, true, weight);
            Bow.SetString(bowString, equipment.TransformPoint(top), middle, equipment.TransformPoint(bottom));
            var direction = offset.sqrMagnitude > 0.000001f ? -offset.normalized : equipment.forward;
            arrow.SetPositionAndRotation(middle + direction * arrowLength, Quaternion.LookRotation(direction, equipment.up));
            arrow.gameObject.SetActive(nocked);
        }

        // Places the palm center at the grip or the pinch between thumb and index finger at the string.
        private Vector3 Contact(bool onLeft, bool pinch)
        {
            var a = pinch ? (onLeft ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal) :
                (onLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var b = pinch ? (onLeft ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal) :
                (onLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            return Vector3.Lerp(rig.GetBoneTransform(a).position, rig.GetBoneTransform(b).position, pinch ? 0.5f : 0.7f);
        }

        // Retains the captured wrist orientation and elbow bend while solving hand contact within the arm's reach.
        private void FitHand(bool onLeft, Vector3 target, bool pinch, float amount)
        {
            var a = onLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            var b = onLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            var c = onLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var wrist = rig.GetBoneTransform(c);
            var rotation = wrist.rotation;
            var hint = rig.GetBoneTransform(b).position - rig.GetBoneTransform(a).position;
            ArcherLimbIK.Solve(rig, a, b, c, wrist.position + (target - Contact(onLeft, pinch)) * amount, hint);
            wrist.rotation = rotation;
        }
    }
}
