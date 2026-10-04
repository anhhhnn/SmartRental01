using System.ComponentModel.DataAnnotations;

namespace SmartRental.Models
{
    public class TienNghi
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string TenTienNghi { get; set; } = string.Empty;

        public ICollection<PhongTienNghi> PhongTienNghis { get; set; }
            = new List<PhongTienNghi>();
    }
}