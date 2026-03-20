namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight projection of <see cref="BsDataCostTier"/> used by the
/// <c>/unitsList</c> endpoint.  Only the fields actually consumed by the
/// client are included; <c>Id</c> and <c>UnitId</c> are omitted to
/// reduce payload size.
/// </summary>
public class BsDataCostTierSlim
{
    /// <summary>Minimum number of models in this cost tier.</summary>
    public int? MinModels { get; set; }
    /// <summary>Maximum number of models in this cost tier (null if unbounded).</summary>
    public int? MaxModels { get; set; }
    /// <summary>Points cost for this tier.</summary>
    public decimal Points { get; set; }
}
