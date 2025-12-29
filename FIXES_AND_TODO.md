# Basketball Archetype Collector - Fixes Applied & TODO

## Current Version
- **Version**: 1.4.0 (Build 22)
- **Target Framework**: net8.0-ios (builds on Codemagic, not local due to .NET 10 RC2 SDK)
- **Last Updated**: 2025-12-30

---

## Issues Fixed (This Session - 2025-12-30)

### 1. Pack Navigation Issue - Cards Blank After Viewing Detail
**Problem**: After opening a pack, tapping a card to view details, then going back - all cards would disappear/be blank.

**Root Cause**: MAUI creates a new `PackOpeningPage` and `PackOpeningViewModel` instance when navigating back, losing the pack results stored in the previous ViewModel's `Cards` collection.

**Solution**: Changed card viewing to use a modal overlay instead of navigating to a separate page:
- Added `ShowCardDetail` and `SelectedCardForDetail` properties to `PackOpeningViewModel`
- Added `ViewCard`, `CloseCardDetail`, and `ViewFullDetail` commands
- Added card detail modal overlay to `PackOpeningPage.xaml`
- Tapping a card now shows a modal with stats; "Full Details" button navigates to full page if needed

**Files Changed**:
- `ViewModels/PackOpeningViewModel.cs` - Added modal state and commands
- `Views/PackOpeningPage.xaml` - Added modal overlay UI

### 2. pack_purchases Collection Now Used
**Problem**: `pack_purchases` collection in Appwrite was defined but never used.

**Solution**: Integrated pack purchase tracking:
- Created `Models/PackPurchase.cs` model
- Added CRUD methods to `AppwriteService.cs` for pack_purchases
- `GameStateService.OpenPack()` now records purchases to Appwrite
- Stats page shows pack purchase statistics (visible when logged in)

### 3. Account Deletion Now Works
**Problem**: "Delete Account" button only signed out, didn't actually delete data.

**Solution**: Implemented proper account deletion using Appwrite's `UpdateStatus()`:
- Deletes user's `user_collections` document
- Deletes all user's `pack_purchases` documents
- Marks account as blocked/deleted in Appwrite
- Clears local session data

### 4. Crests Bucket Documented
**Problem**: `crests` storage bucket appeared unused but purpose was unclear.

**Solution**: Added comment in `AppConfig.cs` explaining it's intentionally unused - crest images are stored as external URLs from ModelsLab/DALL-E for cost optimization.

### 5. PlayerCard Redesign - Full Rarity Color Theme
**Problem**: Cards only showed rarity color on border, not prominent enough.

**Solution**: Complete PlayerCard redesign with:
- Full rarity-tinted background color (dark variants)
- Glowing outer border with rarity color shadow
- Improved overall badge with glow effect
- Colored stats (PPG=orange, RPG=blue, APG=green)
- Cleaner rarity label at bottom
- Better crest image framing

**Files Changed**:
- `Controls/PlayerCard.xaml` - Complete UI overhaul
- `Controls/PlayerCard.xaml.cs` - Added RarityBackgroundColor, RarityLabel properties

### 6. Collection Page UI Redesign
**Problem**: Collection page looked cramped and boring.

**Solution**: Modern redesign with:
- Header with shadow and glow effects
- Search bar with search icon
- Filter pills with icons (✨ Rarity, 📅 Era)
- Better spacing and dark theme
- Improved empty state with "Open Packs" button
- Better loading overlay

**Files Changed**:
- `Views/CollectionPage.xaml` - Complete UI overhaul
- `ViewModels/CollectionViewModel.cs` - Added GoToPacksCommand

---

## Previous Fixes

### 5. Login Page Flash on App Launch
**Problem**: Users with existing sessions briefly saw the login page before being redirected to main.

**Solution**: Added a LoadingPage that shows while checking session status.
- Created `Views/LoadingPage.xaml` - Shows app logo + loading spinner
- Created `Views/LoadingPage.xaml.cs` - Contains session check logic
- Updated `MauiProgram.cs` - Registered LoadingPage
- Updated `AppShell.xaml` - Added loading route as first ShellItem
- Simplified `AppShell.xaml.cs` - Removed session check (now in LoadingPage)
- Simplified `App.xaml.cs` - Removed service injection

**Flow**: App starts → LoadingPage → checks session → navigates to `//main/packs` or `//login`

### 2. Pack Re-Opening Bug
**Problem**: After opening a pack and viewing card details, going back would re-open a new pack.

