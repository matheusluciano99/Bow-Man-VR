using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
                if (details == null) details = ReadNotices();
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

        // Uses the copy distributed with the PC player so every source and license is available offline.
        private static string[] ReadNotices()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "BowMan-Credits.txt");
            var result = new List<string>();
            var lines = new List<string>();
            foreach (var paragraph in File.ReadAllText(path).Replace("\r", "").Trim().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
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
            body.characterSize = showingNotices ? 0.019f : 0.022f;
            noticesLabel.text = showingNotices ? "Credits" : "Notices";
            counter.text = $"{page + 1} / {set.Length}";
            previous.gameObject.SetActive(page > 0);
            next.gameObject.SetActive(page + 1 < set.Length);
        }
    }
}
