using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Controls Star Platinum's movement, idle hovering over the player's shoulder,
    /// defensive shield positioning when blocking, and executing the ORA punch barrage.
    /// Uses LateUpdate to apply procedural fist punches and guard poses on top of the character rig.
    /// </summary>
    internal class StandFollower : MonoBehaviour
    {
        public Player Owner;

        // Limb / bone transforms
        public Transform FistL;
        public Transform FistR;
        public Transform ArmL;
        public Transform ArmR;
        public Transform Torso;
        public Transform Head;

        public Animator StandAnimator;
        public Light Aura;
        public AudioSource StandAudio;

        // Positioning offsets
        public Vector3 IdleOffset   = new Vector3(0.55f, 0.40f, -0.75f); // floating over right shoulder
        public Vector3 AttackOffset = new Vector3(0.35f, 0.20f, 0.55f);  // lunging forward beside player
        public Vector3 BlockOffset  = new Vector3(0.00f, 0.25f, 0.85f);  // hovering right in front of player as shield
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

        private int _blockingParamHash = 0;
        private bool _hasBlockingParam = false;

        private void Start()
        {
            if (StandAnimator != null)
            {
                foreach (var p in StandAnimator.parameters)
                {
                    if (p.name == "blocking")
                    {
                        _hasBlockingParam = true;
                        _blockingParamHash = p.nameHash;
                        break;
                    }
                }
            }
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

        private void LateUpdate()
        {
            // Sync blocking animator state
            bool isBlocking = !IsAttacking && Owner != null && Owner.IsBlocking() && StandManager.AreHandsFree(Owner);
            if (_hasBlockingParam && StandAnimator != null)
            {
                StandAnimator.SetBool(_blockingParamHash, isBlocking);
            }

            // Procedural bone posing applied in LateUpdate after the Animator has evaluated
            if (IsAttacking)
            {
                float punchL = Mathf.Sin(_punchAnimTime);
                float punchR = Mathf.Sin(_punchAnimTime + Mathf.PI);

                float extL = Mathf.Clamp(punchL, 0f, 1f) * 0.55f;
                float extR = Mathf.Clamp(punchR, 0f, 1f) * 0.55f;

                if (FistL != null)
                {
                    FistL.position += transform.forward * extL + transform.up * (punchL * 0.04f);
                }
                if (FistR != null)
                {
                    FistR.position += transform.forward * extR + transform.up * (punchR * 0.04f);
                }
                if (ArmL != null)
                {
                    ArmL.position += transform.forward * (extL * 0.4f);
                }
                if (ArmR != null)
                {
                    ArmR.position += transform.forward * (extR * 0.4f);
                }
            }
            else if (isBlocking)
            {
                if (_blockReactionTimer > 0f)
                {
                    if (_isParryReaction)
                    {
                        // Deflection thrust forward
                        if (FistL != null) FistL.position += transform.forward * 0.40f;
                        if (FistR != null) FistR.position += transform.forward * 0.40f;
                    }
                    else
                    {
                        // Slight absorb recoil backward
                        if (FistL != null) FistL.position -= transform.forward * 0.12f;
                        if (FistR != null) FistR.position -= transform.forward * 0.12f;
                    }
                }
                else
                {
                    // Guard stance: cross arms across chest
                    if (FistL != null) FistL.position += transform.right * 0.15f + transform.forward * 0.16f + transform.up * 0.10f;
                    if (FistR != null) FistR.position += -transform.right * 0.15f + transform.forward * 0.16f + transform.up * 0.10f;
                }
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

            // Stand moves smoothly right in front of the player
            Vector3 targetPos = t.TransformPoint(BlockOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Smooth * 1.5f * Time.deltaTime);

            // Stand faces player's look direction
            Vector3 lookDir = Owner.GetLookDir();
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Smooth * Time.deltaTime);
            }

            // Flare Stand aura into a bright protective barrier
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 2.5f, Smooth * Time.deltaTime);
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

            // Punch damage tick every ~0.12s
            if (Time.time >= _nextPunchDamageTime)
            {
                _nextPunchDamageTime = Time.time + 0.12f;
                PerformPunchHit();
            }

            // Extend barrage if button held and stamina available
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
            Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.07f);
            Vector3 target = t.TransformPoint(IdleOffset) + bob;
            transform.position = Vector3.Lerp(transform.position, target, Smooth * Time.deltaTime);

            Vector3 lookDir = Owner.GetLookDir();
            lookDir.y = 0;
            Quaternion targetRot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : t.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Smooth * Time.deltaTime);

            // Return aura intensity to normal
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 1.4f, Smooth * Time.deltaTime);
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
