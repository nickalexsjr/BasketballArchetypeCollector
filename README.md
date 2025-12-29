# Basketball Archetype Collector

A .NET MAUI iOS app that replicates the HTML NBA Cards Archetype game. Collect basketball player cards, open packs, and generate unique AI-powered archetype crests for each player.

## Status: PRODUCTION READY

All core features from the HTML version have been implemented and are working.

---

## Quick Start

### 1. Deploy Appwrite Functions

Before the app will work, you need to deploy the Appwrite functions:

```bash
# Install Appwrite CLI
npm install -g appwrite-cli

# Login to Appwrite
appwrite login

# Navigate to functions folder
cd appwrite/functions

# Deploy generate-archetype function
cd generate-archetype
npm install
appwrite functions createDeployment \
  --functionId="generate-archetype" \
  --entrypoint="src/main.js" \
  --code="."

# Deploy fetch-documents function
cd ../fetch-documents
npm install
appwrite functions createDeployment \
  --functionId="fetch-documents" \
  --entrypoint="src/main.js" \
  --code="."
```

### 2. Set Appwrite Function Environment Variables

In Appwrite Console, set these environment variables for `generate-archetype`:

| Variable | Description |
|----------|-------------|
| `APPWRITE_API_KEY` | API key with database/storage permissions |
| `DATABASE_ID` | `basketball-archetypes` |
| `ARCHETYPES_COLLECTION_ID` | `archetypes` |
| `CRESTS_BUCKET_ID` | `crests` |
| `OPENAI_API_KEY` | Your OpenAI API key |
| `MODELSLAB_API_KEY` | Your ModelsLab API key (optional, for cheaper images) |

For `fetch-documents`:

| Variable | Description |
|----------|-------------|
| `APPWRITE_API_KEY` | API key with database permissions |
| `DATABASE_ID` | `basketball-archetypes` |

### 3. Build and Deploy

```bash
# Build for iOS
dotnet build -f net8.0-ios -c Release

# Or use Codemagic (push to main branch)
git push origin main
```

---

## Features

### Core Gameplay
- **Pack Store** - Purchase packs with coins (Standard, Premium, Elite, Legendary)
- **Pack Opening** - Animated card reveal with rarity-based odds
- **Card Collection** - View, filter, sort, and manage your collected players
- **Player Database** - Browse all 5,527+ players ranked by overall rating
- **Statistics** - Track your collection progress and rarity breakdown
- **Archetype Generation** - AI-generated unique crests for each player

### Rarity System
| Rarity | Overall | Odds | Coin Value | Sell Value |
|--------|---------|------|------------|------------|
| GOAT | 99 | 0.5% | 2000 | 1000 |
| Legendary | 94+ | 2% | 500 | 250 |
| Epic | 88+ | 8% | 200 | 100 |
| Rare | 80+ | 15% | 75 | 37 |
| Uncommon | 72+ | 25% | 30 | 15 |
| Common | <72 | 50% | 10 | 5 |

*Note: GOAT rarity is exclusive to Michael Jordan and LeBron James*

### Pack Types
| Pack | Cost | Cards | Special |
|------|------|-------|---------|
| Standard | 100 | 5 | Basic odds |
| Premium | 250 | 5 | 2x rare chance, Uncommon+ guaranteed |
| Elite | 500 | 5 | 3x epic, 2x legendary, Rare+ guaranteed |
| Legendary | 1000 | 5 | 5x legendary, 2x GOAT, Epic+ guaranteed |

### Era Classification
| Era | Draft Year | Color |
|-----|------------|-------|
| Classic | pre-1980 | Teal |
| 80s | 1980-1989 | Orange |
| 90s | 1990-1999 | Red |
| 2000s | 2000-2009 | Purple |
| 2010s | 2010-2019 | Blue |
| Modern | 2020+ | Green |

---

## Tech Stack

- **.NET MAUI 8.0** - Cross-platform UI framework
- **Appwrite** - Backend (Auth, Database, Storage, Functions)
- **CommunityToolkit.Mvvm** - MVVM architecture
- **ModelsLab API** - AI image generation (~$0.002/image)
- **OpenAI GPT-4o-mini** - Archetype text generation
- **DALL-E 2** - Fallback image generation (~$0.016/image)

---

## Project Structure

```
BasketballArchetypeCollector/
├── Models/
│   ├── Player.cs           # Player data structure
│   ├── Rarity.cs           # Rarity enum and config
│   ├── Era.cs              # Era classification
│   ├── Pack.cs             # Pack definitions
│   ├── GameState.cs        # Game state persistence
│   ├── ArchetypeData.cs    # Archetype structure
│   └── User.cs             # User model
├── ViewModels/
│   ├── BaseViewModel.cs
│   ├── MainViewModel.cs
│   ├── PackStoreViewModel.cs
│   ├── PackOpeningViewModel.cs
│   ├── CollectionViewModel.cs
│   ├── DatabaseViewModel.cs
│   ├── StatsViewModel.cs
│   ├── PlayerDetailViewModel.cs
│   └── SignInViewModel.cs
├── Views/
│   ├── HomePage.xaml
│   ├── PackStorePage.xaml
│   ├── PackOpeningPage.xaml
│   ├── CollectionPage.xaml
│   ├── DatabasePage.xaml
│   ├── StatsPage.xaml
│   ├── PlayerDetailPage.xaml
│   └── SignInPage.xaml
├── Services/
│   ├── AppwriteService.cs  # Backend API integration
│   ├── GameStateService.cs # Local + cloud state sync
│   └── PlayerDataService.cs # CSV data loader
├── Controls/
│   ├── CoinDisplay.xaml
│   ├── PlayerCard.xaml
│   └── PackCard.xaml
├── Helpers/
│   ├── OverallCalculator.cs
│   └── Converters.cs
├── Resources/
│   ├── Data/
│   │   └── player_career_stats.csv
│   └── Fonts/
│       ├── Inter-*.ttf
│       └── Orbitron-*.ttf
└── appwrite/
    └── functions/
        ├── generate-archetype/
        │   ├── package.json
        │   └── src/main.js
        └── fetch-documents/
            ├── package.json
            └── src/main.js
```

