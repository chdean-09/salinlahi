using System.Text.RegularExpressions;

public static class TemplateVariantNameUtility
{
    private static readonly Regex s_variantPattern = new Regex(@"^(?<id>[A-Z][A-Z-]*)_TEMPLATE_(?<variant>\d+)$", RegexOptions.Compiled);

    public static bool TryExtractCharacterID(string templateName, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(templateName))
            return false;

        Match match = s_variantPattern.Match(templateName.ToUpperInvariant().Trim());
        if (!match.Success)
            return false;

        id = BaybayinIdCanonicalizer.Canonicalize(match.Groups["id"].Value);
        return !string.IsNullOrEmpty(id);
    }
}
