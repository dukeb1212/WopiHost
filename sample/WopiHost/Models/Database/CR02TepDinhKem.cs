using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WopiHost.Models.Database;

[Table("cr02tepdinhkem", Schema = "section0")]
public class CR02TepDinhKem : ModelBase
{
    [Column("iddoituong")]
    public int? IdDoiTuong { get; set; }

    [Column("tenbang")]
    public string TenBang { get; set; } = string.Empty;

    [Column("remotepath")]
    public string RemotePath { get; set; } = string.Empty;

    [Column("filename")]
    [Required]
    public string FileName { get; set; } = string.Empty;

    [Column("fileextension")]
    [Required]
    public string FileExtension { get; set; } = string.Empty;

    [Column("sizeinbytes")]
    public long SizeInBytes { get; set; } = 0;

    [Column("mimetype")]
    public string MimeType { get; set; } = string.Empty;

    [Column("filecategory")]
    public string FileCategory { get; set; } = string.Empty;

    public bool IsOfficeDocument => IsWordDocument || IsExcelDocument || IsPowerPointDocument;

    public bool IsWordDocument => FileExtension.ToLowerInvariant() switch
    {
        ".doc" or ".docx" or ".docm" or ".dot" or ".dotx" or ".dotm" => true,
        _ => false
    };

    public bool IsExcelDocument => FileExtension.ToLowerInvariant() switch
    {
        ".xls" or ".xlsx" or ".xlsm" or ".xlt" or ".xltx" or ".xltm" or ".csv" => true,
        _ => false
    };

    public bool IsPowerPointDocument => FileExtension.ToLowerInvariant() switch
    {
        ".ppt" or ".pptx" or ".pptm" or ".pot" or ".potx" or ".potm" => true,
        _ => false
    };
}