**Solution**: Added page-level flag in `PackOpeningPage.xaml.cs`:
```csharp
private bool _hasStartedOpening;

protected override async void OnAppearing()
{
    if (!_hasStartedOpening && !_viewModel.IsOpening && _viewModel.Cards.Count == 0)
    {
        _hasStartedOpening = true;
        // ... open pack
    }
}
```

### 3. Rarity-Colored Card Highlighting in Pack Results
**Problem**: Cards from packs didn't show their rarity colors prominently.

**Solution**:
- Added `RarityColor` property to `CardItem` class in `PackOpeningViewModel.cs`
- Updated `PackOpeningPage.xaml` to wrap PlayerCard in a glowing Border with rarity color

### 4. CrestsGenerated Stat Not Incrementing
**Problem**: The crestsGenerated stat wasn't being updated when new crests were generated.

**Solution**: Updated `GameStateService.cs` `CacheArchetype()` method to increment the stat:
```csharp
if (archetype.HasCrestImage)
{
    _currentState.Stats.CrestsGenerated++;
    await SaveAndSync();
}
```

---

## CRITICAL: Appwrite Configuration Issues

### FIXED: Environment Variable
**Problem**: `ARCHETYPES_COLLECTION_ID` was possibly misconfigured

**Fix Applied**: Verified/reset `ARCHETYPES_COLLECTION_ID` = `archetypes` in Appwrite Console

### FIXED: Schema Mismatch in Function
**Problem**: Function was saving `createdAt` field but collection uses auto-generated `$createdAt`

**Fix Applied**: Updated `src/main.js` to remove `createdAt` and add `imagePrompt` field instead

### KNOWN ISSUE: Storage Bucket Not Updating
**Problem**: The `crests` storage bucket shows no files even after pack opening

**Cause**: The function currently generates images via ModelsLab/DALL-E but returns external URLs - it doesn't upload to Appwrite Storage. The crestImageUrl points to the external image host.

**Future Fix**: If needed, add logic to download external image and upload to Appwrite Storage bucket.

### KNOWN ISSUE: archetypes & pack_purchases Collections Empty
**Problem**: Collections have no data rows

**Possible Causes**:
1. Function timeout (needs 120s)
2. Wrong environment variables
3. API key permissions
4. Schema mismatch (fixed above)

**Status**: Testing after function redeploy...

### Required Environment Variables for `generate-archetype` Function

| Variable | Required Value |
|----------|----------------|
| `DATABASE_ID` | `basketball-archetypes` |
| `ARCHETYPES_COLLECTION_ID` | `archetypes` ← **FIX THIS** |
| `CRESTS_BUCKET_ID` | `crests` |
| `OPENAI_API_KEY` | (your key) |
| `MODELSLAB_API_KEY` | (your key, optional) |
| `APPWRITE_API_KEY` | (key with DB write permissions) |

### Required Collections

#### 1. `archetypes` Collection
Create this collection if it doesn't exist with these attributes:

| Attribute | Type | Required | Size |
|-----------|------|----------|------|
| playerId | string | Yes | 255 |
| playerName | string | Yes | 255 |
| archetype | string | No | 255 |
| subArchetype | string | No | 255 |
| playStyleSummary | string | No | 2000 |
| confidence | string | No | 50 |
| crestImageUrl | string (URL) | No | 2000 |
| crestSeed | string | No | 50 |
| createdAt | string | No | 50 |

**Permissions**: Enable "Document Security" OR set collection-level permissions for authenticated users.

#### 2. `user_collections` Collection (Already exists - verified)
Schema is correct with all required fields.

#### 3. `pack_purchases` Collection
| Attribute | Type | Required |
|-----------|------|----------|
| userId | string | Yes |
| packId | string | Yes |
| packName | string | No |
| cost | integer | Yes |
| cardsReceived | string[] | No |
| purchasedAt | string | No |

### Required Storage Bucket

#### `crests` Bucket
- Bucket ID: `crests`
- File permissions: Allow authenticated users to read
- Max file size: 5MB

---

## Function Timeout Issue

**Problem**: "Synchronous function execution timed out. 30 seconds"

**Solution**: In Appwrite Console → Functions → generate-archetype → Settings:
- Set **Timeout** to `120` seconds (image generation takes 60-90 seconds)

---

## TODO List

