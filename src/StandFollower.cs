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

        // Full scale on summon (no slow wakeup or scale-in delay)
        private readonly Vector3 _targetScale = new Vector3(1.22f, 1.22f, 1.22f);
        private int[] _idleStateHashes;
        private bool _isPlayingIntroGPower = false;
        private float _introGPowerTimer = 0f;

        // Barrage tuning (Hit-limited: 12 rapid punches per trigger)
        public const int MaxPunchesPerBarrage = 12;
        private const float BarrageStaminaCost = 16f;
        private const float BarrageDuration = 1.45f;
        private const float BarrageFirstHitDelay = 0.03f;
        private const float BarrageHitInterval = 0.115f;
        private const float BarrageAnimInterval = 0.07f;
        private const float BarrageAnimSpeed = 6.2f;
        private const float BarrageAnimStartNormalized = 0.22f;
        private const float BarrageVfxInterval = 0.025f;
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
        private float _nextBarrageVfxTime = 0f;
        private float _cooldownTimer = 0f;
        private StandBarrageVfx _barrageVfx;

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

        internal void ConfigureAnimatorStates(int[] idleStateHashes)
        {
            _idleStateHashes = idleStateHashes;
        }

        internal void NotifyIntroGPowerStarted()
        {
            _isPlayingIntroGPower = true;
            _introGPowerTimer = 1.6f;
        }

        private bool IsBlockingNow() =>
            !IsAttacking && Owner != null && Owner.IsBlocking() && StandManager.AreHandsFree(Owner);

        private void InterruptIntroGPower(bool resetBaseLayer)
        {
            if (!_isPlayingIntroGPower) return;
            _isPlayingIntroGPower = false;
            _introGPowerTimer = 0f;

            if (StandAnimator == null) return;
            if (HasParameter("gpower"))
            {
                StandAnimator.ResetTrigger("gpower");
            }

            if (_idleStateHashes == null) return;
            int startLayer = resetBaseLayer ? 0 : 1;
            int count = Mathf.Min(_idleStateHashes.Length, StandAnimator.layerCount);
            for (int layer = startLayer; layer < count; layer++)
            {
                int idleHash = _idleStateHashes[layer];
                if (idleHash == 0) continue;

                var cur = StandAnimator.GetCurrentAnimatorStateInfo(layer);
                if (cur.fullPathHash != idleHash || StandAnimator.IsInTransition(layer))
                {
                    StandAnimator.CrossFadeInFixedTime(idleHash, 0.04f, layer, 0f);
                }
            }
        }

        private bool AreAllLayersAtIdle()
        {
            if (StandAnimator == null || _idleStateHashes == null) return true;
            int count = Mathf.Min(_idleStateHashes.Length, StandAnimator.layerCount);
            for (int layer = 0; layer < count; layer++)
            {
                int idleHash = _idleStateHashes[layer];
                if (idleHash == 0) continue;
                if (StandAnimator.IsInTransition(layer)) return false;
                if (StandAnimator.GetCurrentAnimatorStateInfo(layer).fullPathHash != idleHash) return false;
            }
            return true;
        }

        private void Start()
        {
            transform.localScale = _targetScale;
            _barrageVfx = new StandBarrageVfx();

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
            _barrageVfx?.Tick(Time.deltaTime);

            if (_isPlayingIntroGPower)
            {
                _introGPowerTimer -= Time.deltaTime;
                if (_introGPowerTimer <= 0f || (_introGPowerTimer < 1.35f && AreAllLayersAtIdle()))
                {
                    _isPlayingIntroGPower = false;
                }
            }

            bool isBlocking = IsBlockingNow();
            if (isBlocking && _isPlayingIntroGPower)
            {
                InterruptIntroGPower(resetBaseLayer: true);
            }

            // Sync blocking parameter before Unity evaluates Animator this frame
            if (_hasBlockingParam && StandAnimator != null)
            {
                StandAnimator.SetBool(_blockingParamHash, isBlocking);
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
            else if (isBlocking)
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
            bool isBlocking = IsBlockingNow();

            _guardBlend = Mathf.MoveTowards(_guardBlend, isBlocking ? 1f : 0f, Time.deltaTime * 8f);

            // Defensive and flurry punch bone posing
            if (IsAttacking && _attackMode == AttackMode.Barrage)
            {
                // Subtle high-frequency alternating forearm drive in sync with 6.2x punch clips
                float waveL = Mathf.Max(0f, Mathf.Sin(_punchAnimTime * 54f));
                float waveR = Mathf.Max(0f, Mathf.Sin(_punchAnimTime * 54f + Mathf.PI));
                if (ArmL != null) ArmL.position += transform.forward * (waveL * 0.085f);
                if (ArmR != null) ArmR.position += transform.forward * (waveR * 0.085f);
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

            // Cancel the spawn Guardian Power pose cleanly if the player attacks right away
            InterruptIntroGPower(resetBaseLayer: false);

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
                _nextBarrageVfxTime = 0f;

                TriggerPunchAnim(BarrageAnimSpeed, 0, BarrageAnimStartNormalized, 0.018f);

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

                Plugin.Log?.LogInfo("Star Platinum: 12-hit ORA barrage activated!");
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

        private void TriggerPunchAnim(float speed, int chainIdx, float startNormalizedTime = 0f, float crossFadeDuration = 0.04f)
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
                StandAnimator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0, startNormalizedTime);
            }
            else
            {
                int baseHash = Animator.StringToHash(baseAnim);
                if (StandAnimator.HasState(0, baseHash))
                {
                    StandAnimator.CrossFadeInFixedTime(baseHash, crossFadeDuration, 0, startNormalizedTime);
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
                transform.position += transform.forward * (Mathf.Abs(Mathf.Sin(_punchAnimTime * 42f)) * 0.06f);

                // Flare Stand aura during barrage
                if (Aura != null)
                {
                    float pulse = 2.8f + Mathf.Abs(Mathf.Sin(_punchAnimTime * 40f)) * 0.65f;
                    Aura.intensity = Mathf.Lerp(Aura.intensity, pulse, Smooth * Time.deltaTime);
                    Aura.range = Mathf.Lerp(Aura.range, 5.4f, Smooth * Time.deltaTime);
                }

                // Emit high-frequency spectral afterimage punches forward
                if (_punchAnimTime >= _nextBarrageVfxTime)
                {
                    _nextBarrageVfxTime = _punchAnimTime + BarrageVfxInterval;
                    _barrageVfx?.SpawnFlurryFist(transform);
                }

                // Cycle valid unarmed punch chains rapidly (~14 punches/sec), skipping slow wind-up
                if (_punchAnimTime >= _nextPunchAnimTriggerTime)
                {
                    _nextPunchAnimTriggerTime = _punchAnimTime + BarrageAnimInterval;
                    int chainIdx = _barragePunchIndex % Mathf.Max(1, _punchChainLevels);
                    _barragePunchIndex++;
                    TriggerPunchAnim(BarrageAnimSpeed, chainIdx, BarrageAnimStartNormalized, 0.018f);
                }

                // Punch damage tick every ~0.115s, strictly limited to 12 hits per trigger (~1.30s total)
                if (Time.time >= _nextPunchDamageTime && _punchesRemaining > 0)
                {
                    _nextPunchDamageTime = Time.time + BarrageHitInterval;
                    bool isFinisher = _punchesRemaining == 1;
                    _punchesRemaining--;

                    PerformPunchHit(
                        damage: isFinisher ? 45f : 20f,
                        pushForce: isFinisher ? 32f : 5f,
                        staggerMultiplier: isFinisher ? 3.0f : 1.5f);

                    Vector3 ringPos = transform.position + transform.up * 1.32f + transform.forward * 1.30f;
                    _barrageVfx?.SpawnShockRing(ringPos, transform.forward, isFinisher ? 1.55f : 1.0f);

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
                    PerformPunchHit(damage: 35f, pushForce: 25f, staggerMultiplier: 2.5f);
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
            _barrageVfx?.Dispose();
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
            if (Aura != null)
            {
                Aura.intensity = Mathf.Lerp(Aura.intensity, 1.4f, Smooth * Time.deltaTime);
                Aura.range = Mathf.Lerp(Aura.range, 4.2f, Smooth * Time.deltaTime);
            }
        }

        private void PerformPunchHit(float damage, float pushForce, float staggerMultiplier)
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
                    m_pushForce = pushForce,
                    m_staggerMultiplier = staggerMultiplier
                };
                hit.m_damage.m_blunt = damage;
                hit.SetAttacker(Owner);

                destructible.Damage(hit);
            }
        }
    }
}
