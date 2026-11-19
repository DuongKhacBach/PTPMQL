using System.Globalization;
using System.Text;

namespace DemoMVC.Models.Process
{
    public static class VietnameseTextHelper
    {
        public static readonly CultureInfo VietnameseCulture = new("vi-VN");
        public static readonly StringComparer VietnameseComparer = StringComparer.Create(VietnameseCulture, ignoreCase: true);

        /// <summary>
        /// Kiểm tra chuỗi nguồn có chứa từ khóa hay không (không phân biệt hoa/thường và hỗ trợ tìm cả tiếng Việt có dấu lẫn không dấu).
        /// </summary>
        public static bool ContainsMatch(string? source, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return true;
            if (string.IsNullOrWhiteSpace(source)) return false;

            var trimmedKeyword = keyword.Trim();

            // 1. So khớp chuẩn tiếng Việt không phân biệt hoa/thường
            if (VietnameseCulture.CompareInfo.IndexOf(source, trimmedKeyword, CompareOptions.IgnoreCase) >= 0)
            {
                return true;
            }

            // 2. So khớp bỏ dấu tiếng Việt (VD: gõ "nguyen" khớp "Nguyễn", "dia chat" khớp "Địa chất")
            var normalizedSource = RemoveDiacritics(source);
            var normalizedKeyword = RemoveDiacritics(trimmedKeyword);
            return normalizedSource.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tách tên chính (từ cuối cùng trong Họ và tên) để sắp xếp danh sách sinh viên chuẩn tiếng Việt A-Z / Z-A.
        /// </summary>
        public static string GetGivenName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
            var trimmed = fullName.Trim();
            int lastSpaceIndex = trimmed.LastIndexOf(' ');
            return lastSpaceIndex >= 0 ? trimmed.Substring(lastSpaceIndex + 1) : trimmed;
        }

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c switch
                    {
                        'đ' => 'd',
                        'Đ' => 'D',
                        _ => c
                    });
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
