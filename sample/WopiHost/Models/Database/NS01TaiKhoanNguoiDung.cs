using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WopiHost.Models.Database
{
    [Table("ns01taikhoannguoidung", Schema = "section9nhansu")]
    public class NS01TaiKhoanNguoiDung : ModelBase
    {
        [Column("manhanvien")]
        [Required]
        public string MaNhanVien { get; set; } = string.Empty;

        [Column("hoten")]
        [Required]
        public string HoTen { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("sodienthoai")]
        public string SoDienThoai { get; set; } = string.Empty;
    }
}
