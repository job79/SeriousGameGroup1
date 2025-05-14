using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace FishGame
{
    public class PlayerController : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public Sprite suckingSprite;
        public Sprite normalSprite;
        
        public float speed = 3f;
        public float arcHeight = 1.5f; // Controls the height of the parabolic motion
        public float smoothTime = 0.5f; // Controls how smooth the movement is
        public Vector2 viewportPadding = new Vector2(0.1f, 0.1f); // Padding from viewport edges
        public float minFlakeDistance = 3.0f; // Minimum safe distance from flakes
        public float timeBeforeMoveAway = 1.5f;
        
        private GameObject _flake;
        private bool _isCollideWithFlake = false;
        private float _timeWaiting = 0f;
        private Vector3 _randomTargetPosition;
        private bool _hasRandomTarget = false;
        
        // Variables for smooth movement
        private Vector3 _currentVelocity = Vector3.zero;
        private float _moveProgress = 0f;
        private bool _isMoving = false;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            _flake = GameObject.FindGameObjectWithTag("FlakeObject");
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Update is called once per frame
        private void FixedUpdate()
        {
            // Make sure we have a reference to the flake
            if (!_flake)
            {
                _flake = GameObject.FindGameObjectWithTag("FlakeObject");
                if (!_flake) return; // Exit if no flake found
            }
        
            if (!_isCollideWithFlake)
            {
                MoveToFlakes();
            }
            else
            {
                // Set the sprite of the fish to the sucking sprite
                spriteRenderer.sprite = suckingSprite;
                
                _timeWaiting += Time.deltaTime;
                if (!(_timeWaiting >= timeBeforeMoveAway)) return;
                BounceOffFlake();
            }
        }

        private void MoveToFlakes()
        {
            if (!_flake) return;
            
            // Reset random target flag when moving to flakes
            _hasRandomTarget = false;
            
            // Get direction to flake
            var direction = _flake.transform.position - transform.position;
            var distance = direction.magnitude;
            
            // If we're too close to the flake, don't move any closer
            if (distance <= minFlakeDistance)
            {
                // Just hover at current position
                _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 2f);
                SetPlayerRotation(_flake.transform.position);
                return;
            }
            
            // Calculate target position that's a safe distance from the flake
            var safeTarget = _flake.transform.position;
            if (distance < minFlakeDistance + 0.1f)
            {
                // Adjust target to be at the minimum safe distance
                safeTarget = _flake.transform.position - direction.normalized * minFlakeDistance;
            }
            
            // Calculate parabolic path
            var nextPosition = CalculateParabolicPosition(
                transform.position, 
                safeTarget, 
                arcHeight * Mathf.Min(1.0f, distance / 10f)); // Scale arc height by distance
            
            // Smooth movement with acceleration/deceleration
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                nextPosition, 
                ref _currentVelocity, 
                smoothTime, 
                speed);
            
            SetPlayerRotation(safeTarget);
        }

        private void BounceOffFlake()
        {
            if (!_flake) return;
            
            // Generate a random position if we don't have one
            if (!_hasRandomTarget)
            {
                _randomTargetPosition = GetSafeRandomPosition();
                _hasRandomTarget = true;
                _currentVelocity = Vector3.zero; // Reset velocity for smooth start
            }
            
            spriteRenderer.sprite = normalSprite;
            
            // Calculate parabolic path to random position
            var nextPosition = CalculateParabolicPosition(
                transform.position, 
                _randomTargetPosition, 
                arcHeight * 1.5f); // Higher arc for random movement
            
            // Move along parabolic path with smooth damping
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                nextPosition, 
                ref _currentVelocity, 
                smoothTime * 0.8f, // Faster movement
                speed * 1.2f);     // Slightly increased speed
            
            // Point in the direction we're moving
            SetPlayerRotation(_randomTargetPosition);

            // If we've reached the random position or moved far enough from the flake, reset
            var distanceToTarget = Vector3.Distance(transform.position, _randomTargetPosition);
            if (!(distanceToTarget < 1.5f) &&
                !(Vector3.Distance(transform.position, _flake.transform.position) > 15f)) return;
            _isCollideWithFlake = false;
            _hasRandomTarget = false;
            _currentVelocity = Vector3.zero; // Reset velocity for smooth transition
        }
        
        private Vector3 GetSafeRandomPosition()
        {
            Vector3 randomPos;
            var safetyCounter = 0; // Prevent infinite loops
            
            do {
                randomPos = GetRandomViewportPosition();
                safetyCounter++;
            } while (IsTooCloseToFlake(randomPos) && safetyCounter < 20);
            
            // If we couldn't find a good position after 20 tries, just make sure it's
            // at least the minimum distance away from the flake
            if (!IsTooCloseToFlake(randomPos)) return randomPos;
            var directionFromFlake = (randomPos - _flake.transform.position).normalized;
            randomPos = _flake.transform.position + directionFromFlake * (minFlakeDistance * 1.5f);

            return randomPos;
        }
        
        private bool IsTooCloseToFlake(Vector3 position)
        {
            if (!_flake) return false;
            
            // Check if the position is too close to any flake
            return Vector3.Distance(position, _flake.transform.position) < minFlakeDistance * 1.5f;
        }
        
        private Vector3 GetRandomViewportPosition()
        {
            // Get a random position within the viewport, respecting padding
            var randomX = Random.Range(viewportPadding.x, 1f - viewportPadding.x);
            var randomY = Random.Range(viewportPadding.y, 1f - viewportPadding.y);
            
            // Convert viewport position to world position
            var randomWorldPosition = Camera.main.ViewportToWorldPoint(new Vector3(randomX, randomY, 0));
            
            // Keep the Z position the same as the current position
            randomWorldPosition.z = transform.position.z;
            
            return randomWorldPosition;
        }
        
        private Vector3 CalculateParabolicPosition(Vector3 start, Vector3 end, float height)
        {
            // Calculate a point along a parabolic path from start to end
            var normalizedDistance = Vector3.Distance(transform.position, end) / Vector3.Distance(start, end);
            normalizedDistance = Mathf.Clamp01(normalizedDistance);
            
            // Inverse the value to make progress from 1 to 0 (start to end)
            normalizedDistance = 1f - normalizedDistance;
            
            // Calculate height at this point using parabola formula: 4*h*x*(1-x)
            // This creates a parabolic arc with max height in the middle
            var verticalOffset = 4f * height * normalizedDistance * (1f - normalizedDistance);
            
            // Create direct position first
            var directPosition = Vector3.Lerp(start, end, 1f - normalizedDistance);
            
            // Add vertical offset in world space
            directPosition.y += verticalOffset;
            
            return directPosition;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!other.gameObject.CompareTag("FlakeObject")) return;
            _isCollideWithFlake = true;
            _timeWaiting = 0f; // Reset waiting time when newly colliding
            _currentVelocity = Vector3.zero; // Reset velocity for smooth transition
        }

        private void SetPlayerRotation(Vector3 targetPosition)
        {
            var direction = targetPosition - transform.position;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // Smooth rotation
            var targetRotation = Quaternion.Euler(new Vector3(0, 0, angle + 180));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