---

## Appwrite Configuration

### Project Settings
- **Project ID:** `basketballarchetype`
- **Region:** Sydney
- **Database ID:** `basketball-archetypes`

### Collections

| Collection | Purpose |
|------------|---------|
| `players` | Player data (5,527 imported from CSV) |
| `archetypes` | Generated archetype cache |
| `user_collections` | User game state (coins, collection, stats) |
| `pack_purchases` | Purchase history |

### Storage Buckets

| Bucket | Purpose |
|--------|---------|
| `crests` | Generated crest images |

### Functions

| Function | Purpose |
|----------|---------|
| `generate-archetype` | Generates archetype using GPT-4 + ModelsLab/DALL-E |
| `fetch-documents` | Fetches all documents (bypasses 25-doc limit) |

---

## Codemagic CI/CD

### Environment Variables (Group: `io`)

| Variable | Type | Description |
|----------|------|-------------|
| `APP_STORE_CONNECT_KEY_IDENTIFIER` | Text | API Key ID |
| `APP_STORE_CONNECT_ISSUER_ID` | Text | Issuer ID |
| `APP_STORE_CONNECT_PRIVATE_KEY` | File | API Key (.p8) |
| `CERTIFICATE_PRIVATE_KEY` | File | Distribution cert (.p12) |
| `CERTIFICATE_PRIVATE_KEY_PASSWORD` | Text | Certificate password |
| `PROVISIONING_PROFILE` | File | Distribution profile |

### Build Trigger

Push to `main` branch triggers iOS build and TestFlight upload.

---

## Local Development

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or VS Code with .NET MAUI extension
- Xcode 15+ (for iOS simulator)

### Build Commands
```bash
# Restore packages
dotnet restore

# Build for iOS (Debug)
dotnet build -f net8.0-ios -c Debug

# Build for iOS (Release)
dotnet build -f net8.0-ios -c Release
```

---

## Coin Economy

| Action | Coins |
|--------|-------|
| Starting Balance | 1000 |
| Daily Bonus | +100 |
| Duplicate Card | +Full rarity value |
| Sell Card | +50% of rarity value |
| Standard Pack | -100 |
| Premium Pack | -250 |
| Elite Pack | -500 |
| Legendary Pack | -1000 |

---

## Data Persistence

### Local Storage
- Game state saved to `SecureStorage` as JSON
- Works offline
- Survives app restarts

### Cloud Sync (When Logged In)
- Syncs to Appwrite database
- Smart merge (keeps higher coin count, merges collections)
- Cross-device sync

---

## Image Generation Cost Comparison

| Provider | Cost per Image | Quality |
|----------|----------------|---------|
| ModelsLab | ~$0.002 | Good |
| DALL-E 2 (256x256) | ~$0.016 | Better |

The app uses ModelsLab first (8x cheaper), with DALL-E as fallback.

---

## Implementation Checklist

### Core Features - COMPLETE
- [x] Pack purchasing with coins
- [x] Pack opening with card reveal animation
- [x] Rarity system (6 tiers)
- [x] Card collection management
- [x] Selling cards for coins
- [x] Duplicate detection and auto-sell
- [x] Player statistics display
- [x] Era classification
- [x] Search and filter functionality
- [x] Pagination in database view

### Backend Integration - COMPLETE
- [x] Appwrite authentication (email)
- [x] Appwrite database for game state
- [x] Appwrite storage for crest images
- [x] Appwrite function for archetype generation
- [x] Local + cloud state sync
- [x] Archetype caching

### UI/UX - COMPLETE
- [x] Bottom tab navigation (4 tabs)
- [x] Pack store page
- [x] Collection page with filters
- [x] Database page with pagination
- [x] Stats page with rarity breakdown
- [x] Player detail page
- [x] Pack opening animation
- [x] Coin display component
- [x] Player card component
- [x] Rarity-based styling

### CI/CD - COMPLETE
- [x] Codemagic configuration
- [x] iOS build pipeline
- [x] TestFlight deployment

---

## Notes

- **Player Data:** 5,527 total players, 5,042 with stats
- **Bundle ID:** `com.basketballarchetype.app`
- **Target:** iOS only (initial release)
- **Minimum iOS:** 14.2

---

## License

MIT License

---

*Last Updated: December 29, 2025*
