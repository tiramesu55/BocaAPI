using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Boca.API.Entities
{
    public class Ingredient
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id {get;set;}
        [Required]
        [MaxLength(400, ErrorMessage = "Ingredient Definition should be less than 200 chars")]
        public string Definition { get; set; }

        //[ForeignKey("RecepieId")]
        //public Recepie? Recepie { get; set; }

        public int RecepieId { get; set; }
        public Ingredient( string definition)
        {
            Definition = definition;
        }
    }
}