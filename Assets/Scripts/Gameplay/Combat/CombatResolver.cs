using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// Listens for OnCharacterRecognized and defeats the correct enemy.
/// This is the bridge between the recognition pipeline and the
/// enemy system. Without this, drawing does nothing.
[DisallowMultipleComponent]
public class CombatResolver : MonoBehaviour
{
    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;
    [SerializeField, Min(1)] private int _aoeDamagePerTarget = 1;
    [Header("AOE Defeat Timing")]
    [SerializeField] private bool _staggerAoeDefeats = false;
    [SerializeField] private bool _distanceWeightedDelay = true;
    [SerializeField] private Transform _baseAnchor;
    [SerializeField, Min(0f)] private float _aoeMinDelay = 0.02f;
    [SerializeField, Min(0f)] private float _aoeMaxDelay = 0.22f;
    [SerializeField, Min(0f)] private float _aoeRandomJitter = 0.06f;
    [SerializeField, Min(0f)] private float _aoeExtraRandomDelay = 0.12f;
    [SerializeField, Min(0f)] private float _aoeInitialDelayMin = 0.08f;
    [SerializeField, Min(0f)] private float _aoeInitialDelayMax = 0.2f;
    [Header("Pronunciation Timing")]
    [Tooltip("Small lead so pronunciation starts before damage resolves.")]
    [SerializeField, Min(0f)] private float _pronunciationLeadSeconds = 0.06f;
    private static CombatResolver _instance;

    /// <summary>
    /// Fallback window in which an identical characterID is treated as an echo of the same
    /// finger-lift rather than a fresh attempt. Comfortably longer than the pronunciation lead and
    /// far shorter than any real repeated draw.
    /// </summary>
    /// <remarks>
    /// SALIN-182 asks for shorter correction windows as a difficulty lever, so the live value now
    /// comes from <c>GameConfigSO.echoedRecognitionSeconds</c>. This constant remains the default
    /// and the fallback when no config is assigned, which keeps current behaviour byte-identical.
    ///
    /// Shortening this affects more than the echo guard: SALIN-135 hoisted it into
    /// HandleCharacterRecognized, so it also gates the AOE, closest-match and miss paths.
    /// </remarks>
    private const float DefaultEchoedRecognitionSeconds = 0.15f;

    [Header("Correction Window (SALIN-182)")]
    [Tooltip("Optional tuning source for the echo window. Falls back to 0.15s when unassigned.")]
    [SerializeField] private GameConfigSO _config;

    private float EchoedRecognitionSeconds =>
        _config != null ? Mathf.Max(0f, _config.echoedRecognitionSeconds) : DefaultEchoedRecognitionSeconds;

    private string _lastRecognizedCharacterId;
    private float _lastRecognizedTime = float.NegativeInfinity;

    /// <summary>Cached so a correct hit does not trigger a scene-wide type scan.</summary>
    private ActiveCluePresenter _cachedPresenter;

    // Reused per draw so multi-target resolution allocates nothing on the recognition path.
    // Valid only for the duration of one ResolveActiveClueDraw call; never hand them out.
    private readonly List<Enemy> _clueEnemyBuffer = new List<Enemy>();
    private readonly List<ClueCandidate> _clueCandidateBuffer = new List<ClueCandidate>();
    private readonly List<int> _clueTargetIndices = new List<int>();
    private readonly List<Enemy> _clueTargetBuffer = new List<Enemy>();

