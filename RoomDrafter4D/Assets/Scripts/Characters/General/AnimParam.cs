using UnityEngine;

namespace TRV
{
    /// <summary>
    /// One Animator parameter: caches its hash and whether the controller actually defines it, so
    /// every setter is a safe no-op (no warning spam) when the parameter is absent. Shared by the
    /// character → Animator bridges (TRVAnimator, EnemyAnimator).
    /// </summary>
    public readonly struct AnimParam
    {
        private readonly Animator _animator;
        private readonly int _hash;
        private readonly bool _exists;

        public AnimParam(Animator animator, string name)
        {
            _animator = animator;
            _hash = Animator.StringToHash(name);
            _exists = false;
            if (animator != null)
            {
                foreach (var p in animator.parameters)
                {
                    if (p.name == name) { _exists = true; break; }
                }
            }
        }

        public void SetFloat(float value) { if (_exists) _animator.SetFloat(_hash, value); }
        public void SetBool(bool value) { if (_exists) _animator.SetBool(_hash, value); }
        public void SetTrigger() { if (_exists) _animator.SetTrigger(_hash); }
    }
}
