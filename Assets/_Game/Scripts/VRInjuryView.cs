using System;
using UnityEngine;

namespace BowMan
{
    [DefaultExecutionOrder(1000)]
    public sealed class VRInjuryView : MonoBehaviour
    {
        [SerializeField] private Transform trackingOrigin;
        private ArcherReactionAnimation reaction;
        private ArcherHealth health;
        private Transform actor;
        private Transform eyes;
        private Vector3 restEyePosition;
        private Quaternion restEyeRotation;
        private Vector3 originPosition;
        private Quaternion originRotation;
        private bool bound;

        // Stores the standing eye reference while leaving tracked head and controller poses untouched.
        public void Bind(Transform character, Transform eyeAnchor, ArcherHealth duelActor = null)
        {
            if (trackingOrigin == null || character == null || eyeAnchor == null || !eyeAnchor.IsChildOf(character))
                throw new ArgumentException("Injury view requires a tracking origin and the character's eye anchor.");
            ResetView();
            health = duelActor;
            reaction = health != null ? health.GetComponent<ArcherReactionAnimation>() : null;
            actor = character;
            eyes = eyeAnchor;
            restEyePosition = actor.InverseTransformPoint(eyes.position);
            restEyeRotation = Quaternion.Inverse(actor.rotation) * eyes.rotation;
            originPosition = trackingOrigin.position;
            originRotation = trackingOrigin.rotation;
            bound = true;
        }

        // Restores the floor origin before replacing a character or starting another match.
        public void ResetView()
        {
            if (bound && trackingOrigin != null) trackingOrigin.SetPositionAndRotation(originPosition, originRotation);
            bound = false;
            actor = null;
            eyes = null;
        }

        // Applies only animated eye displacement/rotation, avoiding a second standing-height offset.
        private void LateUpdate()
        {
            if (eyes == null || actor == null) return;
            if (reaction != null && !reaction.IsInjured && !health.IsDefeated)
            {
                trackingOrigin.SetPositionAndRotation(originPosition, originRotation);
                return;
            }
            var neutralEyes = actor.TransformPoint(restEyePosition);
            var rotation = eyes.rotation * Quaternion.Inverse(actor.rotation * restEyeRotation);
            trackingOrigin.SetPositionAndRotation(eyes.position + rotation * (originPosition - neutralEyes),
                rotation * originRotation);
        }

        // Restores ordinary tracked movement when injury following is disabled.
        private void OnDisable() => ResetView();
    }
}
