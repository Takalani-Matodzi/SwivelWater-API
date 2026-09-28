using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwivelWater.API.Models
{
    public class RefillLoyaltyTransaction
    {
        [Key]
        public Guid RefillLoyaltyTransactionId { get; set; }

        [Required]
        public Guid RefillLoyaltyCardId { get; set; }

        [Required]
        public Guid OrderId { get; set; }

        public Guid? OrderItemId { get; set; }

        [Required]
        public string TransactionType { get; set; } = string.Empty;

        public int Litres { get; set; } = 5;

        public int TicksAdded { get; set; } = 0;

        public int FreeRefillsAdded { get; set; } = 0;

        public int FreeRefillsUsed { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RefillLoyaltyCardId))]
        public RefillLoyaltyCard? RefillLoyaltyCard { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [ForeignKey(nameof(OrderItemId))]
        public OrderItem? OrderItem { get; set; }
    }
}