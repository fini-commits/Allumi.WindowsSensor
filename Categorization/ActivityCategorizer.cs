using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Allumi.WindowsSensor.Categorization
{
    /// <summary>
    /// Smart activity categorizer that analyzes app name and window title
    /// to provide intelligent, context-aware categorization.
    /// AI will refine these later, but this provides immediate accurate categories.
    /// </summary>
    public static class ActivityCategorizer
    {
        private static readonly Dictionary<string, CategoryRule> _rules = new()
        {
            // Development & Programming
            ["Visual Studio Code"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Visual Studio"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["IntelliJ IDEA"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["PyCharm"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["WebStorm"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Sublime Text"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Atom"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Eclipse"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["NetBeans"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Xcode"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Android Studio"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Rider"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            ["Code"] = new CategoryRule("Development", new[] { "coding", "programming", "development" }),
            
            // Command Line & System Tools
            ["powershell"] = new CategoryRule("Development", new[] { "terminal", "command-line", "scripting" }),
            ["cmd"] = new CategoryRule("Development", new[] { "terminal", "command-line", "scripting" }),
            ["Windows PowerShell"] = new CategoryRule("Development", new[] { "terminal", "command-line", "scripting" }),
            ["WindowsTerminal"] = new CategoryRule("Development", new[] { "terminal", "command-line", "scripting" }),
            ["bash"] = new CategoryRule("Development", new[] { "terminal", "command-line", "scripting" }),
            
            // Design & Creative
            ["Figma"] = new CategoryRule("Design", new[] { "design", "creative", "ui-ux" }),
            ["Adobe Photoshop"] = new CategoryRule("Design", new[] { "design", "creative", "graphics" }),
            ["Adobe Illustrator"] = new CategoryRule("Design", new[] { "design", "creative", "graphics" }),
            ["Sketch"] = new CategoryRule("Design", new[] { "design", "creative", "ui-ux" }),
            ["Adobe XD"] = new CategoryRule("Design", new[] { "design", "creative", "ui-ux" }),
            ["Canva"] = new CategoryRule("Design", new[] { "design", "creative", "graphics" }),
            ["GIMP"] = new CategoryRule("Design", new[] { "design", "creative", "graphics" }),
            ["Inkscape"] = new CategoryRule("Design", new[] { "design", "creative", "graphics" }),
            ["Blender"] = new CategoryRule("Design", new[] { "3d-modeling", "creative", "animation" }),
            
            // Communication
            ["Slack"] = new CategoryRule("Communication", new[] { "messaging", "team-chat", "collaboration" }),
            ["Microsoft Teams"] = new CategoryRule("Communication", new[] { "messaging", "video-call", "collaboration" }),
            ["Discord"] = new CategoryRule("Communication", new[] { "messaging", "voice-chat", "community" }),
            ["Zoom"] = new CategoryRule("Communication", new[] { "video-call", "meeting", "conferencing" }),
            ["Skype"] = new CategoryRule("Communication", new[] { "video-call", "messaging", "voice-call" }),
            ["WhatsApp"] = new CategoryRule("Communication", new[] { "messaging", "chat", "instant-messaging" }),
            ["Telegram"] = new CategoryRule("Communication", new[] { "messaging", "chat", "instant-messaging" }),
            ["Signal"] = new CategoryRule("Communication", new[] { "messaging", "chat", "secure-messaging" }),
            ["Outlook"] = new CategoryRule("Communication", new[] { "email", "calendar", "scheduling" }),
            ["Gmail"] = new CategoryRule("Communication", new[] { "email", "messaging" }),
            ["Thunderbird"] = new CategoryRule("Communication", new[] { "email", "messaging" }),
            
            // Productivity & Office
            ["Microsoft Word"] = new CategoryRule("Productivity", new[] { "document-editing", "writing", "office" }),
            ["Microsoft Excel"] = new CategoryRule("Productivity", new[] { "spreadsheet", "data-analysis", "office" }),
            ["Microsoft PowerPoint"] = new CategoryRule("Productivity", new[] { "presentation", "slides", "office" }),
            ["Google Docs"] = new CategoryRule("Productivity", new[] { "document-editing", "writing", "collaboration" }),
            ["Google Sheets"] = new CategoryRule("Productivity", new[] { "spreadsheet", "data-analysis", "collaboration" }),
            ["Google Slides"] = new CategoryRule("Productivity", new[] { "presentation", "slides", "collaboration" }),
            ["Notion"] = new CategoryRule("Productivity", new[] { "note-taking", "knowledge-management", "collaboration" }),
            ["Obsidian"] = new CategoryRule("Productivity", new[] { "note-taking", "knowledge-management", "writing" }),
            ["OneNote"] = new CategoryRule("Productivity", new[] { "note-taking", "writing", "organization" }),
            ["Evernote"] = new CategoryRule("Productivity", new[] { "note-taking", "writing", "organization" }),
            ["Trello"] = new CategoryRule("Productivity", new[] { "project-management", "task-tracking", "collaboration" }),
            ["Asana"] = new CategoryRule("Productivity", new[] { "project-management", "task-tracking", "collaboration" }),
            ["Monday"] = new CategoryRule("Productivity", new[] { "project-management", "task-tracking", "collaboration" }),
            ["Jira"] = new CategoryRule("Productivity", new[] { "project-management", "issue-tracking", "development" }),
            
            // Browsers (default to Research, refined by window title)
            ["chrome"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Google Chrome"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["firefox"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Mozilla Firefox"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["msedge"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Microsoft Edge"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Safari"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Opera"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            ["Brave"] = new CategoryRule("Research", new[] { "web-browsing", "browser" }),
            
            // Media & Entertainment
            ["Spotify"] = new CategoryRule("Entertainment", new[] { "music", "audio", "streaming" }),
            ["iTunes"] = new CategoryRule("Entertainment", new[] { "music", "audio", "media" }),
            ["VLC"] = new CategoryRule("Entertainment", new[] { "video", "media-player" }),
            ["Windows Media Player"] = new CategoryRule("Entertainment", new[] { "video", "audio", "media" }),
            ["Netflix"] = new CategoryRule("Entertainment", new[] { "video", "streaming", "movies" }),
            
            // Gaming
            ["Steam"] = new CategoryRule("Entertainment", new[] { "gaming", "games" }),
            ["Epic Games"] = new CategoryRule("Entertainment", new[] { "gaming", "games" }),
            ["Battle.net"] = new CategoryRule("Entertainment", new[] { "gaming", "games" }),
            ["League of Legends"] = new CategoryRule("Entertainment", new[] { "gaming", "games" }),
            ["Valorant"] = new CategoryRule("Entertainment", new[] { "gaming", "games" }),
            
            // System & Utilities
            ["explorer"] = new CategoryRule("Utilities", new[] { "file-management", "system" }),
            ["Finder"] = new CategoryRule("Utilities", new[] { "file-management", "system" }),
            ["Task Manager"] = new CategoryRule("Utilities", new[] { "system-monitoring", "system" }),
            ["Settings"] = new CategoryRule("Utilities", new[] { "system-configuration", "system" }),
            ["Control Panel"] = new CategoryRule("Utilities", new[] { "system-configuration", "system" }),
        };

        // Window title patterns that indicate specific activities
        private static readonly List<WindowPatternRule> _windowPatterns = new()
        {
            // Learning & Education
            new WindowPatternRule(new[] { "tutorial", "course", "learn", "udemy", "coursera", "khan academy", "edx", "pluralsight", "linkedin learning" }, 
                "Learning", new[] { "education", "training", "skill-development" }),
            new WindowPatternRule(new[] { "documentation", "docs", "manual", "guide", "api reference" }, 
                "Learning", new[] { "documentation", "research", "technical-reading" }),
            
            // YouTube context-aware
            new WindowPatternRule(new[] { "youtube" }, 
                "Learning", new[] { "video", "education" }, 
                titleKeywords: new[] { "tutorial", "how to", "guide", "course", "learn", "programming", "coding", "development" }),
            new WindowPatternRule(new[] { "youtube" }, 
                "Entertainment", new[] { "video", "streaming" }, 
                titleKeywords: new[] { "music", "gaming", "vlog", "funny", "comedy", "entertainment" }),
            new WindowPatternRule(new[] { "youtube" }, 
                "Research", new[] { "video", "information" }, 
                titleKeywords: new[] { "review", "news", "documentary", "interview", "conference", "presentation" }),
            
            // Social Media (context matters)
            new WindowPatternRule(new[] { "twitter", "linkedin", "reddit" }, 
                "Social Media", new[] { "networking", "social-networking" }),
            new WindowPatternRule(new[] { "facebook", "instagram", "tiktok", "snapchat" }, 
                "Entertainment", new[] { "social-media", "networking" }),
            
            // Work-related patterns
            new WindowPatternRule(new[] { "jira", "confluence", "github", "gitlab", "bitbucket" }, 
                "Development", new[] { "project-management", "version-control", "collaboration" }),
            new WindowPatternRule(new[] { "stackoverflow", "stack overflow" }, 
                "Learning", new[] { "research", "problem-solving", "development" }),
            
            // Research & Reading
            new WindowPatternRule(new[] { "medium", "dev.to", "hashnode", "towards data science" }, 
                "Learning", new[] { "reading", "articles", "technical-content" }),
            new WindowPatternRule(new[] { "wikipedia", "arxiv", "scholar", "research" }, 
                "Research", new[] { "information-gathering", "academic" }),
            
            // Shopping & Finance
            new WindowPatternRule(new[] { "amazon", "ebay", "shopping", "store", "shop" }, 
                "Shopping", new[] { "e-commerce", "browsing" }),
            new WindowPatternRule(new[] { "bank", "paypal", "stripe", "finance", "investment" }, 
                "Finance", new[] { "banking", "financial-management" }),
            
            // News & Information
            new WindowPatternRule(new[] { "news", "bbc", "cnn", "reuters", "techcrunch", "hacker news" }, 
                "News", new[] { "information", "current-events" }),
        };

        public static CategoryResult Categorize(string appName, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return new CategoryResult("Uncategorized", new[] { "unknown" }, 0);

            var lowerApp = appName.ToLowerInvariant();
            var lowerTitle = (windowTitle ?? "").ToLowerInvariant();

            // First, check window patterns (more specific)
            foreach (var pattern in _windowPatterns)
            {
                if (pattern.Matches(lowerApp, lowerTitle))
                {
                    return new CategoryResult(pattern.Category, pattern.Tags, 85);
                }
            }

            // Then check app-based rules
            foreach (var kvp in _rules)
            {
                if (lowerApp.Contains(kvp.Key.ToLowerInvariant()))
                {
                    var rule = kvp.Value;
                    
                    // For browsers, try to refine based on window title
                    if (rule.Category == "Research" && !string.IsNullOrWhiteSpace(lowerTitle))
                    {
                        var refinedCategory = RefineBrowserCategory(lowerTitle);
                        if (refinedCategory != null)
                        {
                            return new CategoryResult(refinedCategory.Category, refinedCategory.Tags, 80);
                        }
                    }
                    
                    return new CategoryResult(rule.Category, rule.Tags, 90);
                }
            }

            // Enhanced default categorization - NEVER return Uncategorized
            // Use smart pattern matching to categorize everything
            
            // Gaming keywords
            if (ContainsAny(lowerApp, "game", "play", "steam", "epic", "battle", "blizzard", "riot", "xbox", "playstation"))
                return new CategoryResult("Entertainment", new[] { "gaming" }, 70);
            
            // Communication keywords
            if (ContainsAny(lowerApp, "mail", "outlook", "chat", "messenger", "teams", "slack", "zoom", "meet", "discord", "telegram", "whatsapp", "signal"))
                return new CategoryResult("Communication", new[] { "messaging" }, 70);
            
            // Development keywords
            if (ContainsAny(lowerApp, "code", "studio", "dev", "git", "terminal", "cmd", "shell", "compiler", "debugger", "ide"))
                return new CategoryResult("Development", new[] { "coding" }, 70);
            
            // Productivity keywords
            if (ContainsAny(lowerApp, "office", "word", "excel", "sheet", "doc", "note", "calendar", "task", "todo", "project"))
                return new CategoryResult("Productivity", new[] { "office-work" }, 70);
            
            // Media & Entertainment
            if (ContainsAny(lowerApp, "media", "player", "music", "video", "spotify", "netflix", "youtube", "vlc", "movie", "audio"))
                return new CategoryResult("Entertainment", new[] { "media" }, 70);
            
            // Design keywords
            if (ContainsAny(lowerApp, "design", "photo", "image", "draw", "paint", "adobe", "figma", "sketch", "canva"))
                return new CategoryResult("Design", new[] { "creative" }, 70);
            
            // Browser keywords (if not caught earlier)
            if (ContainsAny(lowerApp, "browser", "web", "chrome", "firefox", "edge", "safari", "opera"))
                return new CategoryResult("Research", new[] { "web-browsing" }, 70);
            
            // System utilities
            if (ContainsAny(lowerApp, "explorer", "finder", "manager", "settings", "control", "system", "utility", "tools"))
                return new CategoryResult("Utilities", new[] { "system" }, 70);
            
            // File management
            if (ContainsAny(lowerApp, "file", "folder", "zip", "archive", "backup", "sync", "drive", "cloud"))
                return new CategoryResult("Utilities", new[] { "file-management" }, 70);
            
            // Security & Privacy
            if (ContainsAny(lowerApp, "security", "antivirus", "firewall", "vpn", "password", "auth", "secure"))
                return new CategoryResult("Utilities", new[] { "security" }, 70);
            
            // Use window title as additional context for final fallback
            if (!string.IsNullOrWhiteSpace(lowerTitle))
            {
                // Check title for category hints
                if (ContainsAny(lowerTitle, "meeting", "call", "zoom", "teams", "conference"))
                    return new CategoryResult("Communication", new[] { "video-call" }, 65);
                
                if (ContainsAny(lowerTitle, "document", "edit", "write", "draft"))
                    return new CategoryResult("Productivity", new[] { "writing" }, 65);
                
                if (ContainsAny(lowerTitle, "code", "programming", "development", "github", "gitlab"))
                    return new CategoryResult("Development", new[] { "coding" }, 65);
                
                if (ContainsAny(lowerTitle, "video", "music", "stream", "watch", "listen"))
                    return new CategoryResult("Entertainment", new[] { "media" }, 65);
            }
            
            // Final fallback: If nothing matches, categorize by execution context
            // Desktop apps that don't match any pattern are likely utilities or custom business apps
            if (!string.IsNullOrWhiteSpace(lowerApp))
            {
                // If app name is simple/short, likely a utility or built-in app
                if (lowerApp.Length <= 10)
                    return new CategoryResult("Utilities", new[] { "system", "uncategorized-app" }, 60);
                
                // Longer app names are likely custom business/work apps
                return new CategoryResult("Productivity", new[] { "business-app", "custom-software" }, 60);
            }
            
            // Absolute last resort (should rarely/never happen)
            return new CategoryResult("Utilities", new[] { "unknown-app" }, 50);
        }

        private static CategoryResult? RefineBrowserCategory(string windowTitle)
        {
            // Check if it's learning/education content
            if (ContainsAny(windowTitle, "tutorial", "course", "learn", "how to", "guide", "documentation", "docs"))
                return new CategoryResult("Learning", new[] { "education", "web-browsing" }, 85);

            // Check if it's development related
            if (ContainsAny(windowTitle, "github", "gitlab", "stackoverflow", "stack overflow", "codepen", "jsfiddle", "dev.to"))
                return new CategoryResult("Development", new[] { "web-browsing", "coding" }, 85);

            // Check if it's entertainment
            if (ContainsAny(windowTitle, "netflix", "youtube - music", "spotify", "twitch", "gaming", "stream"))
                return new CategoryResult("Entertainment", new[] { "video", "streaming", "web-browsing" }, 85);

            // Check if it's social media
            if (ContainsAny(windowTitle, "facebook", "twitter", "instagram", "tiktok", "reddit", "linkedin"))
                return new CategoryResult("Social Media", new[] { "networking", "web-browsing" }, 85);

            // Check if it's shopping
            if (ContainsAny(windowTitle, "amazon", "ebay", "shop", "cart", "checkout", "buy", "store"))
                return new CategoryResult("Shopping", new[] { "e-commerce", "web-browsing" }, 85);

            // Check if it's news
            if (ContainsAny(windowTitle, "news", "bbc", "cnn", "reuters", "article"))
                return new CategoryResult("News", new[] { "information", "web-browsing" }, 85);

            return null; // Keep default "Research" category
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class CategoryRule
    {
        public string Category { get; }
        public string[] Tags { get; }

        public CategoryRule(string category, string[] tags)
        {
            Category = category;
            Tags = tags;
        }
    }

    public class WindowPatternRule
    {
        public string[] AppPatterns { get; }
        public string Category { get; }
        public string[] Tags { get; }
        public string[]? TitleKeywords { get; }

        public WindowPatternRule(string[] appPatterns, string category, string[] tags, string[]? titleKeywords = null)
        {
            AppPatterns = appPatterns;
            Category = category;
            Tags = tags;
            TitleKeywords = titleKeywords;
        }

        public bool Matches(string appName, string windowTitle)
        {
            // Check if app name or window title contains any of the patterns
            var textToMatch = $"{appName} {windowTitle}".ToLowerInvariant();
            
            foreach (var pattern in AppPatterns)
            {
                if (textToMatch.Contains(pattern.ToLowerInvariant()))
                {
                    // If we have title keywords, verify at least one matches
                    if (TitleKeywords != null && TitleKeywords.Length > 0)
                    {
                        return TitleKeywords.Any(k => windowTitle.Contains(k, StringComparison.OrdinalIgnoreCase));
                    }
                    return true;
                }
            }
            
            return false;
        }
    }

    public class CategoryResult
    {
        public string Category { get; }
        public string[] Tags { get; }
        public int Confidence { get; }

        public CategoryResult(string category, string[] tags, int confidence)
        {
            Category = category;
            Tags = tags;
            Confidence = confidence;
        }
    }
}
