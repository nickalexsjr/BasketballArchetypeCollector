# Basketball Archetype Collector - Environment Setup Guide

## App Configuration

### AppConfig.cs Setup

Copy `AppConfig.template.cs` to `AppConfig.cs` and fill in your values:

```csharp
public static class AppConfig
{
    public const string AppwriteEndpoint = "https://cloud.appwrite.io/v1";
    public const string AppwriteProjectId = "YOUR_PROJECT_ID";

    // Collection IDs (create these in Appwrite Console)
    public const string DatabaseId = "basketball-archetypes";
    public const string PlayersCollectionId = "players";
    public const string ArchetypesCollectionId = "archetypes";
    public const string UserCollectionsId = "user_collections";
    public const string PackPurchasesId = "pack_purchases";

    // Storage Bucket IDs
    public const string CrestsBucketId = "crests";

    // Function IDs
    public const string FetchDocumentsFunctionId = "fetch-documents";
    public const string GenerateArchetypeFunctionId = "generate-archetype";
}
```

---

## Appwrite Console Setup

### 1. Create Project
1. Go to https://cloud.appwrite.io
2. Create new project named "Basketball Archetype Collector"
3. Select **Sydney** region (or closest to your users)
4. Note your **Project ID**

### 2. Enable Authentication
1. Go to **Auth** > **Settings**
2. Enable **Email/Password** authentication
3. Optionally configure email verification settings

### 3. Create Database
1. Go to **Databases** > **Create Database**
2. Name: `basketball-archetypes`
3. Note the **Database ID**

### 4. Create Collections

#### Collection: `players`
| Attribute | Type | Required | Array | Notes |
|-----------|------|----------|-------|-------|
| name | String (255) | Yes | No | Player name |
| era | String (50) | Yes | No | Classic, 80s, 90s, 2000s, 2010s, Modern |
| rarity | String (50) | Yes | No | Common, Uncommon, Rare, Epic, Legendary, GOAT |
| ppg | Float | No | No | Points per game |
| rpg | Float | No | No | Rebounds per game |
| apg | Float | No | No | Assists per game |
| spg | Float | No | No | Steals per game |
| bpg | Float | No | No | Blocks per game |
| imageUrl | String (2000) | No | No | Player image URL |

**Permissions:** Any (Read), Users (Create, Update, Delete)

#### Collection: `archetypes`
| Attribute | Type | Required | Array | Notes |
|-----------|------|----------|-------|-------|
| playerId | String (255) | Yes | No | Reference to player |
| playerName | String (255) | Yes | No | Denormalized player name |
| archetype | String (255) | Yes | No | e.g., "Scoring Machine" |
| subArchetype | String (255) | No | No | e.g., "Mid-Range Assassin" |
| playStyleSummary | String (2000) | No | No | Description of play style |
| confidence | String (50) | No | No | high, medium, low |
| crestImageUrl | String (2000) | No | No | DALL-E generated crest URL |
| crestSeed | String (50) | No | No | Stable seed for regeneration |
| createdAt | DateTime | No | No | When archetype was generated |

**Permissions:** Any (Read), Users (Create, Update)

#### Collection: `user_collections`
| Attribute | Type | Required | Array | Notes |
|-----------|------|----------|-------|-------|
| userId | String (255) | Yes | No | Appwrite user ID |
| playerId | String (255) | Yes | No | Reference to player |
| acquiredAt | DateTime | Yes | No | When player was acquired |
| packId | String (255) | No | No | Which pack it came from |

**Permissions:** Users (Create, Read, Update, Delete) - Document-level security

#### Collection: `pack_purchases`
| Attribute | Type | Required | Array | Notes |
|-----------|------|----------|-------|-------|
| userId | String (255) | Yes | No | Appwrite user ID |
| packId | String (255) | Yes | No | Which pack type |
| cost | Integer | Yes | No | Coins spent |
| purchasedAt | DateTime | Yes | No | Purchase timestamp |
| playersReceived | String (5000) | No | No | JSON array of player IDs |

**Permissions:** Users (Create, Read)

### 5. Create Storage Bucket

#### Bucket: `crests`
1. Go to **Storage** > **Create Bucket**
2. Name: `crests`
3. Permissions: Any (Read), Users (Create)
4. Allowed file extensions: jpg, jpeg, png, webp
5. Max file size: 5MB

### 6. Create Functions

#### Function: `fetch-documents`
1. Go to **Functions** > **Create Function**
2. Name: `fetch-documents`
3. Runtime: **Node.js 18.0**
4. Entrypoint: `src/main.js`
5. Upload `fetch-documents.tar.gz`

**Environment Variables:**
| Key | Value |
|-----|-------|
| DATABASE_ID | `basketball-archetypes` |

#### Function: `generate-archetype`
1. Go to **Functions** > **Create Function**
2. Name: `generate-archetype`
3. Runtime: **Node.js 18.0**
4. Entrypoint: `src/main.js`
5. Upload `generate-archetype.tar.gz`

