using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Bulky.Models
{
    public class Company
    {
        //[Key] sets the column as the primary key
        [Key]
        public int Id { get; set; }

        [Required, DisplayName("Company Name")]
        public string Name { get; set; }


        [DisplayName("Street Address")]
        public string? StreetAddress { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        [DisplayName("Postal Code")]
        public string? PostalCode { get; set; }

        [DisplayName("Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}
