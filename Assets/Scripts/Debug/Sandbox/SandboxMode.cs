namespace Salinlahi.Debug.Sandbox
{
    public static class SandboxMode
    {
        private static bool _isActive;
        private static float _movementSpeedScale = 1f;
        private static bool _movementPaused;

#if UNITY_INCLUDE_TESTS
        private static bool? _availabilityOverride;
#endif

        public static bool IsAvailable
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (_availabilityOverride.HasValue)
                    return _availabilityOverride.Value;
#endif

#if UNITY_EDITOR || SALINLAHI_SANDBOX
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsActive => IsAvailable && _isActive;

        public static bool ShouldBypassLifeLoss => IsActive;

        public static bool IsMovementPaused => IsActive && _movementPaused;

        public static float MovementSpeedScale => IsActive ? _movementSpeedScale : 1f;

        public static bool IsAvailableForSymbols(bool unityEditor, bool salinlahiSandbox)
        {
            return unityEditor || salinlahiSandbox;
        }

        public static bool TryActivate()
        {
            if (!IsAvailable)
            {
                _isActive = false;
                return false;
            }

            _isActive = true;
            _movementSpeedScale = 1f;
            _movementPaused = false;
            return true;
        }

        public static void Deactivate()
        {
            _isActive = false;
            _movementSpeedScale = 1f;
            _movementPaused = false;
        }

        public static void SetMovementSpeedScale(float scale)
        {
            _movementSpeedScale = UnityEngine.Mathf.Clamp(scale, 0.05f, 2f);
        }

        public static void SetMovementPaused(bool paused)
        {
            _movementPaused = paused;
        }

        public static void ToggleMovementPaused()
        {
            _movementPaused = !_movementPaused;
        }

#if UNITY_INCLUDE_TESTS
        public static void SetAvailabilityOverrideForTests(bool? isAvailable)
        {
            _availabilityOverride = isAvailable;
            if (isAvailable == false)
                _isActive = false;
        }
#endif
    }
}
