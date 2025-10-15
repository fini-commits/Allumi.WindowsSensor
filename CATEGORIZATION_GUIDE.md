# 🎯 Activity Categorization System

## Overview
The Windows Sensor app uses a **dual-layer categorization system**:

1. **Smart App-Side Categorization** (Immediate, 50-90% confidence)
   - Analyzes app name + window title
   - Provides context-aware categories
   - Works offline, no API calls needed
   
2. **AI Refinement** (Backend, after sync)
   - Improves categorization with AI insights
   - Adds subcategories, confidence scores, reasoning
   - Provides productivity scores

---

## 📊 Category List

The app intelligently assigns these categories based on context:

### Primary Categories

| Category | Description | Examples |
|----------|-------------|----------|
| **Development** | Coding, programming, technical work | VS Code, IntelliJ, GitHub, Terminal |
| **Design** | Creative and UI/UX work | Figma, Photoshop, Sketch, Canva |
| **Communication** | Meetings, messaging, email | Teams, Slack, Zoom, Outlook |
| **Productivity** | Document editing, spreadsheets, notes | Word, Excel, Notion, Trello |
| **Learning** | Tutorials, courses, documentation | YouTube (tutorials), Udemy, Stack Overflow |
| **Research** | Information gathering, reading | Wikipedia, articles, technical docs |
| **Entertainment** | Gaming, music, videos | YouTube (entertainment), Spotify, Netflix |
| **Social Media** | Networking, social platforms | Twitter, LinkedIn, Facebook |
| **Shopping** | E-commerce, browsing stores | Amazon, eBay |
| **Finance** | Banking, financial management | Banking apps, PayPal, investment tools |
| **News** | Current events, news reading | News sites, Hacker News, TechCrunch |
| **Utilities** | System tools, file management | Explorer, Task Manager, Settings |
| **Uncategorized** | Unknown apps (AI will categorize) | New or unrecognized applications |

---

## 🤖 Context-Aware Intelligence

### YouTube Smart Categorization
YouTube is **NOT always entertainment**! The app analyzes window titles:

| Window Title Contains | Category | Tags |
|----------------------|----------|------|
| "tutorial", "how to", "guide", "course" | **Learning** | education, video |
| "music", "gaming", "vlog", "funny" | **Entertainment** | video, streaming |
| "review", "news", "documentary" | **Research** | video, information |

**Examples:**
- `YouTube - Python Tutorial for Beginners` → **Learning**
- `YouTube - Top 10 Funny Cat Videos` → **Entertainment**
- `YouTube - M1 MacBook Pro Review` → **Research**

### Browser Smart Categorization
Browsers default to **Research**, but the app refines based on content:

| URL/Title Contains | Category | Tags |
|-------------------|----------|------|
| "github", "stackoverflow", "dev.to" | **Development** | coding, web-browsing |
| "netflix", "spotify", "twitch" | **Entertainment** | streaming, web-browsing |
| "facebook", "twitter", "instagram" | **Social Media** | networking, web-browsing |
| "amazon", "ebay", "shop", "cart" | **Shopping** | e-commerce, web-browsing |
| "news", "bbc", "cnn", "article" | **News** | information, web-browsing |
| "tutorial", "course", "learn", "docs" | **Learning** | education, web-browsing |

**Examples:**
- `Chrome - GitHub Repository` → **Development**
- `Chrome - Netflix` → **Entertainment**
- `Firefox - Amazon.com Shopping Cart` → **Shopping**
- `Edge - Stack Overflow - How to fix...` → **Learning**

---

## 📝 Categorization Logic

### Level 1: App-Based Rules (90% confidence)
Direct app name matching with predefined categories.

```csharp
"Visual Studio Code" → Development
"Figma" → Design
"Slack" → Communication
"Spotify" → Entertainment
```

### Level 2: Window Pattern Rules (85% confidence)
Analyzes window title for specific patterns.

```csharp
"YouTube" + "tutorial" → Learning
"YouTube" + "music" → Entertainment
"Chrome" + "github" → Development
```

### Level 3: Browser Refinement (85% confidence)
Refines browser activity based on URL/title content.

```csharp
Browser + "stackoverflow" → Learning
Browser + "netflix" → Entertainment
Browser + "news" → News
```

### Level 4: Fallback Patterns (70% confidence)
Generic pattern matching for unknown apps.

```csharp
Contains "game" → Entertainment
Contains "mail" → Communication
Contains "chat" → Communication
```

### Level 5: Unknown (50% confidence)
Unrecognized apps marked as "Uncategorized" for AI to handle.

```csharp
"MyCustomApp.exe" → Uncategorized
```

---

## 🗄️ Database Schema Compatibility

### `device_activities` Table Fields

The app sends:
```json
{
  "appName": "Visual Studio Code",
  "windowTitle": "Program.cs - Allumi.WindowsSensor",
  "category": "Development",  // ← Smart category from app
  "startTime": "2025-10-15T18:45:00Z",
  "endTime": "2025-10-15T18:46:30Z",
  "durationSeconds": 90,
  "isIdle": false
}
```

