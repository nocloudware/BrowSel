using System.Text.RegularExpressions;

static class UpdateParsing
{
    internal static Version? ParseVersion(string? tag)
    {
        if (tag == null) return null;
        var parts = tag.TrimStart('v', 'V').Split('.');
        var nums = new List<int>();
        foreach (var p in parts.Take(3))
        {
            var digits = new string(p.TakeWhile(char.IsAsciiDigit).ToArray());
            if (!int.TryParse(digits, out var n)) break;
            nums.Add(n);
        }
        return nums.Count == 0 ? null : new Version(nums[0], nums.Count > 1 ? nums[1] : 0, nums.Count > 2 ? nums[2] : 0);
    }

    // Accepts "<hex>" or "<hex>  <filename>" (sha256sum / Get-FileHash output). Lowercase hex or null.
    internal static string? ParseHash(string? text)
    {
        var first = text?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first != null && Regex.IsMatch(first, "^[0-9a-fA-F]{64}$") ? first.ToLowerInvariant() : null;
    }
}
