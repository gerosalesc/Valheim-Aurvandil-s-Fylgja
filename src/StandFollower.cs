using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Controls Star Platinum's movement, idle hovering over the player's shoulder,
    /// defensive shield positioning when blocking, single-punch strikes, and the rapid ORA punch barrage.
    /// Faces the direction the player is facing (independent of camera).
    /// </summary>
    internal class StandFollower : MonoBehaviour
    {
        internal enum AttackStartStatus
        {
            Started,
            Busy,
            Unavailable
        }

        private enum AttackMode
        {
            None,
            SinglePunch,
            Barrage
        }

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

        // Positioning offsets relative to player's body orientation
        public Vector3 IdleOffset   = new Vector3(0.55f, 0.40f, -0.75f); // floating over right shoulder
        public Vector3 AttackOffset = new Vector3(0.35f, 0.20f, 0.55f);  // lunging forward beside player
        public Vector3 BlockOffset  = new Vector3(0.00f, 0.25f, 0.85f);  // hovering right in front of player as shield
        public float Smooth = 12f;

        // Entrance animation
        private float _entranceTimer = 0.7f;
        private float _entranceDuration = 0.7f;
        private Vector3 _targetScale = new Vector3(1.22f, 1.22f, 1.22f);

        // Barrage tuning (Hit-limited: 5 punches per trigger)
        public const int MaxPunchesPerBarrage = 5;
        private const float BarrageStaminaCost = 16f;
        private const float BarrageDuration = 0.85f;
        private const float BarrageFirstHitDelay = 0.04f;
        private const float BarrageHitInterval = 0.12f;
        private const float BarrageAnimInterval = 0.14f;
        private const float BarrageAnimSpeed = 3.2f;
        private const float BarrageCooldown = 0.25f;

        // Single-punch tuning (1 hit, silent, fast left-click trigger)
        private const float SinglePunchStaminaCost = 5f;
        private const float SinglePunchDuration = 0.34f;
        private const float SinglePunchHitDelay = 0.10f;
        private const float SinglePunchAnimSpeed = 2.0f;
        private const float SinglePunchCooldown = 0.06f;
        private const float SingleComboResetWindow = 0.90f;

        // Attack state
        public bool IsAttacking { get; private set; }
        private AttackMode _attackMode = AttackMode.None;
        private AttackMode _lastCompletedMode = AttackMode.None;
        private int _punchesRemaining = 0;
        private float _attackTimer = 0f;
        private float _punchAnimTime = 0f;
        private float _nextPunchDamageTime = 0f;
        private float _nextPunchAnimTriggerTime = 0f;
        private float _cooldownTimer = 0f;

        // Player unarmed animation parameters
        private string _punchAnimBase = "unarmed_attack";
        private int _punchChainLevels = 3;
        private int _barragePunchIndex = 0;
        private int _singleComboIndex = 0;
        private float _lastSinglePunchTime = -10f;
        private string _lastTrigger = null;

        // Block reaction state
        private float _blockReactionTimer = 0f;
        private bool _isParryReaction = false;
        private float _guardBlend = 0f;

        // Animator parameter cache
        private int _blockingParamHash = 0;
        private bool _hasBlockingParam = false;
        private readonly HashSet<string> _animParamNames = new HashSet<string>();

        private void Start()
        {
            _entranceTimer = _entranceDuration;
            transform.localScale = _targetScale * 0.25f;

            // Detect player's unarmed attack animation configuration
            if (Owner != null && Owner.m_unarmedWeapon != null)
            {
                var shared = Owner.m_unarmedWeapon.m_itemData?.m_shared;
                if (shared != null && shared.m_attack != null && !string.IsNullOrEmpty(shared.m_attack.m_attackAnimation))
                {
                    _punchAnimBase = shared.m_attack.m_attackAnimation;
                    _punchChainLevels = shared.m_attack.m_attackChainLevels;
                    Plugin.Log?.LogInfo($"StandFollower: Using player's punch animation '{_punchAnimBase}' (chains: {_punchChainLevels})");
                }
            }

            if (StandAnimator != null)
            {
                foreach (var p in StandAnimator.parameters)
                {
                    _animParamNames.Add(p.name);
                    if (p.name == "blocking")
                    {
                        _hasBlockingParam = true;
                        _blockingParamHash = p.nameHash;
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

            // Entrance emergence animation
            if (_entranceTimer > 0f)
            {
                _entranceTimer -= Time.deltaTime;
                float progress = 1f - Mathf.Clamp01(_entranceTimer / _entranceDuration);
                float scaleMult = Mathf.Lerp(0.25f, 1.0f, Mathf.SmoothStep(0f, 1f, progress * 1.15f));
                transform.localScale = _targetScale * scaleMult;

                if (Aura != null)
                {
                    Aura.intensity = Mathf.Lerp(4.5f, 1.4f, progress);
                    Aura.range = Mathf.Lerp(6.0f, 4.2f, progress);
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

            _guardBlend = Mathf.MoveTowards(_guardBlend, isBlocking ? 1f : 0f, Time.deltaTime * 8f);

            // Defensive and entrance bone posing (punching now uses pure accelerated player animator)
            if (isBlocking)
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
                        // Absorb recoil backward
                        if (FistL != null) FistL.position -= transform.forward * 0.12f;
                        if (FistR != null) FistR.position -= transform.forward * 0.12f;
                    }
                }
                else
                {
                    // Guard stance: cross arms across chest (eased in so it never pops)
                    if (FistL != null) FistL.position += (transform.right * 0.15f + transform.forward * 0.16f + transform.up * 0.10f) * _guardBlend;
                    if (FistR != null) FistR.position += (-transform.right * 0.15f + transform.forward * 0.16f + transform.up * 0.10f) * _guardBlend;
                }
            }
            else if (_entranceTimer > 0.35f)
            {
                // Manifestation flex during entrance
                if (FistL != null) FistL.position += transform.right * 0.20f + transform.forward * 0.18f + transform.up * 0.12f;
                if (FistR != null) FistR.position += -transform.right * 0.20f + transform.forward * 0.18f + transform.up * 0.12f;
            }
        }

        public AttackStartStatus TryStartAttack(bool allowStaminaFlash = true) =>
            TryBeginAttack(AttackMode.Barrage, allowStaminaFlash);

        public AttackStartStatus TryStartSinglePunch(bool allowStaminaFlash = true) =>
            TryBeginAttack(AttackMode.SinglePunch, allowStaminaFlash);

        private AttackStartStatus TryBeginAttack(AttackMode mode, bool allowStaminaFlash)
        {
            if (Owner == null || !StandManager.AreHandsFree(Owner))
            {
                return AttackStartStatus.Unavailable;
            }

            if (mode == AttackMode.SinglePunch)
            {
                if (IsAttacking || _cooldownTimer > 0f) return AttackStartStatus.Busy;
            }
            else if (mode == AttackMode.Barrage)
            {
                if (IsAttacking && _attackMode == AttackMode.Barrage) return AttackStartStatus.Busy;
                if (_lastCompletedMode == AttackMode.Barrage && _cooldownTimer > 0f) return AttackStartStatus.Busy;
            }

            float staminaCost = (mode == AttackMode.Barrage) ? BarrageStaminaCost : SinglePunchStaminaCost;
            if (!Owner.HaveStamina(staminaCost))
            {
                if (allowStaminaFlash && Hud.instance != null)
                {
                    Hud.instance.StaminaBarEmptyFlash();
                }
                return AttackStartStatus.Unavailable;
            }

            // Allow an incoming barrage to cancel out of a completed/recovering single punch cleanly
            if (IsAttacking && _attackMode == AttackMode.SinglePunch && mode == AttackMode.Barrage)
            {
                StopAttack();
            }

            Owner.UseStamina(staminaCost);

            IsAttacking = true;
            _attackMode = mode;
            _punchAnimTime = 0f;
            _lastTrigger = null;

            if (mode == AttackMode.Barrage)
            {
                _punchesRemaining = MaxPunchesPerBarrage;
                _attackTimer = BarrageDuration;
                _barragePunchIndex = 1; // 0 triggered immediately below
                _nextPunchDamageTime = Time.time + BarrageFirstHitDelay;
                _nextPunchAnimTriggerTime = BarrageAnimInterval;

                TriggerPunchAnim(BarrageAnimSpeed, 0);

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

                Plugin.Log?.LogInfo("Star Platinum: 5-hit ORA barrage activated!");
            }
            else
            {
                _punchesRemaining = 1;
                _attackTimer = SinglePunchDuration;
                _nextPunchDamageTime = Time.time + SinglePunchHitDelay;

                if (Time.time - _lastSinglePunchTime > SingleComboResetWindow)
                {
                    _singleComboIndex = 0;
                }

                int comboIdx = _singleComboIndex;
                _singleComboIndex = (_singleComboIndex + 1) % Mathf.Max(1, _punchChainLevels);

                TriggerPunchAnim(SinglePunchAnimSpeed, comboIdx);

                // Ensure no ORA shout plays for single punches
                if (StandAudio != null && StandAudio.isPlaying)
                {
                    StandAudio.Stop();
                }
            }

            return AttackStartStatus.Started;
        }

        private void TriggerPunchAnim(float speed, int chainIdx)
        {
            if (StandAnimator == null) return;

            StandAnimator.speed = speed;

            // Ensure unarmed animation state (0 = Unarmed)
            if (HasParameter("statef")) StandAnimator.SetFloat("statef", 0f);
            if (HasParameter("statei")) StandAnimator.SetInteger("statei", 0);

            // Determine punch trigger based on player's unarmed attack
            string baseAnim = !string.IsNullOrEmpty(_punchAnimBase) ? _punchAnimBase : "unarmed_attack";
            string triggerName = (_punchChainLevels > 1) ? (baseAnim + chainIdx) : baseAnim;

            // Reset previous trigger if any to allow rapid retriggering
            if (!string.IsNullOrEmpty(_lastTrigger) && HasParameter(_lastTrigger))
            {
                StandAnimator.ResetTrigger(_lastTrigger);
            }

            // Set new trigger
            if (HasParameter(triggerName))
            {
                StandAnimator.SetTrigger(triggerName);
                _lastTrigger = triggerName;
            }
            else if (HasParameter(baseAnim))
            {
                StandAnimator.SetTrigger(baseAnim);
                _lastTrigger = baseAnim;
            }

            // Crossfade into state directly if present in Animator for instantaneous punch snap
            int stateHash = Animator.StringToHash(triggerName);
            if (StandAnimator.HasState(0, stateHash))
            {
                StandAnimator.CrossFadeInFixedTime(stateHash, 0.04f, 0, 0f);
            }
            else
            {
                int baseHash = Animator.StringToHash(baseAnim);
                if (StandAnimator.HasState(0, baseHash))
                {
                    StandAnimator.CrossFadeInFixedTime(baseHash, 0.04f, 0, 0f);
                }
            }
        }

        private bool HasParameter(string paramName) =>
            !string.IsNullOrEmpty(paramName) && _animParamNames.Contains(paramName);

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

            // Stand faces the EXACT direction the player is facing (independent of camera)
            transform.rotation = Quaternion.Slerp(transform.rotation, t.rotation, Smooth * 1.5f * Time.deltaTime);

            // Flare Stand aura into a bright protective barrier
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 2.5f, Smooth * Time.deltaTime);
            }
        }

        private void UpdateAttack()
        {
            _attackTimer -= Time.deltaTime;
            _punchAnimTime += Time.deltaTime;

            // Move Stand forward into attack position
            var t = Owner.transform;
            Vector3 targetPos = t.TransformPoint(AttackOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Smooth * 1.5f * Time.deltaTime);

            // Stand faces the direction the player is facing (independent of camera)
            transform.rotation = Quaternion.Slerp(transform.rotation, t.rotation, Smooth * 1.5f * Time.deltaTime);

            if (_attackMode == AttackMode.Barrage)
            {
                // Subtle flurry recoil forward with punches
                transform.position += transform.forward * (Mathf.Abs(Mathf.Sin(_punchAnimTime * 28f)) * 0.06f);

                // Cycle punch animation rapidly across the 5 hits (0, 1, 0, 1, 2)
                if (_punchAnimTime >= _nextPunchAnimTriggerTime)
                {
                    _nextPunchAnimTriggerTime = _punchAnimTime + BarrageAnimInterval;
                    int chainIdx = (_barragePunchIndex < 4) ? (_barragePunchIndex % 2) : 2;
                    _barragePunchIndex++;
                    TriggerPunchAnim(BarrageAnimSpeed, chainIdx);
                }

                // Punch damage tick every ~0.12s, strictly limited to 5 hits per trigger
                if (Time.time >= _nextPunchDamageTime && _punchesRemaining > 0)
                {
                    _nextPunchDamageTime = Time.time + BarrageHitInterval;
                    _punchesRemaining--;
                    PerformPunchHit();

                    if (_punchesRemaining <= 0)
                    {
                        StopAttack();
                        return;
                    }
                }
            }
            else if (_attackMode == AttackMode.SinglePunch)
            {
                // Single punch connects once on extension, then lets follow-through finish cleanly
                if (Time.time >= _nextPunchDamageTime && _punchesRemaining > 0)
                {
                    _punchesRemaining--;
                    PerformPunchHit();
                }
            }

            // Completion / safety timeout
            if (_attackTimer <= 0f)
            {
                StopAttack();
            }
        }

        private void StopAttack()
        {
            if (_attackMode == AttackMode.SinglePunch)
            {
                _lastSinglePunchTime = Time.time;
            }

            _lastCompletedMode = _attackMode;
            _cooldownTimer = (_attackMode == AttackMode.Barrage) ? BarrageCooldown : SinglePunchCooldown;
            _attackMode = AttackMode.None;
            IsAttacking = false;
            _punchesRemaining = 0;

            if (StandAnimator != null)
            {
                StandAnimator.speed = 1.0f;
                if (!string.IsNullOrEmpty(_lastTrigger) && HasParameter(_lastTrigger))
                {
                    StandAnimator.ResetTrigger(_lastTrigger);
                }
                _lastTrigger = null;
            }

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

            // Stand faces the EXACT direction the player is facing (independent of camera)
            transform.rotation = Quaternion.Slerp(transform.rotation, t.rotation, Smooth * Time.deltaTime);

            // Return aura intensity to normal
            if (Aura != null && _entranceTimer <= 0f)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 1.4f, Smooth * Time.deltaTime);
                Aura.range = Mathf.Lerp(Aura.range, 4.2f, Smooth * Time.deltaTime);
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
