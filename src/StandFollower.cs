using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Controls Star Platinum's movement, idle hovering over the player's shoulder,
    /// defensive shield positioning when blocking, and executing the ORA punch barrage.
    /// </summary>
    internal class StandFollower : MonoBehaviour
    {
        public Player Owner;

        // Limb transforms
        public Transform FistL;
        public Transform FistR;
        public Transform ArmL;
        public Transform ArmR;
        public Transform LegL;
        public Transform LegR;
        public Transform Torso;
        public Transform Head;
        public Light Aura;
        public AudioSource StandAudio;

        // Base local transforms for resting pose
        private Vector3 _baseFistL;
        private Vector3 _baseFistR;
        private Vector3 _baseArmL;
        private Vector3 _baseArmR;

        // Positioning offsets
        public Vector3 IdleOffset   = new Vector3(0.55f, 0.35f, -0.80f); // floating over right shoulder
        public Vector3 AttackOffset = new Vector3(0.40f, 0.15f, 0.45f);  // lunging forward beside player
        public Vector3 BlockOffset  = new Vector3(0.00f, 0.18f, 0.70f);  // hovering right in front of player as shield
        public float Smooth = 12f;

        // Attack state
        public bool IsAttacking { get; private set; }
        private float _attackDuration = 1.3f;
        private float _attackTimer = 0f;
        private float _punchAnimTime = 0f;
        private float _nextPunchDamageTime = 0f;
        private float _staminaCost = 15f;
        private float _cooldownTimer = 0f;

        // Block reaction state
        private float _blockReactionTimer = 0f;
        private bool _isParryReaction = false;

        private void Start()
        {
            if (FistL != null) _baseFistL = FistL.localPosition;
            if (FistR != null) _baseFistR = FistR.localPosition;
            if (ArmL != null) _baseArmL = ArmL.localPosition;
            if (ArmR != null) _baseArmR = ArmR.localPosition;
        }

        private void Update()
        {
            if (Owner == null || Owner.IsDead())
            {
                Destroy(gameObject);
                return;
            }

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_blockReactionTimer > 0f) _blockReactionTimer -= Time.deltaTime;

            // Secondary attack input check for local player (hands must be free)
            if (Owner == Player.m_localPlayer)
            {
                bool secInput = ZInput.GetButton("SecondaryAttack") || ZInput.GetButton("JoySecondaryAttack") ||
                                ZInput.GetButtonDown("SecondaryAttack") || ZInput.GetButtonDown("JoySecondaryAttack");

                if (secInput && !IsAttacking && StandManager.AreHandsFree(Owner))
                {
                    TryStartAttack();
                }
            }

            if (IsAttacking)
            {
                if (!StandManager.AreHandsFree(Owner))
                {
                    StopAttack();
                    UpdateFollow();
                }
                else
                {
                    UpdateAttack();
                }
            }
            else if (Owner.IsBlocking() && StandManager.AreHandsFree(Owner))
            {
                UpdateBlockDefend();
            }
            else
            {
                UpdateFollow();
            }
        }

        public bool TryStartAttack()
        {
            if (Owner == null || IsAttacking || !StandManager.AreHandsFree(Owner)) return false;
            if (_cooldownTimer > 0f) return false;

            // Stamina check
            if (Owner.HaveStamina(_staminaCost))
            {
                Owner.UseStamina(_staminaCost);
            }
            else
            {
                if (Hud.instance != null) Hud.instance.StaminaBarEmptyFlash();
                return false;
            }

            IsAttacking = true;
            _attackTimer = _attackDuration;
            _punchAnimTime = 0f;
            _nextPunchDamageTime = 0f;

            // Play ORA barrage sound
            if (StandAudio != null && SoundManager.OraClip != null)
            {
                StandAudio.clip = SoundManager.OraClip;
                StandAudio.time = 0f;
                StandAudio.loop = true;
                StandAudio.Play();
            }

            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "★ STAR PLATINUM: ORA ORA ORA! ★");
            }

            Plugin.Log?.LogInfo("Star Platinum: ORA barrage activated!");
            return true;
        }

        public void OnBlockHit(bool isParry)
        {
            _blockReactionTimer = 0.25f;
            _isParryReaction = isParry;
        }

        private void UpdateBlockDefend()
        {
            var t = Owner.transform;

            // Stand moves smoothly in front of the player
            Vector3 targetPos = t.TransformPoint(BlockOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Smooth * 1.5f * Time.deltaTime);

            // Stand faces player's look direction
            Vector3 lookDir = Owner.GetLookDir();
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Smooth * Time.deltaTime);
            }

            // Cross arms defensively across chest
            Vector3 guardFistL = _baseFistL + new Vector3(0.35f, 0.22f, 0.12f);
            Vector3 guardFistR = _baseFistR + new Vector3(-0.35f, 0.22f, 0.12f);
            Vector3 guardArmL  = _baseArmL  + new Vector3(0.20f, 0.08f, 0.10f);
            Vector3 guardArmR  = _baseArmR  + new Vector3(-0.20f, 0.08f, 0.10f);

            // Impact reactions
            if (_blockReactionTimer > 0f)
            {
                if (_isParryReaction)
                {
                    // Parry deflect: thrust fists forward
                    guardFistL += Vector3.forward * 0.45f;
                    guardFistR += Vector3.forward * 0.45f;
                }
                else
                {
                    // Block absorb: slight recoil backward
                    guardFistL -= Vector3.forward * 0.15f;
                    guardFistR -= Vector3.forward * 0.15f;
                }
            }

            if (FistL != null) FistL.localPosition = Vector3.Lerp(FistL.localPosition, guardFistL, Smooth * Time.deltaTime);
            if (FistR != null) FistR.localPosition = Vector3.Lerp(FistR.localPosition, guardFistR, Smooth * Time.deltaTime);
            if (ArmL != null)  ArmL.localPosition  = Vector3.Lerp(ArmL.localPosition,  guardArmL,  Smooth * Time.deltaTime);
            if (ArmR != null)  ArmR.localPosition  = Vector3.Lerp(ArmR.localPosition,  guardArmR,  Smooth * Time.deltaTime);

            // Flare Stand aura into a bright protective barrier
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 2.4f, Smooth * Time.deltaTime);
            }
        }

        private void UpdateAttack()
        {
            _attackTimer -= Time.deltaTime;
            _punchAnimTime += Time.deltaTime * 35f;

            // Move Stand forward into attack position
            var t = Owner.transform;
            Vector3 targetPos = t.TransformPoint(AttackOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Smooth * 1.5f * Time.deltaTime);

            // Stand faces player's look direction
            Vector3 lookDir = Owner.GetLookDir();
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Smooth * Time.deltaTime);
            }

            // Animate fists alternating forward/back rapidly
            float punchL = Mathf.Sin(_punchAnimTime);
            float punchR = Mathf.Sin(_punchAnimTime + Mathf.PI);

            if (FistL != null)
            {
                float extL = Mathf.Clamp(punchL, 0f, 1f) * 1.35f;
                FistL.localPosition = _baseFistL + Vector3.forward * extL;
            }
            if (FistR != null)
            {
                float extR = Mathf.Clamp(punchR, 0f, 1f) * 1.35f;
                FistR.localPosition = _baseFistR + Vector3.forward * extR;
            }
            if (ArmL != null)
            {
                ArmL.localPosition = _baseArmL + Vector3.forward * (Mathf.Clamp(punchL, 0f, 1f) * 0.5f);
            }
            if (ArmR != null)
            {
                ArmR.localPosition = _baseArmR + Vector3.forward * (Mathf.Clamp(punchR, 0f, 1f) * 0.5f);
            }

            // Punch damage tick every ~0.12s
            if (Time.time >= _nextPunchDamageTime)
            {
                _nextPunchDamageTime = Time.time + 0.12f;
                PerformPunchHit();
            }

            // Check if duration expired; if button is still held and stamina available, extend barrage
            if (_attackTimer <= 0f)
            {
                bool hold = Owner == Player.m_localPlayer && (ZInput.GetButton("SecondaryAttack") || ZInput.GetButton("JoySecondaryAttack"));
                if (hold && Owner.HaveStamina(_staminaCost * 0.5f))
                {
                    Owner.UseStamina(_staminaCost * 0.5f);
                    _attackTimer = 0.6f;
                }
                else
                {
                    StopAttack();
                }
            }
        }

        private void StopAttack()
        {
            IsAttacking = false;
            _cooldownTimer = 0.35f;

            // Stop barrage sound
            if (StandAudio != null && StandAudio.isPlaying)
            {
                StandAudio.Stop();
            }

            // Restore limbs
            if (FistL != null) FistL.localPosition = _baseFistL;
            if (FistR != null) FistR.localPosition = _baseFistR;
            if (ArmL != null) ArmL.localPosition = _baseArmL;
            if (ArmR != null) ArmR.localPosition = _baseArmR;
        }

        private void OnDestroy()
        {
            if (StandAudio != null && StandAudio.isPlaying)
            {
                StandAudio.Stop();
            }
        }

        private void UpdateFollow()
        {
            var t = Owner.transform;
            Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.06f);
            Vector3 target = t.TransformPoint(IdleOffset) + bob;
            transform.position = Vector3.Lerp(transform.position, target, Smooth * Time.deltaTime);

            Vector3 lookDir = Owner.GetLookDir();
            lookDir.y = 0;
            Quaternion targetRot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : t.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Smooth * Time.deltaTime);

            // Restore limbs smoothly to resting pose
            if (FistL != null) FistL.localPosition = Vector3.Lerp(FistL.localPosition, _baseFistL, Smooth * Time.deltaTime);
            if (FistR != null) FistR.localPosition = Vector3.Lerp(FistR.localPosition, _baseFistR, Smooth * Time.deltaTime);
            if (ArmL != null)  ArmL.localPosition  = Vector3.Lerp(ArmL.localPosition,  _baseArmL,  Smooth * Time.deltaTime);
            if (ArmR != null)  ArmR.localPosition  = Vector3.Lerp(ArmR.localPosition,  _baseArmR,  Smooth * Time.deltaTime);

            // Return aura intensity to normal
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 1.3f, Smooth * Time.deltaTime);
            }
        }

        private void PerformPunchHit()
        {
            Vector3 center = Owner.transform.position + Owner.transform.forward * 2.2f + Vector3.up * 1.1f;
            Collider[] hits = Physics.OverlapSphere(center, 2.2f);
            var damaged = new HashSet<IDestructible>();

            foreach (var col in hits)
            {
                if (col.isTrigger) continue;
                var destructible = col.GetComponentInParent<IDestructible>();
                if (destructible == null || damaged.Contains(destructible)) continue;
                if (destructible is Character c && c == Owner) continue;

                damaged.Add(destructible);

                var hit = new HitData
                {
                    m_point = col.bounds.ClosestPoint(center),
                    m_dir = Owner.transform.forward,
                    m_pushForce = 25f,
                    m_staggerMultiplier = 2.5f
                };
                hit.m_damage.m_blunt = 35f;
                hit.SetAttacker(Owner);

                destructible.Damage(hit);
            }
        }
    }
}
