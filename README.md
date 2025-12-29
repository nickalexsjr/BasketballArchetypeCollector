# Basketball Archetype Collector - .NET MAUI App

A .NET MAUI mobile app that replicates the NBA Cards Archetype Edition HTML game with full feature parity, Appwrite backend integration, and Codemagic CI/CD for iOS deployment.

---

## Table of Contents

1. [Overview](#overview)
2. [Feature Parity Checklist](#feature-parity-checklist)
3. [Architecture](#architecture)
4. [Data Models](#data-models)
5. [UI Specifications](#ui-specifications)
6. [Appwrite Backend Setup](#appwrite-backend-setup)
7. [API Integration (Appwrite Functions)](#api-integration-appwrite-functions)
8. [Codemagic CI/CD Setup](#codemagic-cicd-setup)
9. [Implementation Steps](#implementation-steps)
10. [File Structure](#file-structure)

---

## Overview

This app is a basketball card collector game where users:
- Open packs to collect player cards
- Each card has an AI-generated archetype crest (via OpenAI GPT-4.1-mini + DALL-E 2)
- Cards have rarities based on player overall rating
- Users can view their collection, browse the database, and track stats

**Target Platforms:** iOS only (initial release)

**Configuration:**
- **App Name:** Basketball Archetype Collector
- **Bundle ID:** `com.basketballarchetype.app`
- **Appwrite Region:** Sydney
- **Authentication:** Email sign-up

---

## Feature Parity Checklist

### Core Features (Must Match HTML Exactly)

#### Game State
- [ ] Starting coins: 1000
- [ ] Coin balance persists across sessions
- [ ] Collection of owned player cards (array of player IDs)
- [ ] Stats tracking:
  - [ ] Packs opened count
  - [ ] Cards collected count
  - [ ] GOAT cards count
  - [ ] Legendary cards count
  - [ ] Epic cards count
  - [ ] Rare cards count

#### Tabs/Navigation (Bottom Tab Bar)
- [ ] **Packs Tab** (icon: 📦) - Buy and open packs
- [ ] **Collection Tab** (icon: 🃏) - View owned cards
- [ ] **Database Tab** (icon: 📊) - Browse all players
- [ ] **Stats Tab** (icon: 📈) - View collection stats

#### Pack System
| Pack ID | Name | Cards | Cost | Description | Guaranteed | Boosts |
|---------|------|-------|------|-------------|------------|--------|
| standard | Standard Pack | 3 | 100 | 3 random cards | - | - |
| premium | Premium Pack | 5 | 250 | Better odds | - | rare: 1.5x, epic: 1.3x, legendary: 1.2x |
| elite | Elite Pack | 5 | 500 | Guaranteed Rare+ | rare | epic: 2x, legendary: 1.5x |
| legendary | Legendary Pack | 3 | 1000 | Guaranteed Epic+ | epic | legendary: 3x |

#### Rarity System
| Rarity | Label | Min Overall | Chance % | Coin Value | Sell Value | Color | Special |
|--------|-------|-------------|----------|------------|------------|-------|---------|
| goat | GOAT | 99 | 0.5 | 2000 | 1000 | Red (#DC143C) | MJ & LeBron only, pulsing glow |
| legendary | LEGENDARY | 94 | 2 | 500 | 250 | Gold (#FFD700) | - |
| epic | EPIC | 88 | 8 | 200 | 100 | Purple (#9B59B6) | - |
| rare | RARE | 80 | 15 | 75 | 37 | Blue (#3498DB) | - |
| uncommon | UNCOMMON | 72 | 25 | 30 | 15 | Green (#2ECC71) | - |
| common | COMMON | 0 | 50 | 10 | 5 | Gray (#7F8C8D) | - |

#### Era System
| Era | Draft Year Range | Badge Color |
|-----|------------------|-------------|
| modern | 2020+ | #2ECC71 (green) |
| 2010s | 2010-2019 | #3498DB (blue) |
| 2000s | 2000-2009 | #9B59B6 (purple) |
| 90s | 1990-1999 | #E74C3C (red) |
| 80s | 1980-1989 | #F39C12 (orange) |
| classic | Before 1980 | #1ABC9C (teal) |
| unknown | No draft year | #7F8C8D (gray) |

#### Overall Rating Calculation
```
Hardcoded Players (always this rating):
- Michael Jordan: 99
- LeBron James: 99
- Kareem Abdul-Jabbar: 98
- Nikola Jokic: 98
- Wilt Chamberlain: 98
- Tim Duncan: 98

For players with <10 games or 0 PPG:
- Draft Round 1, Pick 1-3: 75
- Draft Round 1, Pick 4-10: 72
- Draft Round 1, Pick 11+: 68
- Draft Round 2: 65
- Undrafted/Unknown: 62

For players with stats:
- ptsScore = min(100, (ppg / 25) * 100)
- rebScore = min(100, (rpg / 10) * 100)
- astScore = min(100, (apg / 8) * 100)
- defScore = min(100, ((spg + bpg) / 2.5) * 100)
- effScore = fgPct > 0 ? min(100, (fgPct / 0.50) * 100) : 50
- longevityBonus = min(3, (games / 1200) * 3)
- rawScore = (ptsScore * 0.40) + (rebScore * 0.15) + (astScore * 0.18) + (defScore * 0.12) + (effScore * 0.15)
- overall = 52 + (rawScore * 0.47) + longevityBonus
- Clamped to range [60, 98]
```

#### Ranking System
- Players sorted by: overall (desc) → sortTiebreaker (desc) → ppg (desc)
- sortTiebreaker: MJ = 2, LeBron = 1, others = 0
- Rank assigned only to players with stats (hasStats = scrape_status === 'found' && games > 0)

#### Pack Opening Flow
1. User taps pack → deduct coins
2. Show loading screen with:
   - Pack name
   - Spinning loader animation
   - "Generating card X of Y..." text
   - Progress bar (0-100%)
   - "Creating unique archetype crests..." subtitle
3. Generate cards sequentially (one at a time to avoid rate limits)
4. For each card:
   - Call OpenAI GPT-4.1-mini for archetype data
   - Call DALL-E 2 for crest image (256x256)
   - Cache result
5. After all cards generated:
   - Show all cards side by side
   - Click card to view details modal
   - "Close" button to dismiss
   - "Sell All" button with total coin value
6. Duplicate cards auto-sell for half coin value

#### Card Display (Two Sizes)

**Small Card (Collection Grid):**
- Size: 128x192 (w-32 h-48 in Tailwind)
- Padding: 8px (p-2)
- Elements:
  - Top left: Overall rating badge (black/30% bg)
  - Top right: Position badge (black/30% bg)
  - Center: Crest image (48x48) or "?" placeholder
  - Bottom: First name, Last name (text-xs, truncate)
  - Team abbreviation (text-xs, 75% opacity)
  - Archetype badge if unlocked (purple gradient, truncate)
  - Stats grid (9px font): PPG, RPG, APG, GP

**Normal Card (Pack Opening):**
- Size: 192x288 (w-48 h-72)
- Padding: 12px (p-3)
- Same layout as small but larger fonts/images
- Crest image: 64x64
- Stats: 12px font

**Card Styling by Rarity:**
- Gradient background (135deg)
- Glow shadow
- Shine animation (diagonal sweep, 4s infinite)
- GOAT has additional pulse animation (2s infinite)

#### Archetype Modal (Card Details)
- Backdrop blur effect
- Dark gradient background with gold border
- Shows when clicking any card
- Content:
  - Large crest image (150x150) if unlocked
  - Loading spinner if generating
  - Error message if failed
  - "Generate Crest" button if not yet generated
  - Player info: Name, Team, Overall, Position, Rarity
  - Archetype name badge
  - Play style summary text
  - Stats: PPG, RPG, APG, GP, Era
  - Sell button (if owned): "Sell for X coins"
  - Back/Close button

#### Collection View
- Search input (filter by name)
- Rarity filter dropdown: All, GOAT, Legendary, Epic, Rare, Uncommon, Common
- Sort dropdown: Overall, Name, Rarity
- Grid layout: 2 cols mobile, 3 cols sm, 4 cols md, 6 cols lg
- Shows first 50 cards
- Card count display: "X cards (Y total collected)"

#### Database View
- Search input (filter by name)
- Rarity filter dropdown (same as collection + GOAT)
- Unlocked filter dropdown: All Cards, Unlocked, Locked
- Paginated table (50 per page)
- Columns: Rank, Crest (thumbnail or ?), OVR, Name, Team, PPG, RPG, APG, Rarity, Archetype (or ?)
- Owned players highlighted with green background
- Page navigation: Prev/Next buttons, page indicator

#### Stats View
- Packs Opened (teal gradient box)
- Cards Collected (green gradient box)
- Crests Generated (purple gradient box)
- Rarity breakdown grid:
  - GOAT count (red)
  - Legendary count (yellow)
  - Epic count (purple)
  - Rare count (blue)
- Database Info: "Total Players: 5042"
- "+1000 Coins" button (for testing)

#### Animations
- [ ] Card shine effect: diagonal white sweep, 4s infinite loop
- [ ] Pack glow: box-shadow pulse, 2s infinite
- [ ] GOAT pulse: red glow pulse, 2s infinite
- [ ] Loading spinner: rotate 360deg, 1s linear infinite
- [ ] Card hover: scale 1.05 transform
- [ ] Progress bar: smooth width transition

#### Colors & Styling
- Background: Gradient from slate-900 via gray-900 to black
- Primary font: Inter
- Display font: Orbitron (for titles)
- Scrollbar: 8px, white/10% track, #48dbfb thumb
- Modal backdrop: black/80-90% with blur

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET MAUI App                            │
├─────────────────────────────────────────────────────────────┤
│  Views (XAML)          │  ViewModels (MVVM)                 │
│  - MainPage            │  - MainViewModel                   │
│  - PacksPage           │  - PacksViewModel                  │
│  - CollectionPage      │  - CollectionViewModel             │
│  - DatabasePage        │  - DatabaseViewModel               │
│  - StatsPage           │  - StatsViewModel                  │
│  - CardDetailModal     │  - CardDetailViewModel             │
├─────────────────────────────────────────────────────────────┤
│  Services                                                   │
│  - AppwriteService (Database, Auth, Storage)                │
│  - ArchetypeApiService (calls Appwrite Function)            │
│  - GameStateService (local state management)                │
│  - PlayerDataService (player database)                      │
├─────────────────────────────────────────────────────────────┤
│  Models                                                     │
│  - Player, PlayerCard, Pack, Rarity, Era, GameState         │
│  - ArchetypeData, CrestDesign                               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Appwrite Backend                         │
├─────────────────────────────────────────────────────────────┤
│  Database Collections:                                      │
│  - users (user profiles, coins, stats)                      │
│  - collections (owned cards per user)                       │
│  - archetype_cache (generated archetypes + image URLs)      │
│  - players (optional: player data if not embedded)          │
├─────────────────────────────────────────────────────────────┤
│  Functions:                                                 │
│  - generateArchetype (calls OpenAI GPT + DALL-E)            │
├─────────────────────────────────────────────────────────────┤
│  Storage:                                                   │
│  - crest_images (uploaded crest images for persistence)     │
└─────────────────────────────────────────────────────────────┘
```

---

## Data Models

### Player (from CSV)
```csharp
public class Player
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string TeamId { get; set; }
    public string TeamAbbreviation { get; set; }
    public string Position { get; set; }
    public string Height { get; set; }
    public string DraftYear { get; set; }
    public string DraftRound { get; set; }
    public string DraftNumber { get; set; }
    public int Games { get; set; }
    public float Ppg { get; set; }
    public float Rpg { get; set; }
    public float Apg { get; set; }
    public float Spg { get; set; }
    public float Bpg { get; set; }
    public float FgPct { get; set; }
    public string ScrapeStatus { get; set; }

    // Computed properties
    public int Overall { get; set; }
    public int? Rank { get; set; }
    public Rarity Rarity { get; set; }
    public Era Era { get; set; }
    public bool HasStats { get; set; }
    public bool IsActive { get; set; }
}
```

### GameState
```csharp
public class GameState
{
    public int Coins { get; set; } = 1000;
    public List<string> Collection { get; set; } = new();
    public GameStats Stats { get; set; } = new();
}

public class GameStats
{
    public int PacksOpened { get; set; }
    public int CardsCollected { get; set; }
    public int GoatCount { get; set; }
    public int LegendaryCount { get; set; }
    public int EpicCount { get; set; }
    public int RareCount { get; set; }
}
```

### ArchetypeData
```csharp
public class ArchetypeData
{
    public string PlayerName { get; set; }
    public string Confidence { get; set; } // high, medium, low
    public string PlayStyleSummary { get; set; }
    public string Archetype { get; set; }
    public string SubArchetype { get; set; }
    public string CrestSeed { get; set; }
    public CrestDesign CrestDesign { get; set; }
    public string ImagePrompt { get; set; }
    public string CrestImageUrl { get; set; }
}

public class CrestDesign
{
    public string CoreShape { get; set; }
    public string PrimaryMotif { get; set; }
    public List<string> SecondaryMotifs { get; set; }
    public string PatternLanguage { get; set; }
    public List<string> Materials { get; set; }
    public string ColorStory { get; set; }
    public string NegativeSpaceRule { get; set; }
    public List<string> DoNotInclude { get; set; }
}
```

### Pack
```csharp
public class Pack
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Cards { get; set; }
    public int Cost { get; set; }
    public string Description { get; set; }
    public string GradientFrom { get; set; }
    public string GradientTo { get; set; }
    public string Icon { get; set; }
    public string Guaranteed { get; set; } // null, "rare", "epic"
    public Dictionary<string, float> Boost { get; set; }
}
```

---

## UI Specifications

### Color Palette
```csharp
public static class AppColors
{
    // Background
    public static Color BgDark = Color.FromHex("#0f172a"); // slate-900
    public static Color BgMid = Color.FromHex("#111827"); // gray-900
    public static Color BgBlack = Color.FromHex("#000000");

    // Rarity Colors
    public static Color GoatPrimary = Color.FromHex("#DC143C");
    public static Color GoatSecondary = Color.FromHex("#8B0000");
    public static Color LegendaryPrimary = Color.FromHex("#FFD700");
    public static Color LegendarySecondary = Color.FromHex("#FF8C00");
    public static Color EpicPrimary = Color.FromHex("#9B59B6");
    public static Color EpicSecondary = Color.FromHex("#8E44AD");
    public static Color RarePrimary = Color.FromHex("#3498DB");
    public static Color RareSecondary = Color.FromHex("#2980B9");
    public static Color UncommonPrimary = Color.FromHex("#2ECC71");
    public static Color UncommonSecondary = Color.FromHex("#27AE60");
    public static Color CommonPrimary = Color.FromHex("#7F8C8D");
    public static Color CommonSecondary = Color.FromHex("#95A5A6");

    // Text
    public static Color TextPrimary = Color.FromHex("#FFFFFF");
    public static Color TextSecondary = Color.FromHex("#FFFFFF").WithAlpha(0.6f);
    public static Color TextMuted = Color.FromHex("#FFFFFF").WithAlpha(0.4f);

    // Accent
    public static Color Accent = Color.FromHex("#48dbfb");
    public static Color ArchetypeBadge = Color.FromHex("#667eea");
}
```

### Font Configuration
- Primary: Inter (body text)
- Display: Orbitron (titles, headers)
- Include both as embedded resources

---

## Appwrite Backend Setup

### Project Configuration
```
Project ID: basketball-archetype-collector
Region: [Your preferred region]
```

### Database Collections

#### 1. `users` Collection
| Attribute | Type | Required | Description |
|-----------|------|----------|-------------|
| userId | string | Yes | Unique user identifier |
| coins | integer | Yes | Current coin balance (default: 1000) |
| stats | document | Yes | Embedded GameStats |
| createdAt | datetime | Yes | Account creation timestamp |
| updatedAt | datetime | Yes | Last update timestamp |

#### 2. `collections` Collection
| Attribute | Type | Required | Description |
|-----------|------|----------|-------------|
| userId | string | Yes | Owner user ID |
| playerIds | string[] | Yes | Array of owned player IDs |

#### 3. `archetype_cache` Collection
| Attribute | Type | Required | Description |
|-----------|------|----------|-------------|
| playerId | string | Yes | Player ID (unique index) |
| playerName | string | Yes | Full player name |
| archetype | string | Yes | Archetype name |
| subArchetype | string | No | Sub-archetype name |
| playStyleSummary | string | No | Play style description |
| crestImageUrl | string | No | URL to crest image |
| crestImageFileId | string | No | Appwrite Storage file ID |
| confidence | string | No | high/medium/low |
| imagePrompt | string | No | DALL-E prompt used |
| createdAt | datetime | Yes | Generation timestamp |

### Indexes
- `archetype_cache`: Unique index on `playerId`
- `collections`: Index on `userId`
- `users`: Unique index on `userId`

### Storage Buckets

#### `crest_images` Bucket
- File size limit: 5MB
- Allowed extensions: jpg, jpeg, png, webp
- Permissions: Read (any), Write (users)

---

## API Integration (Appwrite Functions)

### Function: `generateArchetype`

**Runtime:** Node.js 18.0

**Environment Variables:**
```
OPENAI_API_KEY=sk-proj-xxx...
```

**Endpoint:** POST `/v1/functions/{functionId}/executions`

**Request Body:**
```json
{
  "playerId": "string",
  "playerName": "string",
  "statHints": "PPG: 27.4, RPG: 5.1, APG: 7.4, Games: 1421, Position: SF"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "archetype": "Aerial Assassin",
    "subArchetype": "Acrobatic Slasher",
    "playStyleSummary": "...",
    "crestImageUrl": "https://...",
    "confidence": "high"
  }
}
```

**Function Code (Node.js):**
```javascript
// See separate function code file
// Calls OpenAI GPT-4.1-mini for archetype generation
// Calls DALL-E 2 for image generation (256x256)
// Uploads image to Appwrite Storage
// Caches result in archetype_cache collection
```

---

## Codemagic CI/CD Setup

### File: `codemagic.yaml`

```yaml
workflows:
  ios-release:
    name: Basketball Archetype Collector iOS
    max_build_duration: 60
    instance_type: mac_mini_m1

    environment:
      xcode: 16.0
      groups:
        - ios_signing  # CERTIFICATE_P12, PROVISIONING_PROFILE, CERTIFICATE_PASSWORD
        - app_store    # APP_STORE_API_KEY, KEY_ID, ISSUER_ID
      vars:
        BUNDLE_ID: "com.najdev.basketballarchetype"
        APP_NAME: "Basketball Archetype Collector"

    triggering:
      events:
        - push
      branch_patterns:
        - pattern: 'main'
          include: true

    scripts:
      - name: Install .NET 8 SDK
        script: |
          curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version 8.0.100
          export DOTNET_ROOT=$HOME/.dotnet
          export PATH=$PATH:$DOTNET_ROOT

      - name: Install MAUI Workloads
        script: |
          export DOTNET_ROOT=$HOME/.dotnet
          export PATH=$PATH:$DOTNET_ROOT
          $HOME/.dotnet/dotnet workload install maui

      - name: Restore NuGet Packages
        script: |
          export DOTNET_ROOT=$HOME/.dotnet
          export PATH=$PATH:$DOTNET_ROOT
          $HOME/.dotnet/dotnet restore BasketballArchetypeCollector.csproj -r ios-arm64

      - name: Decode Signing Assets
        script: |
          echo "$CERTIFICATE_P12" | base64 --decode > certificate.p12
          echo "$PROVISIONING_PROFILE" | base64 --decode > profile.mobileprovision

      - name: Setup Keychain & Import Certificate
        script: |
          # [Same keychain setup as BragStack]

      - name: Install Provisioning Profile
        script: |
          # [Same profile installation as BragStack]

      - name: Build & Publish iOS
        script: |
          export DOTNET_ROOT=$HOME/.dotnet
          export PATH=$PATH:$DOTNET_ROOT

          CERT_IDENTITY=$(security find-identity -v -p codesigning | grep -o '"[^"]*"' | head -1 | tr -d '"')
          source ~/.bash_profile

          $HOME/.dotnet/dotnet publish BasketballArchetypeCollector.csproj \
            -c Release \
            -f net8.0-ios \
            /p:RuntimeIdentifier=ios-arm64 \
            /p:ArchiveOnBuild=true \
            /p:BuildIpa=true \
            /p:CodesignKey="$CERT_IDENTITY" \
            /p:CodesignProvision="$PROVISIONING_UUID" \
            -o ./publish

      - name: Upload to TestFlight
        script: |
          # [Same TestFlight upload as BragStack]

    artifacts:
      - "**/*.ipa"

    publishing:
      email:
        recipients:
          - nick.alexs@najdevelopments.com.au
```

---

## Implementation Steps

### Phase 1: Project Setup
1. [ ] Create .NET MAUI project: `dotnet new maui -n BasketballArchetypeCollector`
2. [ ] Add NuGet packages:
   - [ ] CommunityToolkit.Mvvm
   - [ ] Appwrite (SDK)
   - [ ] System.Text.Json
3. [ ] Configure app icons and splash screen
4. [ ] Add Inter and Orbitron fonts as embedded resources
5. [ ] Set up color resources and styles

### Phase 2: Data Layer
6. [ ] Create all model classes (Player, GameState, Pack, etc.)
7. [ ] Embed player_career_stats_FINAL.csv as resource
8. [ ] Implement PlayerDataService to load and process players
9. [ ] Implement calculateOverall() logic
10. [ ] Implement determineRarity() logic
11. [ ] Implement getEra() logic
12. [ ] Implement player ranking system

### Phase 3: Services
13. [ ] Implement AppwriteService (initialize SDK, auth, database)
14. [ ] Implement GameStateService (local state + sync to Appwrite)
15. [ ] Implement ArchetypeApiService (call Appwrite Function)
16. [ ] Implement local caching for archetype data

### Phase 4: Views - Core
17. [ ] Create AppShell with bottom tab navigation
18. [ ] Implement PacksPage (pack grid, purchase flow)
19. [ ] Implement pack opening loading screen with progress
20. [ ] Implement pack reveal screen (cards side by side)
21. [ ] Implement CollectionPage (grid, search, filter, sort)
22. [ ] Implement DatabasePage (table, pagination, filters)
23. [ ] Implement StatsPage (stats display, +1000 coins button)

### Phase 5: Card Components
24. [ ] Create CardView component (small size)
25. [ ] Create CardView component (normal size)
26. [ ] Implement rarity-based styling (gradients, shadows)
27. [ ] Implement shine animation
28. [ ] Implement GOAT pulse animation
29. [ ] Implement card tap to open modal

### Phase 6: Archetype Modal
30. [ ] Create CardDetailModal popup
31. [ ] Implement loading state with spinner
32. [ ] Implement archetype display (crest image, stats)
33. [ ] Implement generate button for uncached archetypes
34. [ ] Implement sell button functionality
35. [ ] Implement back/close navigation

### Phase 7: Appwrite Backend
36. [ ] Create Appwrite project
37. [ ] Create database collections (users, collections, archetype_cache)
38. [ ] Create storage bucket (crest_images)
39. [ ] Create generateArchetype function
40. [ ] Deploy function with OpenAI API key
41. [ ] Test function endpoint

### Phase 8: Integration
42. [ ] Connect app to Appwrite (initialize in App.xaml.cs)
43. [ ] Implement user authentication (anonymous or email)
44. [ ] Sync game state to Appwrite on changes
45. [ ] Load archetype cache from Appwrite
46. [ ] Store generated archetypes in Appwrite
47. [ ] Upload crest images to Appwrite Storage

### Phase 9: Polish
48. [ ] Add loading states throughout app
49. [ ] Add error handling and retry logic
50. [ ] Implement offline mode with local caching
51. [ ] Test all animations match HTML version
52. [ ] Test all colors/styling match HTML version
53. [ ] Performance optimization

### Phase 10: CI/CD & Deployment
54. [ ] Create codemagic.yaml
55. [ ] Set up Codemagic environment variables
56. [ ] Configure iOS signing (certificate, provisioning profile)
57. [ ] Test build pipeline
58. [ ] Deploy to TestFlight
59. [ ] Create App Store listing (if publishing)

---

## File Structure

```
BasketballArchetypeCollector/
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
├── AppShell.xaml.cs
├── MauiProgram.cs
├── BasketballArchetypeCollector.csproj
├── codemagic.yaml
├── README.md
│
├── Models/
│   ├── Player.cs
│   ├── Pack.cs
│   ├── Rarity.cs
│   ├── Era.cs
│   ├── GameState.cs
│   ├── GameStats.cs
│   ├── ArchetypeData.cs
│   └── CrestDesign.cs
│
├── ViewModels/
│   ├── BaseViewModel.cs
│   ├── MainViewModel.cs
│   ├── PacksViewModel.cs
│   ├── CollectionViewModel.cs
│   ├── DatabaseViewModel.cs
│   ├── StatsViewModel.cs
│   └── CardDetailViewModel.cs
│
├── Views/
│   ├── PacksPage.xaml
│   ├── PacksPage.xaml.cs
│   ├── CollectionPage.xaml
│   ├── CollectionPage.xaml.cs
│   ├── DatabasePage.xaml
│   ├── DatabasePage.xaml.cs
│   ├── StatsPage.xaml
│   ├── StatsPage.xaml.cs
│   └── CardDetailModal.xaml
│   └── CardDetailModal.xaml.cs
│
├── Controls/
│   ├── CardView.xaml
│   ├── CardView.xaml.cs
│   ├── PackView.xaml
│   ├── PackView.xaml.cs
│   ├── LoadingSpinner.xaml
│   └── LoadingSpinner.xaml.cs
│
├── Services/
│   ├── IAppwriteService.cs
│   ├── AppwriteService.cs
│   ├── IGameStateService.cs
│   ├── GameStateService.cs
│   ├── IPlayerDataService.cs
│   ├── PlayerDataService.cs
│   ├── IArchetypeApiService.cs
│   └── ArchetypeApiService.cs
│
├── Helpers/
│   ├── OverallCalculator.cs
│   ├── RarityHelper.cs
│   ├── EraHelper.cs
│   └── StableSeedGenerator.cs
│
├── Resources/
│   ├── Fonts/
│   │   ├── Inter-Regular.ttf
│   │   ├── Inter-Bold.ttf
│   │   ├── Orbitron-Regular.ttf
│   │   └── Orbitron-Bold.ttf
│   ├── Images/
│   │   └── [app icons, etc.]
│   ├── Raw/
│   │   └── player_career_stats_FINAL.csv
│   ├── Styles/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   └── AppIcon/
│       └── [icon files]
│
├── Platforms/
│   ├── iOS/
│   │   ├── Info.plist
│   │   ├── Entitlements.plist
│   │   └── AppDelegate.cs
│   └── Android/
│       ├── AndroidManifest.xml
│       └── MainActivity.cs
│
└── AppwriteFunction/
    └── generateArchetype/
        ├── package.json
        └── index.js
```

---

## Notes

- **Player Data:** The CSV file has 5527 total players, 5042 with stats
- **DALL-E Cost:** ~$0.016 per image (256x256 with DALL-E 2)
- **Rate Limits:** Generate crests sequentially to avoid OpenAI rate limits
- **Image Persistence:** DALL-E URLs expire; upload to Appwrite Storage for permanence
- **Offline Support:** Cache archetype data locally for offline viewing

---

## App Icon Setup (BragStack Style)

The app icon is set up using the same approach as BragStack - a single PNG file that MAUI auto-generates all required sizes from.

### Setup Steps:

1. **Place your icon file:**
   ```
   Platforms/iOS/appicon.png
   ```
   - Recommended size: 1024x1024 pixels
   - Format: PNG with transparency if needed

2. **The .csproj file references it like this:**
   ```xml
   <ItemGroup>
       <!-- App Icon - Use PNG for iOS like BragStack -->
       <MauiIcon Include="Platforms/iOS/appicon.png" Color="#0f172a" BaseSize="128,128" />
   </ItemGroup>

   <!-- iOS specific - include source icon as BundleResource -->
   <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
       <BundleResource Include="Platforms\iOS\appicon.png" Link="Resources\appicon.png" />
   </ItemGroup>
   ```

3. **Color parameter:** `#0f172a` (slate-900) is the background color for any padding needed

### Current Icon Location:
The `appicon.png` file should be placed at:
```
BasketballArchetypeCollector/Platforms/iOS/appicon.png
```

---

## Notes

- **Player Data:** The CSV file has 5527 total players, 5042 with stats
- **DALL-E Cost:** ~$0.016 per image (256x256 with DALL-E 2)
- **Rate Limits:** Generate crests sequentially to avoid OpenAI rate limits
- **Image Persistence:** DALL-E URLs expire; upload to Appwrite Storage for permanence
- **Offline Support:** Cache archetype data locally for offline viewing

---

*Last Updated: December 29, 2025*
