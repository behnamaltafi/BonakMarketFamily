using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ReviewVoteRequest
{
    public bool WasHelpful { get; set; }
}
