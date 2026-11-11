using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>Keeps the stand floating behind its owner, facing the same way, with a gentle bob.</summary>
    internal class StandFollower : MonoBehaviour
    {
        public Player Owner;
        public Vector3 Offset = new Vector3(0f, 0.2f, -1.2f); // local to the player: behind
        public float Smooth = 10f;

        private void Update()
        {
            if (Owner == null) { Destroy(gameObject); return; }
            var t = Owner.transform;
            Vector3 target = t.TransformPoint(Offset) + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.05f;
            transform.position = Vector3.Lerp(transform.position, target, Smooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, t.rotation, Smooth * Time.deltaTime);
        }
    }
}