**Environment Variables:**
| Key | Value |
|-----|-------|
| DATABASE_ID | `basketball-archetypes` |
| ARCHETYPES_COLLECTION_ID | `archetypes` |
| CRESTS_BUCKET_ID | `crests` |
| OPENAI_API_KEY | `sk-...` (your OpenAI API key) |

### 7. Generate API Key
1. Go to **Overview** > **API Keys** > **Create API Key**
2. Name: `iOS App Key`
3. Scopes: Select all Database and Storage scopes
4. Note the **API Key** (you'll need it for functions)

---

## Codemagic Setup

### 1. Connect Repository
1. Go to https://codemagic.io
2. Add your GitHub repository
3. Select **.NET MAUI** workflow

### 2. Environment Variables

Add these variables in Codemagic **Environment Variables** section:

#### App Configuration
| Variable | Value | Group |
|----------|-------|-------|
| `APPWRITE_ENDPOINT` | `https://cloud.appwrite.io/v1` | appwrite |
| `APPWRITE_PROJECT_ID` | Your project ID | appwrite |
| `APPWRITE_DATABASE_ID` | `basketball-archetypes` | appwrite |
| `APPWRITE_PLAYERS_COLLECTION` | `players` | appwrite |
| `APPWRITE_ARCHETYPES_COLLECTION` | `archetypes` | appwrite |
| `APPWRITE_USER_COLLECTIONS` | `user_collections` | appwrite |
| `APPWRITE_PACK_PURCHASES` | `pack_purchases` | appwrite |
| `APPWRITE_CRESTS_BUCKET` | `crests` | appwrite |
| `APPWRITE_FETCH_FUNCTION` | `fetch-documents` | appwrite |
| `APPWRITE_ARCHETYPE_FUNCTION` | `generate-archetype` | appwrite |

#### iOS Signing (same as BragStack/SwishPot)
| Variable | Value | Group |
|----------|-------|-------|
| `APP_STORE_CONNECT_KEY_IDENTIFIER` | Your key ID | ios_credentials |
| `APP_STORE_CONNECT_ISSUER_ID` | Your issuer ID | ios_credentials |
| `APP_STORE_CONNECT_PRIVATE_KEY` | Base64 .p8 key | ios_credentials |
| `CERTIFICATE_PRIVATE_KEY` | Base64 .p12 contents | ios_credentials |
| `CERTIFICATE_PRIVATE_KEY_PASSWORD` | .p12 password | ios_credentials |
| `PROVISIONING_PROFILE` | Base64 .mobileprovision | ios_credentials |

### 3. Bundle ID
Make sure your Apple Developer account has:
- App ID: `com.basketballarchetype.app`
- Provisioning Profile for this bundle ID

### 4. codemagic.yaml

The build configuration should already be in your project. Key settings:
- Target framework: `net9.0-ios`
- Build configuration: `Release`
- Signing style: Manual with provisioning profile

---

## OpenAI Setup

### 1. Get API Key
1. Go to https://platform.openai.com
2. Go to API Keys > Create new secret key
3. Copy the key (starts with `sk-`)

### 2. Models Used
- **GPT-4o-mini**: For archetype generation (cost-effective)
- **DALL-E 2**: For crest image generation (256x256)

### 3. Estimated Costs
- GPT-4o-mini: ~$0.001 per archetype
- DALL-E 2 (256x256): ~$0.016 per image
- Total per player archetype: ~$0.02

---

## Quick Start Checklist

- [ ] Create Appwrite project in Sydney region
- [ ] Enable Email/Password auth
- [ ] Create database `basketball-archetypes`
- [ ] Create all 4 collections with correct attributes
- [ ] Create `crests` storage bucket
- [ ] Deploy `fetch-documents` function
- [ ] Deploy `generate-archetype` function with OpenAI key
- [ ] Generate API key for functions
- [ ] Copy `AppConfig.template.cs` to `AppConfig.cs`
- [ ] Fill in all IDs in `AppConfig.cs`
- [ ] Set up Codemagic with iOS signing credentials
- [ ] Add Appwrite environment variables to Codemagic
- [ ] Import player data to `players` collection (use Kaggle NBA dataset)

---

## Importing Player Data

You can import player data using the Appwrite Console's import feature or via the SDK:

```javascript
// Example: Import players from CSV/JSON
const players = [
    {
        name: "Michael Jordan",
        era: "90s",
        rarity: "GOAT",
        ppg: 30.1,
        rpg: 6.2,
        apg: 5.3,
        spg: 2.3,
        bpg: 0.8
    },
    // ... more players
];

for (const player of players) {
    await databases.createDocument(
        'basketball-archetypes',
        'players',
        ID.unique(),
        player
    );
}
```

The NBA dataset from Kaggle contains ~4,500 players with career stats.