Database stores:
```sql
app_name             | Visual Studio Code
window_title         | Program.cs - Allumi.WindowsSensor
category             | Development              -- From app
ai_subcategory       | NULL                     -- AI fills later
ai_confidence        | NULL                     -- AI fills later
ai_reasoning         | NULL                     -- AI fills later
productivity_score   | NULL                     -- AI fills later
```

After AI refinement:
```sql
app_name             | Visual Studio Code
window_title         | Program.cs - Allumi.WindowsSensor
category             | Development              -- App's smart category
ai_subcategory       | Coding                   -- AI's subcategory
ai_confidence        | 98                       -- AI's confidence
ai_reasoning         | "VS Code editing C# file in Windows Sensor project"
productivity_score   | 90                       -- AI's productivity rating
```

✅ **The database `category` field is a TEXT field** and can store ANY category the app sends!

---

## 🎯 Benefits of App-Side Categorization

### 1. **Instant Results**
- No waiting for AI to categorize
- Dashboard shows meaningful categories immediately
- Users see "Development" not "other"

### 2. **Offline Support**
- Works without internet connection
- No dependency on AI API availability
- Continues tracking even if backend is down

### 3. **Cost Effective**
- Reduces AI API calls by 80%+
- Only unknown apps need AI analysis
- AI refines instead of categorizing from scratch

### 4. **Context Awareness**
- YouTube as Learning vs Entertainment
- Browser activities properly categorized
- Window title analysis provides intent

### 5. **Fallback Safety**
- If AI fails, we still have good categories
- User never sees "other" or "uncategorized" for common apps
- Data is immediately useful

---

## 🔄 Sync Flow with Smart Categorization

```
User switches from Chrome to VS Code
       │
       ▼
CloseCurrentSession() fires
       │
       ├─ _curProc = "chrome"
       ├─ _curTitle = "Stack Overflow - How to..."
       │
       ▼
ActivityCategorizer.Categorize()
       │
       ├─ Checks app patterns → Chrome (browser)
       ├─ Checks window patterns → "stackoverflow"
       └─ Result: Category = "Learning", Confidence = 85%
       │
       ▼
Creates ActivityEvent
       │
       └─ {
             appName: "chrome",
             windowTitle: "Stack Overflow - How to...",
             category: "Learning",  // ← Smart category!
             durationSeconds: 120
           }
       │
       ▼
SendActivityImmediatelyAsync()
       │
       └─ POST to /sync-device-activity
              │
              ▼
         Edge Function
              │
              ├─ INSERT INTO device_activities
              │  (category = "Learning" from app)
              │
              └─ Trigger AI categorization (async)
                     │
                     └─ UPDATE device_activities SET
                        ai_subcategory = "Problem Solving",
                        ai_confidence = 95,
                        ai_reasoning = "Stack Overflow technical Q&A",
                        productivity_score = 85
```

---

## 📊 Expected Category Distribution

For a typical developer's day:

| Category | % of Time | Examples |
|----------|-----------|----------|
| Development | 40% | VS Code, Terminal, GitHub |
| Communication | 15% | Slack, Teams, Email |
| Learning | 20% | Docs, Stack Overflow, Tutorials |
| Research | 10% | Browser research, Wikipedia |
| Entertainment | 5% | Music, YouTube breaks |
| Productivity | 5% | Note-taking, Task management |
| Utilities | 5% | File management, System tools |

---

## ✅ Testing Checklist

After installing v1.0.21, verify smart categorization:

### 1. Development Tools
- Open VS Code → Should be **Development**
- Open Terminal → Should be **Development**
- Browse GitHub → Should be **Development**

### 2. YouTube Context
- Watch "Python Tutorial" → Should be **Learning**
- Watch "Music Video" → Should be **Entertainment**
- Watch "Tech Review" → Should be **Research**

### 3. Browser Activities
- Amazon shopping → Should be **Shopping**
- Facebook → Should be **Social Media**
- Stack Overflow → Should be **Learning**
- Netflix → Should be **Entertainment**

### 4. Communication
- Slack → Should be **Communication**
- Zoom call → Should be **Communication**
- Outlook → Should be **Communication**

### 5. Check Logs
```powershell
Get-Content "$env:APPDATA\Allumi\sensor.log" -Tail 20
```

Should see:
```
2025-10-15T18:45:00Z  proc=Code  title=Program.cs  category=Development  confidence=90%  dur=120s
2025-10-15T18:47:00Z  proc=chrome  title=YouTube - Python Tutorial  category=Learning  confidence=85%  dur=180s
```

---

## 🚀 Future Enhancements

Potential improvements:

1. **User Customization**: Let users define their own categorization rules
2. **Machine Learning**: Learn from user's category corrections
3. **Project Detection**: Categorize based on active project/folder
4. **Time-Based Rules**: Work hours vs personal time categories
5. **Domain Whitelist**: Company-specific app categorizations

---

## 📚 Summary

✅ **App sends intelligent categories immediately**  
✅ **AI refines and enhances after sync**  
✅ **Context-aware (YouTube, browsers)**  
✅ **Database compatible (TEXT field)**  
✅ **Offline capable**  
✅ **Cost effective**  
✅ **User sees meaningful data instantly**  

**Result**: Users get accurate, context-aware activity tracking from the moment they install! 🎉