### High Priority - v1.3.7 (2025-12-30)
- [x] **Fix ARCHETYPES_COLLECTION_ID env var** - Verified set to `archetypes`
- [x] **Fix schema mismatch** - Removed `createdAt`, added `imagePrompt`
- [x] **Archetypes saving** - Confirmed archetypes collection now populates!
- [x] **Collection filter fix** - Now defaults to "Owned", removed "All" option
- [x] **Optimize function** - ModelsLab first (30s timeout), DALL-E fallback
- [ ] **Redeploy generate-archetype function** - Upload new tar.gz ← DO THIS FIRST
- [ ] **Test pack opening** - Verify crest images generate without timeout
- [ ] **Verify loading page** - Should hide login flash on app launch
- [ ] **Verify pack re-opening bug** - Going back from card detail shouldn't re-open pack
- [ ] **Verify rarity glow** - Cards in pack results should have colored glow

### Medium Priority
- [ ] Create `pack_purchases` collection if tracking pack purchases is needed
- [ ] Add error handling UI for function failures (show toast instead of silent fail)
- [ ] Consider adding retry logic for failed archetype generation

### Low Priority / Future Enhancements
- [ ] Add offline mode for cached archetypes
- [ ] Implement pull-to-refresh on collection page
- [ ] Add search/filter on Database tab
- [ ] Consider upgrading to net9.0-ios when Codemagic supports it

---

## Known Issues (Active)

### 1. CRITICAL: Function Timeout at 30s Despite 180s/200s Setting
**Issue**: Function fails at exactly 30 seconds even with Appwrite timeout set to 180s/200s
**Symptoms**:
- Error: "Synchronous function execution timed out. Error Code: 408"
- No function logs appear (function may not be starting)
- ModelsLab works when it responds quickly (~11s)
**Possible Causes**:
- Appwrite may need function redeployment after timeout change
- Runtime issue
- Appwrite Cloud limitation?
**Status**: UNRESOLVED - try redeploying function after changing timeout setting

### 2. Pack Navigation Glitch
**Issue**: Opening a pack, viewing card detail, then going back - pack results disappear
**Status**: ✅ FIXED - Card details now shown in modal overlay instead of navigating away

### 3. Collection Page UI Issues
**Issue**:
- Shows "No players found" on first load (works after playing with filters)
- UI looks cramped
- Cards need better rarity visuals (make whole card the rarity color)
**Status**: ✅ FIXED - Complete UI redesign with rarity-colored cards, better spacing, modern filters

### 4. pack_purchases Collection
**Previous Status**: Was intentionally unused
**Status**: ✅ NOW USED - Pack purchases are tracked and displayed in Stats page

### 5. Storage Bucket (crests) Empty
**Cause**: Images hosted externally by ModelsLab/DALL-E
**Status**: Working as designed

---

## UI Improvements Needed

### Collection Page
- [ ] Fix cramped layout
- [ ] Make cards show full rarity color (not just border)
- [ ] Fix first-load "no players found" issue

### Pack Opening Page
- [ ] Better rarity highlighting on cards
- [ ] Fix navigation back issue

---

## Build Notes

### Local Build Issue
Local machine has .NET 10 RC2 SDK which doesn't have net8.0-ios workloads. Build fails locally with:
```
error NETSDK1135: SupportedOSPlatformVersion 15.0 cannot be higher than TargetPlatformVersion 1.0
```

**Solution**: Build on Codemagic (or install .NET 8 SDK locally alongside .NET 10)

### Codemagic Build
- Uses net8.0-ios target framework
- Do NOT upgrade to net9.0-ios (fails on Codemagic)

---

## File Changes Summary

| File | Change |
|------|--------|
| `MauiProgram.cs` | Added LoadingPage registration |
| `App.xaml.cs` | Simplified constructor |
| `AppShell.xaml` | Already had loading route |
| `AppShell.xaml.cs` | Removed session check logic |
| `Views/LoadingPage.xaml` | Created - loading UI |
| `Views/LoadingPage.xaml.cs` | Created - session check logic |
| `Views/PackOpeningPage.xaml` | Added rarity glow border |
| `Views/PackOpeningPage.xaml.cs` | Already had page-level flag |
| `ViewModels/PackOpeningViewModel.cs` | Added RarityColor to CardItem |
| `Services/GameStateService.cs` | Increment CrestsGenerated on cache |
| `BasketballArchetypeCollector.csproj` | Version 1.3.6, Build 19 |

---

## Testing Checklist

After applying Appwrite fixes:
- [ ] Fresh app install shows loading page, then login (no flash)
- [ ] Existing session shows loading page, then main tabs
- [ ] Opening a pack generates archetypes (check Appwrite archetypes collection)
- [ ] Pack cards show rarity-colored glow borders
- [ ] Navigating to card detail and back doesn't re-open pack
- [ ] Stats page shows correct crestsGenerated count
- [ ] User collections document updates with stats

---

*Last Updated: 2025-12-30*
