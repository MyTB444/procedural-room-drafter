namespace TRV
{
    /// <summary>
    /// Tiny static coordinator for the BLOCKING UIs (pause menu / death screen / room draft /
    /// upgrade choice — NOT the map or the passive HUD): only ONE may be open at a time. A blocking
    /// UI claims the gate when it opens (<see cref="TryOpen"/> — refused while another holds it)
    /// and releases it on close, and each freezes the player itself while open.
    ///
    /// <see cref="MenuActive"/> is the pause-menu/death-screen exception: while it's true even the
    /// map (otherwise always allowed, it's non-blocking) refuses to open — the menu blocks
    /// EVERYTHING until closed.
    /// </summary>
    public static class UIGate
    {
        private static object _owner;

        /// <summary>True while the pause menu or death screen is up — blocks even the map.</summary>
        public static bool MenuActive { get; set; }

        /// <summary>A blocking UI is currently open.</summary>
        public static bool IsBlocked => _owner != null;

        /// <summary>Claim the gate. False (refuse to open) while another UI holds it.</summary>
        public static bool TryOpen(object owner)
        {
            if (_owner != null && _owner != owner) return false;
            _owner = owner;
            return true;
        }

        /// <summary>Take the gate unconditionally — the death screen turning itself on.</summary>
        public static void ForceOpen(object owner) => _owner = owner;

        /// <summary>Release the gate (no-op if <paramref name="owner"/> doesn't hold it).</summary>
        public static void Close(object owner)
        {
            if (_owner == owner) _owner = null;
        }

        /// <summary>Clear everything — statics survive scene reloads, so call on restart/startup.</summary>
        public static void Reset()
        {
            _owner = null;
            MenuActive = false;
        }
    }
}
