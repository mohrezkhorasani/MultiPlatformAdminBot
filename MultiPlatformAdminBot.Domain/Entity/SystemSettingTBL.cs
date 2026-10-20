using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entity
{
    [Table("SystemSettingTBL", Schema = "BotCore")]
    public class SystemSettingTBL
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; }
        
        [MaxLength(1000)]
        public string Value { get; set; }
    }
}
