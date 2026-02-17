using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Moves a trail prefab (with a TrailRenderer) from a start position to an end position
    /// at a given speed so the TrailRenderer draws behind it as it travels.
    ///
    /// On spawn, sets _SpawnTime on the TrailRenderer's material via a MaterialPropertyBlock
    /// so each trail instance fades independently (the shader reads _SpawnTime per-instance,
    /// not from the shared material). This means rapid fire won't reset older trails' fade.
    ///
    /// Once it reaches the end point, it waits for the fade duration + trail time before
    /// destroying itself.
    ///
    /// Added via code by RangedWeapon.SpawnBulletTrail() — not placed manually in scenes.
    /// </summary>
    public class BulletTrailMover : MonoBehaviour
    {
        private static readonly int SpawnTimeID = Shader.PropertyToID("_SpawnTime");

        private Vector3 _start;
        private Vector3 _end;
        private float _speed;
        private float _totalDistance;
        private float _distanceTravelled;
        private bool _reachedEnd;
        private TrailRenderer _trail;

        /// <summary>
        /// Called by RangedWeapon immediately after instantiation.
        /// Sets up the travel path, speed, and per-instance spawn time on the shader.
        /// </summary>
        public void Initialise(Vector3 start, Vector3 end, float speed)
        {
            _start = start;
            _end = end;
            _speed = Mathf.Max(speed, 1f);
            _totalDistance = Vector3.Distance(start, end);
            _distanceTravelled = 0f;
            _reachedEnd = false;

            transform.position = start;
            _trail = GetComponentInChildren<TrailRenderer>();

            // Set _SpawnTime per-instance so each trail fades independently
            if (_trail != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                _trail.GetPropertyBlock(block);
                block.SetFloat(SpawnTimeID, Time.time);
                _trail.SetPropertyBlock(block);
            }
        }

        private void Update()
        {
            if (_reachedEnd) return;

            _distanceTravelled += _speed * Time.deltaTime;

            if (_distanceTravelled >= _totalDistance)
            {
                transform.position = _end;
                _reachedEnd = true;

                // Destroy after the trail has fully faded.
                // trail.time = how long trail segments live,
                // plus a buffer for the shader's _FadeDuration (read from material).
                float trailTime = _trail != null ? _trail.time : 0f;
                float fadeDuration = _trail != null ? _trail.sharedMaterial.GetFloat("_FadeDuration") : 0.5f;
                Destroy(gameObject, trailTime + fadeDuration + 0.1f);
                return;
            }

            float t = _distanceTravelled / _totalDistance;
            transform.position = Vector3.Lerp(_start, _end, t);
        }
    }
}
