namespace EShooting.Web.Contracts.Sessions;

/// <summary>
/// Sessiyaya xal gondermek ucun request modeli.
/// </summary>
public sealed class SubmitScoreRequest
{
    /// <summary>
    /// Raundun sira nomresi.
    /// </summary>
    public int RoundNumber { get; set; }

    /// <summary>
    /// Raund ucun xal degeri.
    /// </summary>
    public int Value { get; set; }
}
