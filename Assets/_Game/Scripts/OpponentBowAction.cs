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
        private Transform[] fingers;
        private Quaternion[] closedFingers;
        private BoxCollider head;
        private CapsuleCollider leftPalm, rightPalm;
        private Transform[] body;
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
            var regions = GetComponentsInChildren<BodyHitRegion>();
            head = regions.First(r => r.Region == BodyRegion.Head).GetComponent<BoxCollider>();
            leftPalm = regions.First(r => r.Region == BodyRegion.LeftHand).GetComponent<CapsuleCollider>();
            rightPalm = regions.First(r => r.Region == BodyRegion.RightHand).GetComponent<CapsuleCollider>();
            CacheGrip();
            body = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => rig.GetBoneTransform((HumanBodyBones)i))
                .Where(t => t != null).ToArray();
            baseRotations = new Quaternion[body.Length];
            basePositions = new Vector3[body.Length];
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

        // Maps the wrapped-finger pose to this avatar once and returns the rig to its current pose.
        private void CacheGrip()
        {
            var skeleton = rig.GetComponentsInChildren<Transform>();
            var positions = skeleton.Select(b => b.localPosition).ToArray();
            var rotations = skeleton.Select(b => b.localRotation).ToArray();
            int first = (int)HumanBodyBones.LeftThumbProximal;
            int count = (int)HumanBodyBones.RightLittleDistal - first + 1;
            fingers = Enumerable.Range(first, count).Select(i => rig.GetBoneTransform((HumanBodyBones)i)).ToArray();
            using var handler = new HumanPoseHandler(rig.avatar, rig.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            for (int i = 0; i < pose.muscles.Length; i++)
            {
                string muscle = HumanTrait.MuscleName[i];
                if (muscle.Contains("Stretched")) pose.muscles[i] = muscle.Contains("Thumb") ? -0.32f : -0.78f;
            }
            handler.SetHumanPose(ref pose);
            closedFingers = fingers.Select(b => b.localRotation).ToArray();
            for (int i = 0; i < skeleton.Length; i++)
                skeleton[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
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
            for (var i = 0; i < body.Length; i++)
                body[i].SetLocalPositionAndRotation(basePositions[i], baseRotations[i]);
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
            for (var i = 0; i < body.Length; i++)
            {
                basePositions[i] = body[i].localPosition;
                baseRotations[i] = body[i].localRotation;
            }
            overlaid = true;
            if (weight > 0f)
            {
                reaction.BowAction(left).SampleAnimation(gameObject, sampleTime);
                for (var i = 0; i < body.Length; i++)
                {
                    body[i].localRotation = Quaternion.Slerp(baseRotations[i], body[i].localRotation, weight);
                    body[i].localPosition = Vector3.Lerp(basePositions[i], body[i].localPosition, weight);
                }
            }
            int half = fingers.Length / 2;
            for (int i = left ? 0 : half, end = i + half; i < end; i++)
                fingers[i].localRotation = closedFingers[i];
            var index = rig.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = rig.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var wrist = rig.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var gripAxis = (index.position - little.position).normalized;

            var restRotation = Quaternion.FromToRotation(transform.up, gripAxis) * transform.rotation;
            var rotation = Quaternion.Slerp(restRotation, aim, weight);
            wrist.rotation = Quaternion.FromToRotation(gripAxis, rotation * Vector3.up) * wrist.rotation;
            if (trackedGrip.HasValue && weight > 0f)
                FitHand(left, trackedGrip.Value, false, weight);
            equipment.SetPositionAndRotation(Contact(left, false), rotation);
            var offset = trackedGrip.HasValue ? pullOffset : -equipment.forward * (draw * maxDraw);
            var middle = equipment.TransformPoint(rest) + (nocked ? offset : Vector3.zero);
            if (nocked) FitHand(!left, middle, true, weight);
            if (weight > 0f) ClearDrawingHand();
            if (nocked) middle = Contact(!left, true);
            Bow.SetString(bowString, equipment.TransformPoint(top), middle, equipment.TransformPoint(bottom));
            var direction = nocked && draw > 0.01f ? (equipment.TransformPoint(rest) - middle).normalized : equipment.forward;
            arrow.SetPositionAndRotation(middle + direction * arrowLength, Quaternion.LookRotation(direction, equipment.up));
            arrow.gameObject.SetActive(nocked);
        }

        // Places the palm center at the grip or the pinch between thumb and index finger at the string.
        private Vector3 Contact(bool onLeft, bool pinch)
        {
            var a = pinch ? (onLeft ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal) :
                (onLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var b = pinch ? (onLeft ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal) :
                (onLeft ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
            return Vector3.Lerp(rig.GetBoneTransform(a).position, rig.GetBoneTransform(b).position, 0.5f);
        }

        // Guides the palm around the drawing-side cheek throughout the reach, pull and follow-through.
        private void ClearDrawingHand()
        {
            var palm = left ? rightPalm : leftPalm;
            var center = head.transform.TransformPoint(head.center);
            var delta = palm.transform.TransformPoint(palm.center) - center;
            var side = transform.right * (left ? 1f : -1f);
            float forward = Mathf.Abs(Vector3.Dot(delta, transform.forward));
            float height = Mathf.Abs(Vector3.Dot(delta, transform.up));
            float nearFace = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.14f, 0.27f, forward))
                * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 0.23f, height));
            float clearance = Mathf.Max(0f, 0.21f - Vector3.Dot(delta, side)) * nearFace;
            if (clearance > 0f) FitHand(!left, Contact(!left, true) + side * clearance, true, 1f);
            for (int pass = 0; pass < 3 && Physics.ComputePenetration(palm, palm.transform.position, palm.transform.rotation,
                head, head.transform.position, head.transform.rotation, out var away, out float depth); pass++)
                FitHand(!left, Contact(!left, true) + away * (depth + 0.008f), true, 1f);
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
