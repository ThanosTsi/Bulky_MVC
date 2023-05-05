using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }
        public string Descriptio { get; set; }

        [Required]
        public string ISBN { get; set; }

        [Required]
        public string Author { get; set; }
        [Required, DisplayName("List Price"), Range(1, 1000)]
        public double ListPrice { get; set; }

    }
}
