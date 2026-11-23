using System.Linq;
using BowMan;
using UnityEngine;

// Fits a captured weight transfer to the character's stationary boot contacts.
internal sealed class PlantedArcherStance
{
    private readonly Animator rig;
    private readonly Transform root;
    private readonly HumanBodyBones[] legs = {
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
    private readonly Vector3[] contacts;
    private readonly Quaternion[] rotations;
    public Transform[] Bones { get; }

    // Records each ankle's position and boot orientation in the authored neutral stance.
    public PlantedArcherStance(Animator rig, Transform root)
    {
        this.rig = rig;
        this.root = root;
        Bones = legs.Select(rig.GetBoneTransform).ToArray();
        contacts = new[] { Bones[2].position, Bones[5].position };
        rotations = new[] { Bones[2].rotation, Bones[5].rotation };
    }

    // Bends each leg under the moving pelvis and restores its original boot contact.
    public void Plant()
    {
        for (int side = 0; side < 2; side++)
        {
            int leg = side * 3;
            ArcherLimbIK.Solve(rig, legs[leg], legs[leg + 1], legs[leg + 2], contacts[side], root.forward);
            Bones[leg + 2].rotation = rotations[side];
        }
    }
}
