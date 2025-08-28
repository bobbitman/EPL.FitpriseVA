using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitpriseVA.Data.Entities
{
    public class Message
    {
        [Key]
        public long Id { get; set; }


        [Required]
        public Guid ConversationId { get; set; }


        [ForeignKey(nameof(ConversationId))]
        public Conversation Conversation { get; set; } = default!;


        [Required, MaxLength(20)]
        public string Role { get; set; } = "user"; // user|assistant|tool


        [Required]
        public string Content { get; set; } = string.Empty;


        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
