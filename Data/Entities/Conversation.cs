using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitpriseVA.Data.Entities
{
    public class Conversation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();


        [MaxLength(200)]
        public string Title { get; set; } = "New Chat";


        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;


        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
