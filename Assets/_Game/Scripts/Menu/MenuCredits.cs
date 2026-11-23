using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace BowMan.Menu
{
    public sealed class MenuCredits : MonoBehaviour
    {
        [SerializeField] private GameObject choices, pages;
        [SerializeField] private ModeOption open, previous, next, back, notices;
        [SerializeField] private TextMesh heading, body, counter, noticesLabel;
        [SerializeField] private TextAsset credits;
        private string[] summaries, details;
        private int page;
        private bool showingNotices;
        public bool IsOpen => pages != null && pages.activeSelf;

        // Connects the scene's pointer panels and page labels to the published credits.
        public void Configure(GameObject menuChoices, GameObject creditPages, ModeOption entry, ModeOption prev,
            ModeOption forward, ModeOption exit, ModeOption sources, TextMesh title, TextMesh text,
            TextMesh pageNumber, TextAsset content)
        {
            choices = menuChoices;
            pages = creditPages;
            open = entry;
            previous = prev;
            next = forward;
            back = exit;
            notices = sources;
            heading = title;
            body = text;
            counter = pageNumber;
            noticesLabel = sources.GetComponentInChildren<TextMesh>();
            credits = content;
        }

        // Enters the credits or restores the mode buttons without replacing the current player rig.
        public void Show(bool visible)
        {
            choices.SetActive(!visible);
            pages.SetActive(visible);
            showingNotices = false;
            page = 0;
            if (!visible) return;
            summaries ??= credits.text.Replace("\r", "").Trim().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            Display();
        }

        // Routes pointer selections within the visible page and leaves ordinary mode choices to the menu.
        public bool Select(ModeOption option)
        {
            if (!IsOpen)
            {
                if (option != open) return false;
                Show(true);
            }
            else if (option == back) Show(false);
            else if (option == previous) Turn(-1);
            else if (option == next) Turn(1);
            else if (option == notices)
            {
                if (details == null)
                {
                    if (Application.platform == RuntimePlatform.WebGLPlayer)
                    {
                        details = new[] { "Loading source notices…" };
                        StartCoroutine(FetchNotices());
                    }
                    else details = ReadNotices();
                }
                showingNotices = !showingNotices;
                page = 0;
                Display();
            }
            return true;
        }

        // Moves within the current set of pages and keeps the player at either end of the list.
        public void Turn(int step)
        {
            page = Mathf.Clamp(page + step, 0, (showingNotices ? details : summaries).Length - 1);
            Display();
        }

        // Loads the notices from the itch.io game's relative StreamingAssets URL.
        private IEnumerator FetchNotices()
        {
            using var request = UnityWebRequest.Get(Application.streamingAssetsPath + "/BowMan-Credits.txt");
            yield return request.SendWebRequest();
            details = request.result == UnityWebRequest.Result.Success
                ? Paginate(request.downloadHandler.text) : new[] { "Could not load source notices." };
            if (IsOpen && showingNotices) Display();
        }

        // Uses the copy distributed with the PC player so every source and license is available offline.
        private static string[] ReadNotices()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "BowMan-Credits.txt");
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Could not open the bundled credits: {error.Message}");
                return new[] { "Could not open BowMan-Credits.txt.\nSelect Credits to return to the contributor pages." };
            }
            return Paginate(text);
        }

        // Wraps the source text into pages sized for the headset panel.
        private static string[] Paginate(string text)
        {
            var result = new List<string>();
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return new[] { "The source notices file is empty." };
            foreach (var paragraph in text.Replace("\r", "").Trim().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var line in paragraph.Split('\n'))
                {
                    var remaining = line;
                    while (remaining.Length > 54)
                    {
                        var space = remaining.LastIndexOf(' ', 54);
                        var count = space > 0 ? space : 54;
                        lines.Add(remaining.Substring(0, count));
                        remaining = remaining.Substring(count).TrimStart();
                    }
                    lines.Add(remaining);
                }
                lines.Add("");
            }
            for (int i = 0; i < lines.Count;)
            {
                while (i < lines.Count && lines[i].Length == 0) i++;
                if (i == lines.Count) break;
                var size = Math.Min(7, lines.Count - i);
                result.Add(string.Join("\n", lines.GetRange(i, size)).TrimEnd());
                i += size;
            }
            return result.ToArray();
        }

        // Updates the title, body, page count and available actions as one visible page.
        private void Display()
        {
            var set = showingNotices ? details : summaries;
            var content = set[page];
            var split = content.IndexOf('\n');
            heading.text = showingNotices ? "Source and license notices" : content.Substring(0, split);
            body.text = showingNotices ? content : content.Substring(split + 1);
            heading.characterSize = 0.029f;
            body.characterSize = showingNotices ? 0.016f : 0.019f;
            noticesLabel.text = showingNotices ? "Credits" : "Notices";
            counter.text = $"{page + 1} / {set.Length}";
            previous.gameObject.SetActive(page > 0);
            next.gameObject.SetActive(page + 1 < set.Length);
        }
    }
}
