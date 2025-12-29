# Basketball Archetype Collector - Fixes Applied & TODO

## Current Version
- **Version**: 1.3.7 (Build 20)
- **Target Framework**: net8.0-ios (builds on Codemagic, not local due to .NET 10 RC2 SDK)
- **Last Updated**: 2025-12-30

---

## Issues Fixed (This Session)

### 1. Login Page Flash on App Launch
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

## Known Issues (To Test Tomorrow)

### 1. Function Timeout (Previously 30s limit)
**Status**: Should be fixed - function timeout set to 180s in Appwrite
**New Logic**:
- ModelsLab first (30s max) → DALL-E fallback (~15-20s)
- Total max: ~60-70s, well within 180s limit

### 2. Pack Navigation Glitch
**Issue**: Clicking back from card detail didn't show the pack just opened
**Possible Cause**: May be related to Appwrite function issues or page navigation
**Status**: Need to test after function fix

### 3. Collection Page Shows "No Players Found"
**Issue**: Collection was defaulting to show all players, not owned
**Fix Applied**: Changed default filter to "Owned", removed "All" option
**Status**: Need to verify fix works

### 4. pack_purchases Collection Empty
**Issue**: No rows in pack_purchases collection
**Cause**: Collection defined but intentionally not used - all needed data is in `user_collections`
**Status**: NOT A BUG - can delete this collection from Appwrite if desired

### 5. Storage Bucket (crests) Empty
**Issue**: No files in crests storage bucket
**Cause**: Function uses external image URLs (ModelsLab/DALL-E hosted), doesn't upload to Appwrite Storage
**Status**: Working as designed - images are hosted externally

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
