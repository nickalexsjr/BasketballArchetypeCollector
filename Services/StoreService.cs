using Plugin.InAppBilling;

namespace BasketballArchetypeCollector.Services;

/// <summary>
/// Handles in-app purchases for coin packs.
/// Uses Plugin.InAppBilling for cross-platform IAP support.
/// </summary>
public class StoreService
{
    private readonly GameStateService _gameStateService;

    // Product IDs - must match App Store Connect
    public const string CoinPack5000ProductId = "com.basketballarchetype.app.coins5000";

    // Coin amounts for each product
    private readonly Dictionary<string, int> _productCoins = new()
    {
        { CoinPack5000ProductId, 5000 }
    };

    public StoreService(GameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    /// <summary>
    /// Gets the available coin pack products with their prices.
    /// </summary>
    public async Task<List<CoinPackProduct>> GetAvailableProductsAsync()
    {
        var products = new List<CoinPackProduct>();

        try
        {
            var billing = CrossInAppBilling.Current;
            var connected = await billing.ConnectAsync();

            if (!connected)
            {
                System.Diagnostics.Debug.WriteLine("[StoreService] Failed to connect to billing");
                return products;
            }

            var productIds = _productCoins.Keys.ToArray();
            var items = await billing.GetProductInfoAsync(ItemType.InAppPurchase, productIds);

            foreach (var item in items)
            {
                if (_productCoins.TryGetValue(item.ProductId, out var coins))
                {
                    products.Add(new CoinPackProduct
                    {
                        ProductId = item.ProductId,
                        Name = item.Name,
                        Description = item.Description,
                        LocalizedPrice = item.LocalizedPrice,
                        CoinAmount = coins
                    });
                }
            }

            await billing.DisconnectAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreService] GetAvailableProducts error: {ex.Message}");
        }

        return products;
    }

    /// <summary>
    /// Purchases a coin pack and adds coins to the user's account.
    /// Returns the number of coins added, or 0 if purchase failed.
    /// </summary>
    public async Task<PurchaseResult> PurchaseCoinsAsync(string productId)
    {
        var result = new PurchaseResult();

        try
        {
            var billing = CrossInAppBilling.Current;
            var connected = await billing.ConnectAsync();

            if (!connected)
            {
                result.ErrorMessage = "Could not connect to the store. Please try again.";
                return result;
            }

            // Make the purchase
            var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);

            if (purchase == null)
            {
                result.ErrorMessage = "Purchase was cancelled.";
                await billing.DisconnectAsync();
                return result;
            }

            // Verify purchase state
            if (purchase.State == PurchaseState.Purchased)
            {
                // Consume the purchase (for consumable items like coins)
                var consumed = await billing.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken);

                if (consumed != null)
                {
                    // Add coins to user account
                    if (_productCoins.TryGetValue(productId, out var coins))
                    {
                        await _gameStateService.AddCoins(coins);
                        result.Success = true;
                        result.CoinsAdded = coins;
                        System.Diagnostics.Debug.WriteLine($"[StoreService] Successfully purchased {coins} coins");
                    }
                }
                else
                {
                    result.ErrorMessage = "Failed to complete purchase. Please contact support.";
                }
            }
            else if (purchase.State == PurchaseState.Pending)
            {
                result.ErrorMessage = "Purchase is pending. Coins will be added once payment is confirmed.";
            }
            else
            {
                result.ErrorMessage = "Purchase failed. Please try again.";
            }

            await billing.DisconnectAsync();
        }
        catch (InAppBillingPurchaseException purchaseEx)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreService] Purchase exception: {purchaseEx.PurchaseError}");
            result.ErrorMessage = purchaseEx.PurchaseError switch
            {
                PurchaseError.UserCancelled => "Purchase was cancelled.",
                PurchaseError.PaymentNotAllowed => "Payments are not allowed on this device.",
                PurchaseError.PaymentInvalid => "Payment is invalid. Please try a different payment method.",
                PurchaseError.BillingUnavailable => "Billing is not available. Please try again later.",
                PurchaseError.ProductRequestFailed => "Could not load product information.",
                PurchaseError.AppStoreUnavailable => "App Store is not available. Please try again later.",
                _ => "Purchase failed. Please try again."
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreService] Purchase error: {ex.Message}");
            result.ErrorMessage = "An error occurred. Please try again.";
        }

        return result;
    }

    /// <summary>
    /// Restores previous purchases (for non-consumable items).
    /// Not typically needed for consumable coin packs.
    /// </summary>
    public async Task<bool> RestorePurchasesAsync()
    {
        try
        {
            var billing = CrossInAppBilling.Current;
            var connected = await billing.ConnectAsync();

            if (!connected) return false;

            // For consumables, there's nothing to restore
            // This method is here for future non-consumable items

            await billing.DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreService] Restore error: {ex.Message}");
            return false;
        }
    }
}

public class CoinPackProduct
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocalizedPrice { get; set; } = string.Empty;
    public int CoinAmount { get; set; }
}

public class PurchaseResult
{
    public bool Success { get; set; }
    public int CoinsAdded { get; set; }
    public string? ErrorMessage { get; set; }
}
