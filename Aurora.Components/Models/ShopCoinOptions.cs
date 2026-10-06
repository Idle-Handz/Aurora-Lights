namespace Aurora.Components.Models;

/// <summary>
/// How the shop makes change and pays out sales. The default (<see cref="UseElectrum"/> false) never
/// hands out electrum: many tables do not use the coin, and silently creating it is a surprise.
/// A table that does use it turns this on and gets electrum in its change and proceeds.
/// </summary>
/// <param name="UseElectrum">
/// Make change and sale proceeds in electrum where a 50 cp coin fits. Electrum a character already
/// carries is spent like any other coin whichever way this is set.
/// </param>
public readonly record struct ShopCoinOptions(bool UseElectrum);
