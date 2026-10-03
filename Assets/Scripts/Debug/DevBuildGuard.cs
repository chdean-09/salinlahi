namespace Salinlahi.Debug
{
    /// <summary>
    /// Documents and enforces the SALIN-179 dev-build guard contract.
    /// Dev-only utilities (unlock-all, recognition test-session tools) are
    /// compiled only when UNITY_EDITOR or SALINLAHI_DEV is defined, so they
    /// cannot ship in a release candidate. See docs/release/RELEASE-PROFILE.md §6.
    /// </summary>
    public static class DevBuildGuard
    {
        /// <summary>
        /// Truth table for the <c>#if SALINLAHI_DEV || UNITY_EDITOR</c> guard.
        /// Returns true only when at least one dev-only symbol is defined.
        /// </summary>
        public static bool IsDevOnlyEnabledForSymbols(bool unityEditor, bool salinlahiDev)
            => unityEditor || salinlahiDev;
    }
}
