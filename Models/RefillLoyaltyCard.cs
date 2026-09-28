using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwivelWater.API.Models
{
    public class RefillLoyaltyCard
    {
        [Key]
        public Guid RefillLoyaltyCardId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        public int TickCount { get; set; } = 0;

        public int FreeRefillsAvailable { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }
    }
}