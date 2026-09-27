using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Abo ng Simula's signature ability: "It covers the first symbol of a word with ash." While an
/// <b>armed</b> Abo lives, the HUD's incomplete-word clue ashes over the word's <b>first</b> slot
/// as well as the target one, so the player cannot read the opening symbol
/// (<c>ActiveCluePresenter.BuildMaskedSpellingWithRestoration</c>).
///
/// <para>
/// <b>This ability changes only what is drawn.</b> Credit is decided in
/// <c>ActiveClueDirector.TryConsumeClue</c> from enemy identity, and recognition matching is a
/// <c>characterID</c> compare in <c>CombatResolver</c>; neither reads anything this ability
/// affects. A correct draw resolves exactly as it would with no Abo on screen — asserted, not
/// assumed, in AbongSimulaAshTests.
/// </para>
///
/// <para>
/// <b>Per-spawn arming (Level 1 design §1 and §3 rule 2).</b> The ash used to be on whenever any
/// Abo with the flag was alive, which put it on the first enemy the player ever meets: the HUD
/// changed before the player had ever read it unobscured, so there was no baseline against which
/// the change could register as an enemy doing something. Arming is therefore per spawn, and every
/// arming is announced by a gust (<see cref="AshGustController"/>) so the HUD change is
/// attributable to the Abo that caused it.
/// </para>
///
/// <para>
/// <b>What an introduction spawn does depends on the outcome, and the two are opposite.</b> Under
/// <c>IntroductionOutcome.IntroduceAndSuppress</c> — the default for every type, and what
/// <see cref="SetSuppressedForIntroductionSpawn"/> switches on — the ability is inert for that
/// spawn: the card states it, the clue stays readable, and a later spawn arms it. Under
/// <c>IntroductionOutcome.IntroduceAndArm</c> the inversion applies: the ash is NOT suppressed and
/// is expected to arm <i>during</i> the introduction, because beat 2 of the eight-beat lesson exists
/// to show the ability landing before the enemy is named. <c>DeferAndSuppress</c> suppresses like
/// the first case.
///
/// <b>No shipped level currently authors that inversion for Abo.</b> Level 1's lesson moved to
/// Iligaw (<c>IligawLesson</c>, waiting on <see cref="MirrorDecoyController"/>): a mirror copy reads
/// cold, where the ash needs a restored slot to degrade before it can be seen at all. Abo still
/// spawns on Level 1 and the ash still behaves exactly as described here — he simply gets the
/// standard four-step card rather than the eight-beat lesson, so in practice this component takes
/// the <c>IntroduceAndSuppress</c> branch. The arm branch is live machinery, not dead code: it is
/// what any future ash-carrying lesson would use, and <c>Level1LessonTests</c> still exercises it
/// through an Abo-shaped lesson on purpose, as the evidence that beat 2's wait is not hardcoded to
/// one ability.
/// </para>
///
/// <para>
/// <b>Why the trigger insists on the needed slot's position.</b> The ash masks the word's first
/// slot <i>as well as</i> the target slot, so the two coincide whenever the needed slot already is
/// its word's first symbol and the ash then changes nothing at all. Arming there would show the
/// player a gust that visibly does nothing — the same "bug, not threat" failure in a new costume.
/// For Level 1's <c>INA AMA</c> the ash only bites in the slot-2 and slot-4 windows, which is what
/// <see cref="_requiredNeededSlotPositionInWord"/> encodes.
/// </para>
///
/// <para>
/// The controller carries no per-frame presentation. Its jobs are to answer "is an armed Abo alive
/// right now?" for the presenter, to stop answering yes the moment its enemy dies, is disabled or
/// is recycled, and to evaluate the arming trigger while it is unarmed.
/// </para>
///
/// Data-driven through <see cref="EnemyDataSO.ashesFirstSlot"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class AshFirstSlotController : MonoBehaviour, IIntroducibleAbility
{
    /// <summary>
    /// Live armed Abo controllers. A set rather than a counter because a pooled shell that is
    /// disabled without its OnDisable running (domain reload, test teardown) would strand a
    /// counter permanently; membership can be re-verified instead, which
    /// <see cref="IsAnyActive"/> does.
    /// </summary>
    private static readonly HashSet<AshFirstSlotController> Registered = new();

    /// <summary>
    /// Fallback "the ash has already been shown this level" latch, used only on a level with no
    /// <see cref="SpawnAssignmentCoordinator"/>. Where a coordinator exists its gate registry is
    /// the authority instead, because that resets on every <c>ApplyLevel</c> and therefore gets
    /// retries right for free — a static latch would leave the ash spent on the second attempt.
    /// </summary>
    private static bool _ashShownFallbackLatch;

    /// <summary>
    /// Cached because the trigger is evaluated every frame an unarmed Abo is alive and an
    /// object-graph search per frame is not. Cleared whenever a scene loads, so a cached
    /// coordinator from the previous level is never consulted.
    /// </summary>
    private static SpawnAssignmentCoordinator _cachedCoordinator;

    [Header("Arming Trigger")]
    [Tooltip("Seconds this spawn must have been on screen before the ash may arm. Keeps the gust "
             + "from firing simultaneously with the Abo's own entrance, where the player would "
             + "read the two as one event. 1.5 s per the Level 1 design. NOTE: accrued on SCALED "
             + "time (see Tick), so under an introduction beat's 0.15 time scale this is about ten "
             + "wall-clock seconds. EnemyIntroductionBeat.PlayAbilityBeat waits on the armed flag "
             + "rather than on a duration precisely because of that. This only paces a lesson's "
             + "beat 2 if a lesson is ever authored on an ash-carrying enemy — Level 1's is on "
             + "Iligaw's mirror copy, which has no delay of its own — and if one is, raising this "
             + "raises how long beat 2 holds and must stay under _abilityBeatArmTimeoutSeconds.")]
    [SerializeField, Min(0f)] private float _armDelaySeconds = 1.5f;

    [Tooltip("How many target-text slots must already be filled before the ash may arm. At least "
             + "one: the clue has to have been used, and read unobscured, for its masking to "
             + "register as something an enemy did.")]
    [SerializeField, Min(0)] private int _minimumFilledSlotsToArm = 1;

    [Tooltip("Which position within its own focus word the currently needed slot must occupy for "
             + "the ash to bite, 1-based. Two, because the ash masks the word's first slot as "
             + "well as the target one: at position one the two coincide and the ash would change "
             + "nothing visible.")]
    [SerializeField, Min(1)] private int _requiredNeededSlotPositionInWord = 2;

    private Enemy _enemy;

    /// <summary>Per spawn, never per shell — see <see cref="ResetSpawnState"/>.</summary>
    private bool _armedThisSpawn;
    private bool _suppressedForIntroductionSpawn;
    private float _timeOnScreenSeconds;

    /// <summary>True once this spawn's ash has armed. A recycled shell comes back false.</summary>
    public bool IsArmedThisSpawn => _armedThisSpawn;

    /// <summary>
    /// <see cref="IIntroducibleAbility.HasFiredThisSpawn"/>. The ash IS the arming: the gust plays
    /// and the clue masks off the same flag, so "armed" and "fired" are the same instant here.
    /// </summary>
    public bool HasFiredThisSpawn => _armedThisSpawn;

    /// <summary>True while this spawn is the type's introduction spawn and must stay inert.</summary>
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>
    /// <see cref="IIntroducibleAbility.CanFireThisSpawn"/>. Always true: the ash spawns nothing and
    /// borrows nothing — it masks a slot that is already on screen, entirely from inside this
    /// component. Everything that decides whether it fires (the arm delay, the needed slot's
    /// position, the clue's state) is re-evaluated every <see cref="Tick"/> and can turn true on a
    /// later frame, so all of it belongs to the wait rather than to this precondition.
    /// </summary>
    public bool CanFireThisSpawn => true;

    /// <summary>Seconds since this spawn's ability was enabled, driven by <see cref="Tick"/>.</summary>
    public float TimeOnScreenSeconds => _timeOnScreenSeconds;

    /// <summary>
    /// True once the ash has been shown for this level attempt, so it cannot be shown twice. Reads
    /// the spawn gate where one exists, because that is what the schedule itself consults.
    /// </summary>
    public static bool AshShownThisLevel => HasAshBeenShown();

    /// <summary>
    /// True while at least one Abo ng Simula is alive, <b>armed</b>, and not suppressed as an
    /// introduction spawn. Self-healing: every call re-checks each registered controller and drops
    /// any that has been destroyed, disabled, recycled, had its flag cleared, or started dying —
    /// so the ash lifts on death and cannot outlive a spawn even if a disable callback is missed.
    /// </summary>
    public static bool IsAnyActive()
    {
        Registered.RemoveWhere(controller => !IsAshingNow(controller));
        return Registered.Count > 0;
    }

    /// <summary>
    /// Resolves the HUD art from a currently active Abo. The clue HUD remains the renderer and
    /// animation clock; the enemy contributes only its authored state definition.
    /// </summary>
    public static EnemyHudAbilityVisualDefinition GetActiveHudVisualDefinition(EnemyHudAbilityVisualId id)
    {
        Registered.RemoveWhere(controller => !IsAshingNow(controller));
        foreach (AshFirstSlotController controller in Registered)
        {
            Enemy enemy = controller._enemy != null ? controller._enemy : controller.GetComponent<Enemy>();
            EnemyHudAbilityVisualDefinition[] definitions = enemy != null && enemy.Data != null
                ? enemy.Data.hudAbilityVisuals
                : null;
            if (definitions == null)
                continue;

            for (int i = 0; i < definitions.Length; i++)
            {
                EnemyHudAbilityVisualDefinition definition = definitions[i];
                if (definition != null && definition.id == id && definition.activeSprite != null)
                    return definition;
            }
        }

        return null;
    }

    /// <summary>Test seam: forget every registration and every level latch.</summary>
    public static void ResetRegistryForTests()
    {
        Registered.Clear();
        _ashShownFallbackLatch = false;
        _cachedCoordinator = null;
    }

    /// <summary>
    /// Marks this spawn as the type's introduction spawn, where the ability is stated by the
    /// introduction card and deliberately does nothing. Called by the introduction beat rather
    /// than inferred here: "first spawn of this type" is campaign progress, not enemy state.
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        _suppressedForIntroductionSpawn = suppressed;

        // Suppression has to bite immediately even on an already-armed spawn, or a beat that
        // suppresses late would leave the clue masked with the card still on screen.
        if (suppressed)
            Registered.Remove(this);
        else if (IsAshingNow(this))
            Registered.Add(this);
    }

    /// <summary>
    /// Arms this spawn's ash. Public so the arming beat — and the trigger below — is one explicit
    /// call, and so a test can put the ability in its armed state without waiting on frames.
    /// </summary>
    public void ArmAsh()
    {
        _armedThisSpawn = true;

        if (IsAshingNow(this))
            Registered.Add(this);
    }

    /// <summary>
    /// Clears everything that is true of a <i>spawn</i> rather than of the shell. Pooled shells are
    /// reused for other enemy types, so an armed Abo that dies and comes back as a Mantsa must
    /// return unarmed and unsuppressed; otherwise the next Abo out of that shell would ash the clue
    /// with no gust and no trigger.
    /// </summary>
    public void ResetSpawnState()
    {
        _armedThisSpawn = false;
        _suppressedForIntroductionSpawn = false;
        _timeOnScreenSeconds = 0f;
        Registered.Remove(this);
    }

    /// <summary>
    /// The whole arming trigger of the Level 1 design §1 in one place: the ash is unshown, this is
    /// not the suppressed introduction spawn, the player has filled at least one slot, the Abo is
    /// alive, it has been on screen long enough, and the needed slot is its word's second symbol.
    /// The two facts only the HUD knows are passed in, so the decision can be asserted directly
    /// rather than through a scene.
    /// <para>
    /// <paramref name="filledSlots"/> is how many target-text slots are already restored;
    /// <paramref name="neededSlotPositionInWord"/> is the 1-based position of the currently needed
    /// slot inside its own focus word, or zero when nothing is needed.
    /// </para>
    /// </summary>
    public bool WantsToArm(int filledSlots, int neededSlotPositionInWord)
    {
        if (_armedThisSpawn || _suppressedForIntroductionSpawn)
            return false;
        if (!IsLiveAbo(this))
            return false;
        if (_timeOnScreenSeconds < _armDelaySeconds)
            return false;
        if (filledSlots < _minimumFilledSlotsToArm)
            return false;
        if (neededSlotPositionInWord != _requiredNeededSlotPositionInWord)
            return false;

        return !HasAshBeenShown();
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        // A spawn begins here: the shell may have been an Abo before, and none of that spawn's
        // arming survives into this one.
        ResetSpawnState();
        Registered.Add(this);
    }

    private void OnDisable()
    {
        // Reset on the way out as well as on the way in, because EditMode and pooled reuse do not
        // guarantee OnEnable runs for the next spawn on this shell.
        ResetSpawnState();
    }

    private void OnDestroy()
    {
        Registered.Remove(this);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Re-asserts this controller's registration, ages this spawn, and evaluates the arming
    /// trigger. Public so the ability can be driven without frames in tests, mirroring
    /// <see cref="GlyphCoverController.Tick"/>. EditMode never fires OnEnable, so a test drives
    /// registration through here.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (IsLiveAbo(this))
        {
            // Scaled time on purpose: the introduction cards drop the level's time scale and the
            // spawn schedule's own clock is scaled too, so "1.5 seconds on screen" means the same
            // 1.5 seconds of gameplay the rest of the pacing system is measured in.
            //
            // The cost of that choice is paid in EnemyIntroductionBeat.PlayAbilityBeat: every wait
            // in the lesson is REALTIME while this one is scaled, so at an introduction time scale
            // of 0.15 (Level 1's) the 1.5 s below takes roughly ten wall-clock seconds to accrue.
            // That beat therefore waits on IsArmedThisSpawn instead of on a fixed hold. Do not
            // switch this to unscaled time to "fix" that — it would decouple the ash from the
            // pacing clock the rest of the spawn system shares.
            _timeOnScreenSeconds += Mathf.Max(0f, deltaTime);
        }
        else if (_armedThisSpawn)
        {
            // The spawn is over — dying, disabled, or recycled. Drop its arming here as well as in
            // the enable callbacks, so a shell that is re-initialised for another Abo without a
            // fresh OnEnable cannot inherit an armed ash and mask the clue with no gust.
            ResetSpawnState();
        }

        if (IsAshingNow(this))
            Registered.Add(this);
        else
            Registered.Remove(this);

        TryArmFromTrigger();
    }

    /// <summary>
    /// Reads the two HUD-side conditions and fires if the trigger is satisfied. The HUD lookup is
    /// last of the cheap checks so an inert spawn — suppressed, too young, or already armed —
    /// costs nothing per frame.
    /// </summary>
    private void TryArmFromTrigger()
    {
        if (_armedThisSpawn || _suppressedForIntroductionSpawn)
            return;
        if (!IsLiveAbo(this) || _timeOnScreenSeconds < _armDelaySeconds)
            return;

        ActiveCluePresenter presenter = ActiveCluePresenter.Active;
        if (presenter == null)
            return;

        if (!WantsToArm(presenter.RestoredSlotCount, presenter.NeededSlotPositionInWord))
            return;

        FireAsh();
    }

    /// <summary>
    /// The arming moment: gust, arm, and open the slot gate the win condition waits on.
    ///
    /// <para>
    /// The gate opens even when no gust could play. A missing VFX reference is a wiring problem
    /// and costs the player an unattributed HUD change; refusing to arm would instead leave the
    /// final slot gated forever, and the schedule returns HoldForGate rather than completing — an
    /// unwinnable level. So the ability degrades to "silent" and says so in the log.
    /// </para>
    /// </summary>
    private void FireAsh()
    {
        if (!AshGustController.PlayGustFrom(transform.position))
        {
            DebugLogger.LogWarning(
                "AshFirstSlotController: the ash armed with no AshGustController able to play a "
                + "gust, so the clue panel crumbles with nothing to attribute it to. Wire an "
                + "AshGustController (with its VFX prefab) on the HUD/VFX root.");
        }

        ArmAsh();
        _ashShownFallbackLatch = true;

        SpawnAssignmentCoordinator coordinator = ResolveCoordinator();
        if (coordinator != null)
            coordinator.OpenGate(SpawnGateRegistry.AboAshShown);
    }

    /// <summary>
    /// The gate registry is the record of the ash having been shown, so that the fact the schedule
    /// gates on and the fact this trigger reads are the same fact and cannot disagree.
    /// </summary>
    private static bool HasAshBeenShown()
    {
        SpawnAssignmentCoordinator coordinator = ResolveCoordinator();
        if (coordinator != null)
            return coordinator.Gates.IsOpen(SpawnGateRegistry.AboAshShown);

        return _ashShownFallbackLatch;
    }

    private static SpawnAssignmentCoordinator ResolveCoordinator()
    {
        if (_cachedCoordinator != null)
            return _cachedCoordinator;

        // Inactive included: LevelFlowController may have created the coordinator on a HUD object
        // that is still being brought up when the first Abo walks on.
        _cachedCoordinator = FindFirstObjectByType<SpawnAssignmentCoordinator>(
            FindObjectsInactive.Include);
        return _cachedCoordinator;
    }

    /// <summary>
    /// This shell is currently a living Abo with the ability flag set. Says nothing about arming:
    /// it is the lifetime half of the question, shared by the trigger and the registry.
    /// </summary>
    private static bool IsLiveAbo(AshFirstSlotController controller)
    {
        if (controller == null)
            return false;
        if (!controller.isActiveAndEnabled)
            return false;

        Enemy enemy = controller._enemy != null
            ? controller._enemy
            : controller.GetComponent<Enemy>();
        if (enemy == null || enemy.IsDying)
            return false;
        if (!enemy.gameObject.activeInHierarchy)
            return false;

        EnemyDataSO data = enemy.Data;
        return data != null && data.ashesFirstSlot;
    }

    /// <summary>
    /// This controller is masking the clue right now: a living Abo whose spawn has armed and is
    /// not the suppressed introduction spawn.
    /// </summary>
    private static bool IsAshingNow(AshFirstSlotController controller)
        => IsLiveAbo(controller)
           && controller._armedThisSpawn
           && !controller._suppressedForIntroductionSpawn;

    /// <summary>
    /// Statics outlive a level in the Editor and in a player build alike, so the fallback latch
    /// and the cached coordinator are dropped on every scene load. Without this a second attempt
    /// at a level with no coordinator would start with the ash already spent.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void SubscribeLevelReset()
    {
        Registered.Clear();
        _ashShownFallbackLatch = false;
        _cachedCoordinator = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _ashShownFallbackLatch = false;
        _cachedCoordinator = null;
    }
}