    // Flattened target-text slots, rebuilt per draw from the presenter's restoration state. Same
    // lifetime rule as the buffers above: valid for one resolve call, never handed out.
    private readonly List<TargetTextSlotMap.Slot> _slotBuffer = new List<TargetTextSlotMap.Slot>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += HandleCharacterRecognized;
        EnsureBaseAnchor();
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void HandleCharacterRecognized(string characterID)
    {
        if (TutorialRuntimeState.IsCombatOverrideActive || ChallengeRuntimeState.IsCombatOverrideActive)
            return;

        // Boss route — runs before AOE and closest-match. If the active boss
        // is targetable and the draw matches a required character, the boss
        // consumes the draw (Hit or Duplicate). Otherwise we fall through.
        BossController boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (boss != null && boss.IsTargetable)
        {
            BossRouteResult routed = boss.TryRouteDraw(characterID);
            if (routed != BossRouteResult.NotRouted)
                return;
        }

        // Echo gate (SALIN-135). A single finger-lift can raise OnCharacterRecognized more than
        // once inside the pronunciation-lead window; every downstream path — active clue, AOE
        // burst, closest match, and the miss branch — must treat that as one attempt so a single
        // user action produces exactly one combat and feedback response. Placed after the boss
        // route on purpose: BossController owns its own BossRouteResult.Duplicate semantics and
        // must keep seeing the raw event stream.
        if (IsEchoedRecognition(characterID))
            return;

        // Active-clue combat (SALIN-180). This gate intentionally runs after boss routing:
        // bosses are not eligible clues, but targetable bosses must retain their existing draw
        // route on a clue-enabled level.
        ActiveClueDirector clueDirector = ActiveClueDirector.Instance;
        if (clueDirector != null && clueDirector.IsClueCombatActive)
        {
            ResolveActiveClueDraw(clueDirector, characterID);
            return;
        }

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        List<Enemy> matches = tracker.FindAllWithCharacter(characterID);

        // Real-match count: decoys and bosses cannot enable an AOE burst. Decoys
        // remain on screen as their own threat; burst is a reward path for sets
        // of legitimate enemies only.
        int realMatchCount = 0;
        if (matches != null)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                Enemy m = matches[i];
                if (m == null) continue;
                if (!IsEligibleCombatTarget(m)) continue;
                if (m.IsBoss) continue;
                if (m.IsDecoy) continue;
                if (m.Data == null) continue;
                realMatchCount++;
            }
        }

        if (IsMultiKillChainEnabledForCurrentLevel()
            && realMatchCount >= _aoeThreshold)
        {
            // Snapshot to a local list because TakeDamage -> Defeat -> Unregister
            // mutates the tracker's shared buffer mid-iteration.
            var burstTargets = new List<Enemy>(matches);
            var chainTargets = new List<Enemy>(burstTargets.Count);
            int defeatedCount = 0;

            for (int i = 0; i < burstTargets.Count; i++)
            {
                Enemy candidate = burstTargets[i];
                if (candidate == null) continue;
                if (!IsEligibleCombatTarget(candidate)) continue;
                if (candidate.IsBoss) continue;
                if (candidate.IsDecoy) continue;
                if (candidate.Data == null) continue;

                chainTargets.Add(candidate);
                EventBus.RaiseEnemyTargeted(candidate);
                defeatedCount++;
            }

            if (chainTargets.Count > 0)
            {
                BaybayinCharacterSO pronunciationCharacter = chainTargets[0] != null ? chainTargets[0].Character : null;
                if (pronunciationCharacter != null)
                    EventBus.RaisePronunciationRequested(pronunciationCharacter);

                StartCoroutine(ApplyAoeDefeatAfterPronunciationLead(chainTargets));
            }

            if (defeatedCount > 0)
            {
                EventBus.RaiseAOETriggered(defeatedCount);
                DebugLogger.Log($"CombatResolver: AOE burst defeated {defeatedCount} for {characterID}");
            }

            return;
        }

        Enemy closestTarget = FindClosestEligibleMatch(matches);
        if (closestTarget == null)
        {
            EventBus.RaiseDrawingMissed();

            // Same board-describing prompt the clue path gets. A miss is a statement about what is on
            // screen either way, and giving the legacy path its own wording would be two strings to
            // keep in step for no gain.
            PublishTextRelation(DrawTextRelation.NoCarrier, characterID, -1, -1, null);

            DebugLogger.Log(
                $"CombatResolver: No enemy carries "
                + $"{characterID} -- miss");
            return;
        }

        ResolveMatchedEnemy(closestTarget, characterID);
    }

    /// <summary>
    /// True when this recognition repeats the previous one inside the echo window, meaning it
    /// is the same finger-lift arriving twice rather than a second attempt.
    /// </summary>
    private bool IsEchoedRecognition(string characterID)
    {
        float now = Time.unscaledTime;
        bool echoed = characterID == _lastRecognizedCharacterId
                      && now - _lastRecognizedTime < EchoedRecognitionSeconds;

        _lastRecognizedCharacterId = characterID;
        _lastRecognizedTime = now;
        return echoed;
    }

    /// <summary>
    /// Returns the active clue presenter, re-scanning only when the cached one is gone.
    /// Uses Unity's null semantics, so a destroyed presenter is refreshed rather than kept.
    /// </summary>
    private ActiveCluePresenter ResolvePresenter()
    {
        if (_cachedPresenter == null)
            _cachedPresenter = FindFirstObjectByType<ActiveCluePresenter>();

        return _cachedPresenter;
    }

    /// <summary>
    /// Active-clue resolution. A correct draw resolves against ANY eligible on-screen enemy
    /// carrying the drawn glyph, not only the marked clue — the mark is a suggestion of what to
    /// draw next, no longer the sole legal target. A glyph no on-screen enemy carries is still a
    /// miss with corrective feedback and no language progress.
    /// </summary>
    /// <remarks>
    /// Targetability widened; lethality did not. How many carriers die still comes from the
    /// level author's <c>multiKillChainEnabled</c> and this resolver's AOE threshold, so Level 1
    /// (chain off) keeps one draw to one kill.
    ///
    /// Eligibility comes from <see cref="ActiveClueDirector.IsClueTargetable"/>, which now ADMITS
    /// Iligaw's false copies: a copy's glyph is legible on a body on screen, so refusing it made a
    /// correct drawing resolve as a miss and the miss prompt then denied that anything out there
    /// carried a symbol the player could read on a walking enemy. A copy therefore competes for the
    /// kill on the same closest-to-base terms as any other carrier, and the consequences are split in
    /// two: the director withholds the word's credit, and this method reclassifies the draw as
    /// <see cref="DrawTextRelation.FalseCopyShattered"/> so the HUD says a copy fell rather than
    /// congratulating a fill that never happened.
    /// </remarks>
    private void ResolveActiveClueDraw(ActiveClueDirector director, string characterID)
    {
        // Echo suppression lives in HandleCharacterRecognized (SALIN-135) so the legacy
        // AOE/closest-match paths get the same single-fire guarantee this path already had.
        Enemy clue = director.CurrentClue;

        CollectClueTargets(characterID);
        bool matchesClue = _clueTargetBuffer.Count > 0;

        if (!matchesClue)
        {
            EventBus.RaiseDrawingMissed();
            PublishTextRelation(DrawTextRelation.NoCarrier, characterID, -1, -1, null);

            if (clue != null && clue.GlyphBadge != null)
                clue.GlyphBadge.PlayFailFlash();

            if (clue != null && clue.Character != null && ProgressManager.Instance != null
                && !string.IsNullOrEmpty(clue.Character.stableId))
            {
                ProgressManager.Instance.LevelEvidence.RecordAttempt(
                    contentId: clue.Character.stableId,
                    contentKind: LearningContentKind.Symbol,
                    dimension: MasteryDimension.Form,
                    success: false,
                    answerWasVisible: false);
            }

            DebugLogger.Log($"CombatResolver: no on-screen enemy carries {characterID} -- miss");
            return;
        }

        Enemy primaryTarget = _clueTargetBuffer[0];

        // Classified BEFORE the clue is consumed, because consuming it restores a slot and moves the
        // cursor. Reading the relation afterwards would report every successful fill as
        // AlreadyFilled -- the draw would have filled the slot it is being compared against.
        DrawTextRelation relation = ClassifyAgainstTargetText(
            characterID, out int relationSlotIndex, out int cursorSlotIndex);

        // ...and then overridden by the BODY, because the glyph alone cannot see the deception. A
        // false copy wears a real symbol of the real word, so classifying the drawn id against the
        // text reports a copy's death as a slot fill — a claim of progress the player did not make,
        // rendered identically to one they did. The whole point of the beat is that the copy fell and
        // the real one walked on, and a fill-shaped response teaches the opposite.
        //
        // The check reads _clueTargetBuffer[0] rather than asking the director, and the two cannot
        // disagree: ActiveClueDirector.FindFalseCopyHoldingTheDraw resolves the same question with
        // DrawTargetResolver.SelectSingleIndex over a snapshot taken in this same frame under the
        // same IsClueTargetable predicate, which is by construction this buffer's head. So the
        // relation the HUD words its prompt from and the credit the director withholds are decided
        // about one body, not two.
        //
        // The head is also the only place a copy can be: DrawTargetResolver keeps decoys out of the
        // chain tail, so a chained draw's extra victims are real carriers by construction and there
        // is no second body to interrogate here.
        //
        // Unconditional on a copy kill, not limited to the cursor case. A copy took nothing away when
        // the drawn syllable was needed later or already restored — but those prompts both explain a
        // non-advance by ORDER or by DUPLICATION, and both presume the body that fell was real. Told
        // that after shattering a copy, the player attributes the outcome to the wrong cause and
        // learns nothing about the copy at all.
        if (primaryTarget != null && primaryTarget.IsDecoy)
            relation = DrawTextRelation.FalseCopyShattered;

        PublishTextRelation(relation, characterID, relationSlotIndex, cursorSlotIndex, primaryTarget);

        // Objective credit follows the GLYPH, not the enemy instance. Once any carrier is a legal
        // target, the closest carrier of the clue's glyph is often not the marked enemy itself;
        // keying credit to instance identity would silently drop progress for a draw that was
        // correct. Consumed before the pronunciation-lead coroutine because recognition can fire
        // twice inside that window and once-ness is the director's guard.
        bool matchesClueGlyph = clue != null
                                && clue.Character != null
                                && clue.Character.characterID == characterID;
        bool creditsObjective = matchesClueGlyph && director.TryConsumeClue(clue);

        if (primaryTarget != null && primaryTarget.Character != null)
            EventBus.RaisePronunciationRequested(primaryTarget.Character);

        if (_clueTargetBuffer.Count == 1)
        {
            StartCoroutine(ResolveMatchedEnemyAfterPronunciationLead(primaryTarget, characterID));
        }
        else
        {
            // Snapshot: TakeDamage -> Defeat -> Unregister mutates the tracker mid-flight, and
            // the buffer is reused by the next draw.
            var chainTargets = new List<Enemy>(_clueTargetBuffer);
            for (int i = 0; i < chainTargets.Count; i++)
                EventBus.RaiseEnemyTargeted(chainTargets[i]);

            StartCoroutine(ApplyAoeDefeatAfterPronunciationLead(chainTargets));
            EventBus.RaiseAOETriggered(chainTargets.Count);
        }

        if (creditsObjective && ProgressManager.Instance != null
            && !string.IsNullOrEmpty(clue.Character.stableId))
        {
            ActiveCluePresenter presenter = ResolvePresenter();
            ProgressManager.Instance.LevelEvidence.RecordAttempt(
                contentId: clue.Character.stableId,
                contentKind: LearningContentKind.Symbol,
                dimension: MasteryDimension.Form,
                success: true,
                answerWasVisible: presenter != null && presenter.AnswerWasVisible);
        }

        DebugLogger.Log(
            $"CombatResolver: Active-clue hit {characterID} on {_clueTargetBuffer.Count} target(s) "
            + $"(credits objective: {creditsObjective}, text relation: {relation})");
    }

    /// <summary>
    /// Where the drawn syllable sits in the target text the player is restoring.
    /// </summary>
    /// <remarks>
    /// Built from the HUD's restoration state rather than from the spawn director's slot list. The
    /// director's list is private to its coordinator and exists only on levels that route spawns
    /// through it, whereas restoration state exists wherever a target text does — so keying off it
    /// means a level can gain these feedback states without also adopting the schedule.
    /// </remarks>
    private DrawTextRelation ClassifyAgainstTargetText(
        string characterID, out int slotIndex, out int cursorIndex)
    {
        slotIndex = -1;
        cursorIndex = -1;

        ActiveCluePresenter presenter = ResolvePresenter();
        ActiveClueRestorationState state = presenter != null ? presenter.RestorationState : null;
        if (state == null)
            return DrawTextRelation.Unknown;

        TargetTextSlotMap.Build(state.FocusWords, state.IsSlotRestored, _slotBuffer);
        if (_slotBuffer.Count == 0)
            return DrawTextRelation.Unknown;

        return TargetTextSlotMap.Classify(_slotBuffer, characterID, out slotIndex, out cursorIndex);
    }

    /// <summary>
    /// Hands one draw's text relation to the feedback HUD, resolving the drawn glyph to content so
    /// the presenter has something to render even on a miss, where no enemy carried it.
    /// </summary>
    private static void PublishTextRelation(
        DrawTextRelation relation,
        string characterID,
        int slotIndex,
        int cursorIndex,
        Enemy resolvedTarget)
    {
        DrawFeedbackSignals.RaiseTextRelationResolved(new DrawFeedbackReport
        {
            Relation = relation,
            DrawnCharacterId = characterID,
            DrawnCharacter = ResolveDrawnCharacter(characterID, resolvedTarget),
            SlotIndex = slotIndex,
            CursorSlotIndex = cursorIndex,
            ResolvedTarget = resolvedTarget,
        });
    }

    /// <summary>
    /// The drawn glyph as content. Prefers the killed enemy's own character, and falls back to the
    /// level's authored roster for the miss case, where by definition no enemy carries it.
    /// </summary>
    private static BaybayinCharacterSO ResolveDrawnCharacter(string characterID, Enemy resolvedTarget)
    {
        if (resolvedTarget != null && resolvedTarget.Character != null)
            return resolvedTarget.Character;

        if (string.IsNullOrEmpty(characterID))
            return null;

        // The level roster rather than a global registry: the miss prompt names a glyph the player
        // just drew on a level that authored it, and a roster lookup needs no new asset reference to
        // wire and cannot resolve a character this level was never meant to show.
        List<BaybayinCharacterSO> roster = GameManager.CurrentLevelConfig?.allowedCharacters;
        if (roster == null)
            return null;

        for (int i = 0; i < roster.Count; i++)
        {
            BaybayinCharacterSO character = roster[i];
            if (character != null && character.characterID == characterID)
                return character;
        }

        return null;
    }

    /// <summary>
    /// Fills <see cref="_clueTargetBuffer"/> with every enemy this draw resolves against, closest
    /// to the base first. Empty means no eligible on-screen enemy carries the glyph — a miss.
    /// </summary>
    private void CollectClueTargets(string characterID)
    {
        _clueTargetBuffer.Clear();
        _clueCandidateBuffer.Clear();
        _clueTargetIndices.Clear();

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        tracker.FillActiveEnemiesSnapshot(_clueEnemyBuffer);

        for (int i = 0; i < _clueEnemyBuffer.Count; i++)
        {
            Enemy enemy = _clueEnemyBuffer[i];

            // The copy flag is carried alongside eligibility, not folded into it. A copy IS a legal
            // target — it has to be, or its glyph reads as a miss — but it is not a legitimate member
            // of a multi-kill set, and the resolver cannot make that distinction from a board it is
            // only told is eligible. Left unpassed, a copy pads the chain threshold and then dies as
            // one of the chain's victims, both of which dress a copy's death up as part of a reward.
            _clueCandidateBuffer.Add(new ClueCandidate(
                enemy != null && enemy.Character != null ? enemy.Character.characterID : null,
                enemy != null ? enemy.transform.position.y : float.MaxValue,
                enemy != null ? enemy.SpawnSequence : long.MaxValue,
                ActiveClueDirector.IsClueTargetable(enemy),
                enemy != null && enemy.IsDecoy));
        }

        DrawTargetResolver.SelectTargets(
            _clueCandidateBuffer,
            characterID,
            IsMultiKillChainEnabledForCurrentLevel(),
            _aoeThreshold,
            _clueTargetIndices);

        for (int i = 0; i < _clueTargetIndices.Count; i++)
            _clueTargetBuffer.Add(_clueEnemyBuffer[_clueTargetIndices[i]]);
    }

    /// <summary>
    /// The one carrier a non-chaining draw kills: the enemy closest to the base, ties broken by
    /// spawn sequence.
    /// </summary>
    /// <remarks>
    /// This used to be a bare running minimum on Y with no tiebreak, which is not the same thing as
    /// deterministic. Two enemies spawned on the same row do not have bit-identical Y, but they are
    /// within float noise of each other, and which one won depended on the order
    /// ActiveEnemyTracker happened to hand the list over — so the pair moment where two bodies carry
    /// the same glyph resolved differently between runs, and the deception beat where one of them is
    /// a decoy became a coin flip the player cannot read.
    ///
    /// Rather than restate a tiebreak here, this now routes through <see cref="ActiveClueSelector"/>,
    /// the same policy <see cref="DrawTargetResolver"/> gives the active-clue path. One rule, one
    /// place: the two paths can no longer disagree about which enemy a draw kills, and the rule stays
    /// covered by that type's EditMode tests instead of needing a scene to exercise.
    ///
    /// No glyph filter is applied here because there is nothing left to filter — every entry arrives
    /// from <c>ActiveEnemyTracker.FindAllWithCharacter</c>, which already matched the drawn id
    /// exactly. Eligibility is the only remaining question.
    /// </remarks>
    private static Enemy FindClosestEligibleMatch(List<Enemy> matches)
    {
        if (matches == null || matches.Count == 0)
            return null;

        var candidates = new List<ClueCandidate>(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            Enemy candidate = matches[i];

            // Populated even though this path selects a single target and never chains: the flag is
            // part of describing the body, and a candidate that silently reports "not a copy" about
            // an enemy whose copy-ness is known right here is a trap for whoever widens this path
            // next. ActiveClueSelector does not read it — which carrier dies stays distance and spawn
            // order, with no "if decoy" branch in the policy.
            candidates.Add(new ClueCandidate(
                candidate != null && candidate.Character != null ? candidate.Character.characterID : null,
                candidate != null ? candidate.transform.position.y : float.MaxValue,
                candidate != null ? candidate.SpawnSequence : long.MaxValue,
                IsEligibleCombatTarget(candidate),
                candidate != null && candidate.IsDecoy));
        }

        int index = ActiveClueSelector.SelectIndex(candidates);
        return index >= 0 ? matches[index] : null;
    }

    private static bool IsEligibleCombatTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        if (enemy.IsDying)
            return false;

        if (enemy.Data == null)
            return false;

        if (enemy.Data.isPhaser && !enemy.IsPhaserVisible)
            return false;

        // SALIN-286: an ability is holding this enemy unresolvable (Bakod shields everything behind
        // it). Tested here in the method, not at any one call site, because this method has five of
        // them: the single-target path (:297), the AOE count and burst (:139, :160), and :375/:453.
        // A filter placed at the single-target call site alone would leak through the AOE and chain
        // paths with no failure anywhere. Neutral by design — Enemy.IsResolutionBlocked names the
        // effect, not the ability — so a second block source needs no change here.
        if (enemy.IsResolutionBlocked)
            return false;

        // Bosses are excluded from AOE counts/bursts, but may still use the
        // single-target resolution path for boss-specific combat tuning.
        return true;
    }

    private static bool IsMultiKillChainEnabledForCurrentLevel()
        => GameManager.CurrentLevelConfig?.multiKillChainEnabled ?? true;

    private static void ResolveMatchedEnemy(Enemy target, string characterID)
    {
        if (target == null)
            return;

        if (target.IsDecoy)
        {
            EventBus.RaiseBaseHit(1);
            target.ApplyDecoyPenalty();

            RecognitionLogger.LogOutcome(
                outcome: "decoy_penalty",
                recognizedCharacterID: characterID,
                intendedCharacterID: TestSessionController.IntendedCharacterID);

            DebugLogger.Log($"CombatResolver: Decoy penalty on {characterID}");
        }
        else
        {
            if (target.Character != null)
                EventBus.RaisePronunciationRequested(target.Character);

            if (_instance != null)
            {
                _instance.StartCoroutine(_instance.ResolveMatchedEnemyAfterPronunciationLead(target, characterID));
            }
            else
            {
                EventBus.RaiseEnemyTargeted(target);
                EventBus.RaiseSingleAttackHit(target);
                target.TakeDamage(1);
                DebugLogger.Log($"CombatResolver: Hit {characterID}");
            }
        }
    }

    private IEnumerator ResolveMatchedEnemyAfterPronunciationLead(Enemy target, string characterID)
    {
        float delay = Mathf.Max(0f, _pronunciationLeadSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsEligibleCombatTarget(target))
            yield break;

        EventBus.RaiseEnemyTargeted(target);
        EventBus.RaiseSingleAttackHit(target);
        target.TakeDamage(1);
        DebugLogger.Log($"CombatResolver: Hit {characterID}");
    }

    private IEnumerator ApplyAoeDefeatAfterPronunciationLead(List<Enemy> targets)
    {
        float delay = Mathf.Max(0f, _pronunciationLeadSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // Broadcast once for AOE-wide systems (audio/UI counters) at execution time.
        ApplyAoeDefeat(targets);
        EventBus.RaiseChainAttackHit(targets);
    }

    private void ApplyAoeDefeat(List<Enemy> targets)
    {
        if (targets == null || targets.Count == 0)
            return;

        if (!_staggerAoeDefeats)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                EventBus.RaiseChainAttackStep(targets[i]);
                DefeatTargetImmediate(targets[i], _aoeDamagePerTarget);
            }
            return;
        }

        EnsureBaseAnchor();
        float minDistance = float.MaxValue;
        float maxDistance = float.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null)
                continue;

            float distance = GetDistanceToBase(enemy.transform.position);
            if (distance < minDistance) minDistance = distance;
            if (distance > maxDistance) maxDistance = distance;
        }

        if (minDistance == float.MaxValue)
        {
            minDistance = 0f;
            maxDistance = 0f;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null)
                continue;

            float delay = ComputeAoeDelay(enemy.transform.position, minDistance, maxDistance);
            StartCoroutine(DefeatTargetAfterDelay(enemy, delay));
        }
    }

    private IEnumerator DefeatTargetAfterDelay(Enemy target, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        EventBus.RaiseChainAttackStep(target);
        DefeatTargetImmediate(target, _aoeDamagePerTarget);
    }

    private static void DefeatTargetImmediate(Enemy target, int damage)
    {
        if (!IsEligibleCombatTarget(target))
            return;

        if (target.Data == null)
            return;

        target.TakeDamage(Mathf.Max(1, damage));
    }

    private float ComputeAoeDelay(Vector3 enemyPosition, float minDistance, float maxDistance)
    {
        float initialMin = Mathf.Min(_aoeInitialDelayMin, _aoeInitialDelayMax);
        float initialMax = Mathf.Max(_aoeInitialDelayMin, _aoeInitialDelayMax);
        float initialDelay = initialMax > 0f ? Random.Range(initialMin, initialMax) : 0f;

        float spanDelay = Mathf.Max(0f, _aoeMaxDelay - _aoeMinDelay);
        float weightedDelay = _aoeMinDelay;

        if (_distanceWeightedDelay && maxDistance - minDistance > 0.0001f)
        {
            float distance = GetDistanceToBase(enemyPosition);
            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            weightedDelay = _aoeMinDelay + (spanDelay * t);
        }
        else if (spanDelay > 0f)
        {
            weightedDelay = Random.Range(_aoeMinDelay, _aoeMaxDelay);
        }

        float jitter = _aoeRandomJitter > 0f ? Random.Range(0f, _aoeRandomJitter) : 0f;
        float extraRandom = _aoeExtraRandomDelay > 0f ? Random.Range(0f, _aoeExtraRandomDelay) : 0f;
        return initialDelay + weightedDelay + jitter + extraRandom;
    }

    private float GetDistanceToBase(Vector3 enemyPosition)
    {
        if (_baseAnchor == null)
            return enemyPosition.magnitude;

        return Vector3.Distance(enemyPosition, _baseAnchor.position);
    }

    private void EnsureBaseAnchor()
    {
        if (_baseAnchor != null)
            return;

        GameObject baseObject = GameObject.FindGameObjectWithTag("PlayerBase");
        if (baseObject != null)
            _baseAnchor = baseObject.transform;
    }
}
