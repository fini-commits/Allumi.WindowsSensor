using System;
using System.Collections.Generic;
using System.Linq;

namespace Allumi.WindowsSensor.Categorization
{
    /// <summary>
    /// Local fallback categorization in case backend AI is unavailable
    /// </summary>
    public static class CategoryDetector
    {
        private static readonly Dictionary<string, string[]> CategoryKeywords = new()
        {
            ["development"] = new[] { 
                "visual studio", "vscode", "code", "github", "gitlab", "bitbucket",
                "terminal", "cmd", "powershell", "bash", "wsl",
                "sublime", "notepad++", "atom", "vim", "emacs",
                "rider", "pycharm", "intellij", "eclipse", "netbeans",
                "android studio", "xcode", "visual studio code",
                "git", "docker", "kubernetes", "postman", "insomnia"
            },
            ["communication"] = new[] { 
                "slack", "teams", "discord", "zoom", "skype", "meet",
                "telegram", "whatsapp", "signal", "messenger", "wechat",
                "outlook", "thunderbird", "mail", "gmail", "protonmail"
            },
            ["productivity"] = new[] { 
                "word", "excel", "powerpoint", "outlook", "onenote",
                "notion", "evernote", "obsidian", "roam",
                "trello", "asana", "jira", "monday", "clickup",
                "todoist", "any.do", "calendar", "planner"
            },
            ["browsing"] = new[] { 
                "chrome", "firefox", "edge", "safari", "opera", "brave",
                "browser", "chromium", "vivaldi", "arc"
            },
            ["entertainment"] = new[] { 
                "spotify", "apple music", "youtube music", "soundcloud",
                "netflix", "hulu", "disney+", "prime video", "hbo",
                "youtube", "twitch", "tiktok",
                "steam", "epic games", "origin", "uplay", "gog",
                "gaming", "game", "minecraft", "fortnite", "valorant"
            },
            ["design"] = new[] { 
                "photoshop", "illustrator", "indesign", "premiere", "after effects",
                "figma", "sketch", "invision", "zeplin",
                "canva", "affinity", "gimp", "inkscape",
                "blender", "maya", "3ds max", "cinema 4d"
            },
            ["utilities"] = new[] {
                "explorer", "finder", "file manager", "task manager",
                "settings", "control panel", "system preferences",
                "calculator", "notepad", "textedit"
            },
            ["research"] = new[] {
                "wikipedia", "stack overflow", "stackoverflow", "reddit",
                "medium", "dev.to", "hacker news", "arxiv",
                "scholar", "pubmed", "research", "documentation", "docs"
            }
        };

        /// <summary>
        /// Detects category based on app name and window title using keyword matching
        /// </summary>
        public static string DetectCategory(string appName, string windowTitle)
        {
            var searchText = $"{appName} {windowTitle}".ToLowerInvariant();

            foreach (var (category, keywords) in CategoryKeywords)
            {
                if (keywords.Any(keyword => searchText.Contains(keyword)))
                {
                    return category;
                }
            }

            return "other";
        }

        /// <summary>
        /// Gets confidence level (0-100) for the detected category
        /// Higher confidence = more specific keyword match
        /// </summary>
        public static int GetConfidence(string appName, string windowTitle, string category)
        {
            var searchText = $"{appName} {windowTitle}".ToLowerInvariant();

            if (category == "other")
                return 50; // Low confidence for uncategorized

            if (!CategoryKeywords.ContainsKey(category))
                return 50;

            var keywords = CategoryKeywords[category];
            var matchCount = keywords.Count(keyword => searchText.Contains(keyword));

            // More matches = higher confidence
            if (matchCount >= 3) return 95;
            if (matchCount == 2) return 85;
            if (matchCount == 1) return 75;

            return 50;
        }
    }
}
