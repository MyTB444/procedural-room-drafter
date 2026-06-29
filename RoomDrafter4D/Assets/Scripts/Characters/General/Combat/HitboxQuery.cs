using UnityEngine;

namespace TRV
{
    /// <summary>
    /// Shared spatial overlap for hitboxes: queries a <see cref="Collider2D"/>'s SHAPE (at its current
    /// transform) against a layer mask, independent of the body's collision/exclude layers (those only
    /// affect simulation contacts, not queries). Used by <see cref="AttackHitbox"/> (a child shape) and
    /// <see cref="DashHitbox"/> (the body collider) so the overlap maths lives in one place.
    /// </summary>
    public static class HitboxQuery
    {
        /// <summary>Overlap <paramref name="col"/>'s shape into <paramref name="results"/>; returns the
        /// count. <paramref name="expand"/> grows the query area (units) so a target that merely TOUCHES
        /// the collider (e.g. two solid bodies separated by collision) still registers.</summary>
        public static int Overlap(Collider2D col, LayerMask hitMask, Collider2D[] results, float expand = 0f)
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(hitMask);

            var t = col.transform;
            Vector2 scale = new Vector2(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
            float grow = Mathf.Max(0f, expand);
            Vector2 grow2 = new Vector2(grow * 2f, grow * 2f);

            switch (col)
            {
                case BoxCollider2D box:
                    return Physics2D.OverlapBox(
                        t.TransformPoint(box.offset), Vector2.Scale(box.size, scale) + grow2, t.eulerAngles.z, filter, results);
                case CircleCollider2D circle:
                    return Physics2D.OverlapCircle(
                        t.TransformPoint(circle.offset), circle.radius * Mathf.Max(scale.x, scale.y) + grow, filter, results);
                case CapsuleCollider2D cap:
                    return Physics2D.OverlapCapsule(
                        t.TransformPoint(cap.offset), Vector2.Scale(cap.size, scale) + grow2, cap.direction, t.eulerAngles.z, filter, results);
                default:
                    return Physics2D.OverlapBox(t.position, Vector2.one + grow2, t.eulerAngles.z, filter, results);
            }
        }
    }
}
