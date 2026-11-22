using UnityEngine;

namespace BowMan
{
    public enum ArcherModel : byte { English, Erika }

    public static class ArcherSelection
    {
        private const string Key = "BowMan.Archer";

        public static ArcherModel Preferred
        {
            get => PlayerPrefs.GetInt(Key) == (int)ArcherModel.Erika ? ArcherModel.Erika : ArcherModel.English;
            set { PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.Save(); }
        }

        // Returns the other playable model for the menu switch and solo opponent.
        public static ArcherModel Other(ArcherModel model) =>
            model == ArcherModel.English ? ArcherModel.Erika : ArcherModel.English;

        // Names the archer in the player-facing controls.
        public static string Name(ArcherModel model) =>
            model == ArcherModel.Erika ? "Erika Archer" : "English Archer";
    }
}
