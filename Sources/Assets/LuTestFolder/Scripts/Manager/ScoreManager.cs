using System.Collections.Generic;
using UnityEngine;

public enum JokBoType
{
    ThreeGwang138 = 0,      // 구 1/3/8 광땡 타입. 기본 프로필에서는 비활성.
    ThreeGwangAny = 1,      // 기본 규칙: 효과로 부여된 광까지 포함해 광 3개
    SamDong = 2,            // 삼동
    Yang = 3,               // 양
    Eum = 4,                // 음
    TwoGwang = 5,           // 기본 규칙: 효과로 부여된 광까지 포함해 광 2개
    BbingPae = 6,           // 삥패
    SaPae = 7,              // 사패
    JangDdaeng = 8,         // 장땡
    Ddaeng = 9,             // 땡
    Mangtong = 10,          // 망통
    Ggeut = 11,             // 끗
    None = 12,

    // 새 족보를 코드 수정 없이 실험하기 위한 예비 슬롯입니다.
    // 기존 enum 숫자를 바꾸지 않기 위해 100번대부터 시작합니다.
    Custom_01 = 100,
    Custom_02 = 101,
    Custom_03 = 102,
    Custom_04 = 103,
    Custom_05 = 104,
    Custom_06 = 105,
    Custom_07 = 106,
    Custom_08 = 107,
    Custom_09 = 108,
    Custom_10 = 109
}

public class ScorePreviewCardStep
{
    public CardController sourceCard;
    public int chipValue;
}

public class ScorePreviewData
{
    public JokBoType jokbo;
    public string jokboDisplayOverride;
    public long jokboBonusChip;
    public long multiplier;
    public long finalTotalScore;
    public long artifactFlatScoreBonus;
    public long finalTargetScore; // Presentation prediction only; committed by OnHandPlay.
    public long finalChipValue;
    public long finalMultiplier;
    public bool forceFinalScoreDisplay;
    public bool allowZeroMultiplierDisplay;
    public List<ScorePreviewCardStep> cardSteps = new List<ScorePreviewCardStep>();
    public List<CardController> jokboCards = new List<CardController>();
    public List<ArtifactScoreStep> artifactSteps = new List<ArtifactScoreStep>();
    public List<ScoreTimelineStep> scoreSteps = new List<ScoreTimelineStep>();
}

public struct ScoreCalculationResult
{
    public JokBoType jokbo;
    public long totalScore;
    public long multiplier;
    public long finalChipValue;
    public List<ArtifactScoreStep> artifactSteps;
    public List<ScoreTimelineStep> scoreSteps;

    public ScoreCalculationResult(
        JokBoType jokbo,
        long totalScore,
        long multiplier,
        long finalChipValue,
        List<ArtifactScoreStep> artifactSteps,
        List<ScoreTimelineStep> scoreSteps = null)
    {
        this.jokbo = jokbo;
        this.totalScore = totalScore;
        this.multiplier = multiplier;
        this.finalChipValue = finalChipValue;
        this.artifactSteps = artifactSteps ?? new List<ArtifactScoreStep>();
        this.scoreSteps = scoreSteps ?? new List<ScoreTimelineStep>();
    }
}
public class ScoreManager : MonoBehaviour
{
    private const double GwangEnhancementMultiplier = 1.6d;

    public static ScoreManager Instance { get; private set; }

    [Header("족보 규칙")]
    [Tooltip("비워두면 Resources/DefaultJokboRuleProfile 또는 코드 내 기본 규칙을 사용합니다.")]
    [SerializeField] private JokboRuleProfile jokboRuleProfile;

    private readonly ScoreCalculationContext scoreContext = new ScoreCalculationContext();
    private readonly Dictionary<CardController, BaseCardSO> copiedOriginalCardDatas = new Dictionary<CardController, BaseCardSO>(3);
    private readonly Dictionary<CardController, CardSO> copiedScoreCardDatas = new Dictionary<CardController, CardSO>(3);
    private readonly List<CardSO> jokboCardScratch = new List<CardSO>(3);
    private readonly List<CardController> jokboControllerScratch = new List<CardController>(3);
    private readonly List<CardController> jokboMatchedControllerScratch = new List<CardController>(3);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        RestoreTemporaryCardData();
        if (Instance == this)
            Instance = null;
    }

    public JokboRuleProfile ActiveJokboRuleProfile => ResolveJokboRuleProfile();
    public IReadOnlyList<CardController> CurrentJokboMatchedCards => scoreContext.JokboMatchedCards;

    public string GetJokboDisplayName(JokBoType jokboType, string languageCode = null)
    {
        return ResolveJokboRuleProfile().GetDisplayName(jokboType, languageCode);
    }

    public string GetLeveledJokboDisplayName(JokBoType jokboType, string languageCode = null, bool useRichTextColor = true)
    {
        if (jokboType == JokBoType.None)
            return "-";

        int level = GetJokboPlanetLevel(jokboType);
        string levelLabel = $"Lv.{level} ";
        if (useRichTextColor)
            levelLabel = $"<color=#{GetJokboLevelColorHex(level)}>{levelLabel}</color>";

        return $"{levelLabel} {GetJokboDisplayName(jokboType, languageCode)}";
    }

    public int GetJokboPlanetLevel(JokBoType jokboType)
    {
        return TryGetPlanetStats(jokboType, out PlanetStats stats) ? Mathf.Max(1, stats.level) : 1;
    }

    private static string GetJokboLevelColorHex(int level)
    {
        if (level >= 8)
            return "FF4A3D";
        if (level >= 6)
            return "FFD166";
        if (level >= 4)
            return "B98CFF";
        if (level >= 2)
            return "80D9FF";

        return "9A9A9A";
    }

    public void SetJokboRuleProfile(JokboRuleProfile profile)
    {
        jokboRuleProfile = profile;
    }

    public ScorePreviewData BuildScorePreviewData(List<CardController> selectedCards)
    {
        ScorePreviewData previewData = new ScorePreviewData();

        if (selectedCards == null || selectedCards.Count == 0)
        {
            previewData.jokbo = JokBoType.None;
            previewData.multiplier = 1;
            return previewData;
        }

        scoreContext.Reset(selectedCards);
        try
        {
            PrepareTemporaryCardDataForJokbo(scoreContext);
            var (jokbo, bonus, multi) = EvaluateJokbo(selectedCards, jokboMatchedControllerScratch, previewData.jokboCards);
            // 칩 가산은 족보 핵심 카드만, 하이라이트는 presentation 카드 기준으로 분리합니다.
            BuildPreviewCardSteps(previewData,
                ShouldScoreAllSubmittedCards() ? selectedCards : jokboMatchedControllerScratch, selectedCards);
            previewData.jokbo = jokbo;
            previewData.jokboBonusChip = ScoreSafeMath.ClampScore(bonus);
            previewData.multiplier = ScoreSafeMath.ClampScore(multi);
            ApplyPlanetBonusToPreview(previewData);
            return previewData;
        }
        finally
        {
            RestoreTemporaryCardData();
        }
    }

    public ScoreCalculationResult CalculateTotalScore(List<CardController> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count == 0) return new ScoreCalculationResult(JokBoType.None, 0, 1, 0, null);

        ScoreAuditSnapshot scoreAudit = ScoreAuditStore.Begin(selectedCards);
        scoreContext.Reset(selectedCards);

        try
        {
            PrepareTemporaryCardDataForJokbo(scoreContext);
            ApplyPreJokboSelectionDebuff(scoreContext);
            scoreAudit?.AddStep(GameText.Get("game.ScoreManager.CalculateTotalScore.031bf95933", "보스"), GameText.Get("game.ScoreManager.CalculateTotalScore.1995db8f70", "족보 전 저주"), 0, 1d, GetCurrentDebuffAuditLabel());

            EvaluateJokbo(scoreContext);
            ApplyPlanetBonus(scoreContext);
            // 족보 판정 후에는 실제 탈을 돌려놓되 복사한 패의 점수/강화는 이번 계산이 끝날 때까지 보관합니다.
            RestoreTemporaryCardData(keepScoreCopies: true);
            MarkBossIgnoredSubmittedCards(scoreContext);
            scoreContext.RebuildPlayedCardData();
            scoreContext.CaptureCombatState();

            BuildInitialJokboScoreStep(scoreContext);
            scoreAudit?.AddStep(
                GameText.Get("game.ScoreManager.CalculateTotalScore.5eed50c2fd", "족보"),
                GetLeveledJokboDisplayName(scoreContext.Jokbo, null, false),
                scoreContext.ChipValue,
                scoreContext.Multiplier,
                GameText.Get("game.ScoreManager.CalculateTotalScore.af872a211b", "족보 칩 +{value0}, 행성 칩 +{value1}, 족보/행성/관 배수 {value2}", new { value0 = scoreContext.JokboBonusChip, value1 = scoreContext.PlanetBonusChip, value2 = $"{scoreContext.Multiplier:0.###}" }));

            ApplyDiscardPileSetEnhancements(scoreContext);
            ApplySubmittedCardsInOrder(scoreContext);
            ApplyHorongbulReplay(scoreContext);
            scoreAudit?.AddStep(
                GameText.Get("game.ScoreManager.CalculateTotalScore.a432aac9b4", "패/탈"),
                GameText.Get("game.ScoreManager.CalculateTotalScore.d17e36b053", "제출 순서 발동 후"),
                scoreContext.ChipValue,
                scoreContext.Multiplier,
                GameText.Get("game.ScoreManager.CalculateTotalScore.d785a2f305", "족보 포함 일반 패와 제출 탈를 제출 순서대로 발동"));

            ApplyBossDebuffOnce(scoreContext);
            scoreAudit?.AddStep(GameText.Get("game.ScoreManager.CalculateTotalScore.031bf95933", "보스"), GameText.Get("game.ScoreManager.CalculateTotalScore.b73bc33508", "최종 보스 저주"), scoreContext.ChipValue, scoreContext.Multiplier, GetCurrentDebuffAuditLabel());

            ApplyGeneralArtifactScoreEffects(scoreContext);
            AppendArtifactAuditSteps(scoreAudit, 0);
            scoreAudit?.AddStep(
                GameText.Get("game.ScoreManager.CalculateTotalScore.7bada8867d", "기물"),
                GameText.Get("game.ScoreManager.CalculateTotalScore.9464fe61f8", "일반 기물 발동 후"),
                scoreContext.ChipValue,
                scoreContext.Multiplier,
                GameText.Get("game.ScoreManager.CalculateTotalScore.d47dc7885b", "패 조건형을 제외한 기물을 배치 순서대로 발동"));

            ApplyHandAndDiscardPileEffects(scoreContext);
            ApplyDeferredLionMasks(scoreContext);
            scoreAudit?.AddStep(
                GameText.Get("game.ScoreManager.CalculateTotalScore.9db1637d67", "손패/저승"),
                GameText.Get("game.ScoreManager.CalculateTotalScore.5f5a655c31", "손패 효과 발동 후"),
                scoreContext.ChipValue,
                scoreContext.Multiplier,
                GameText.Get("game.ScoreManager.CalculateTotalScore.9d58a1ca8b", "손패 패시브와 손패/저승 탈 효과 발동"));

            if (ApplyUnavailableJokboFinalLock(scoreContext))
            {
                scoreAudit?.AddStep(GameText.Get("game.ScoreManager.CalculateTotalScore.031bf95933", "보스"), GameText.Get("game.ScoreManager.CalculateTotalScore.3b1def27ce", "사용 불가 족보 최종 보정"), scoreContext.ChipValue, scoreContext.Multiplier, GetCurrentDebuffAuditLabel());
            }
            scoreAudit?.AddStep(GameText.Get("game.ScoreManager.CalculateTotalScore.299c463a05", "최종"), GameText.Get("game.ScoreManager.CalculateTotalScore.3ff056b64d", "최종 점수"), scoreContext.ChipValue, scoreContext.Multiplier, GameText.Get("game.ScoreManager.CalculateTotalScore.144c7e5266", "최종 점수 = 칩 x 배수"));

            long total = ScoreSafeMath.MultiplyScore(scoreContext.ChipValue, scoreContext.Multiplier);
            ScoreAuditStore.Publish(scoreAudit);

            GameLog.Info($"Jokbo : {scoreContext.Jokbo}, BaseScore : {scoreContext.BaseScore} , Jokbo BonusScore : {scoreContext.JokboBonusChip}" +
                $" Planet Bonus Chip {scoreContext.PlanetBonusChip} , Planet Bonus Multi {scoreContext.PlanetBonusMultiplier} " +
                $" MultiPlier : {scoreContext.Multiplier} , Total Score : {total}");

            return new ScoreCalculationResult(
                scoreContext.Jokbo,
                total,
                ScoreSafeMath.ToLongMultiplier(scoreContext.Multiplier),
                scoreContext.ChipValue,
                new List<ArtifactScoreStep>(scoreContext.ArtifactScoreSteps),
                new List<ScoreTimelineStep>(scoreContext.ScoreTimelineSteps));
        }
        finally
        {
            // 점수 계산 중 예외가 나도 임시 SO와 컨트롤러의 원본 참조를 정리합니다.
            RestoreTemporaryCardData();
        }
    }

    private void BuildInitialJokboScoreStep(ScoreCalculationContext context)
    {
        if (context == null)
            return;

        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        double roundDiscardBonus = CombatManager.instance != null
            ? CombatManager.instance.roundDiscardBonusMultiplier
            : 0;

        context.ChipValue = ScoreSafeMath.AddScore(context.JokboBonusChip, context.PlanetBonusChip);
        context.Multiplier = ScoreSafeMath.AddMultiplier(
            ScoreSafeMath.AddMultiplier(context.JokboMultiplier, context.PlanetBonusMultiplier),
            roundDiscardBonus);
        RecordScoreStep(
            context,
            ScoreTimelineStepKind.Jokbo,
            GetLeveledJokboDisplayName(context.Jokbo),
            null,
            beforeChips,
            beforeMultiplier,
            ScoreMultiplierChangeType.Additive,
            null,
            context.Jokbo,
            true);
    }

    private void ApplyDiscardPileSetEnhancements(ScoreCalculationContext context)
    {
        if (context == null)
            return;

        int hongDanDiscardCount = 0;
        int chodanDiscardCount = 0;
        int cheongDanDiscardCount = 0;

        if (CardManager_2.Instance != null && CardManager_2.Instance.dropCardBuffer != null)
        {
            foreach (var dropped in CardManager_2.Instance.dropCardBuffer)
            {
                if (dropped is CardSO droppedCardSO)
                {
                    if (droppedCardSO.currentDan == DanEnhancement.HongDan) hongDanDiscardCount++;
                    if (droppedCardSO.currentDan == DanEnhancement.ChoDan) chodanDiscardCount++;
                    if (droppedCardSO.currentDan == DanEnhancement.CheongDan) cheongDanDiscardCount++;
                }
            }
        }

        if (chodanDiscardCount >= 3)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 50);
            RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyDiscardPileSetEnhancements.d7ee3eec58", "저승 초단 세트"), null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
        }

        if (hongDanDiscardCount >= 3)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
            RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyDiscardPileSetEnhancements.0884d78676", "저승 홍단 세트"), null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Multiplicative, appliedMultiplierFactor: 1.5d);
        }

        if (cheongDanDiscardCount >= 3)
        {
            GameLog.Info("[강화 확인] 저승 청단 세트 조건 충족. 현재 점수 효과는 없습니다.");
        }
    }

    private void ApplySubmittedCardsInOrder(ScoreCalculationContext context)
    {
        if (context == null || context.SelectedCards == null)
            return;

        for (int i = 0; i < context.SelectedCards.Count; i++)
        {
            CardController card = context.SelectedCards[i];
            if (card == null || card.cardData == null)
                continue;

            if (context.IsCardIgnoredForScore(card))
            {
                RecordScoreStep(
                    context,
                    ScoreTimelineStepKind.BossDebuff,
                    GameText.Get("game.ScoreManager.ApplySubmittedCardsInOrder.5ca2eb5293", "망태할아범 / 마지막 패 무효"),
                    card,
                    context.ChipValue,
                    context.Multiplier,
                    ScoreMultiplierChangeType.None,
                    null,
                    JokBoType.None,
                    true);
                continue;
            }

            if (card.cardData is CardSO normalCard)
            {
                if (!context.JokboMatchedCards.Contains(card) && !ShouldScoreAllSubmittedCards())
                    continue;

                ApplyNormalCardScoreStep(context, card, normalCard);
                continue;
            }

            if (card.cardData is JokerSO joker)
            {
                if (HasAbility<AbilityLionMask>(joker))
                    continue; // 파괴는 다른 패/재발동 효과가 끝난 뒤 처리합니다.
                if (copiedScoreCardDatas.TryGetValue(card, out CardSO copiedCard)
                    && (context.JokboMatchedCards.Contains(card) || ShouldScoreAllSubmittedCards()))
                    ApplyNormalCardScoreStep(context, card, copiedCard);
                ActivateJokerScoreAbilities(context, joker, card, ScoreTimelineStepKind.Joker, ResolveJokerDisplayName(joker));
            }
        }
    }

    /// <summary>사자탈은 제출 순서와 무관하게 동반 패의 발동이 끝난 뒤 파괴와 곱배수를 적용합니다.</summary>
    private void ApplyDeferredLionMasks(ScoreCalculationContext context)
    {
        int repeats = ArtifactManager.Instance != null && ArtifactManager.Instance.HasArtifact("O_Artifact_22")
            && CombatManager.instance != null && CombatManager.instance.SubmittedHandsThisRound == 0 ? 2 : 1;
        for (int pass = 0; pass < repeats; pass++)
            foreach (CardController card in context.SelectedCards)
                if (card != null && !context.IsCardIgnoredForScore(card)
                    && card.cardData is JokerSO joker && HasAbility<AbilityLionMask>(joker))
                    ActivateJokerScoreAbilities(context, joker, card, ScoreTimelineStepKind.Joker, ResolveJokerDisplayName(joker));
    }

    /// <summary>초랭이탈은 저승 세트 효과를 중복 계산하지 않고, 해당 패의 기본점수·강화·에디션만 재발동합니다.</summary>
    public void RetriggerYulGgeutCards(ref long chips, ref double multiplier)
    {
        scoreContext.ChipValue = chips;
        scoreContext.Multiplier = multiplier;
        foreach (CardController controller in scoreContext.JokboMatchedCards)
        {
            if (controller == null || scoreContext.IsCardIgnoredForScore(controller))
                continue;
            CardSO card = copiedScoreCardDatas.TryGetValue(controller, out CardSO copy)
                ? copy : controller.cardData as CardSO;
            if (card != null && card.currentYulGgeut != YulGgeutEnhancement.None)
                ApplyNormalCardScoreStep(scoreContext, controller, card);
        }
        chips = scoreContext.ChipValue;
        multiplier = scoreContext.Multiplier;
    }

    private void ApplyHorongbulReplay(ScoreCalculationContext context)
    {
        if (context == null
            || ArtifactManager.Instance == null
            || !ArtifactManager.Instance.HasArtifact("O_Artifact_22")
            || CombatManager.instance == null
            || CombatManager.instance.SubmittedHandsThisRound != 0)
        {
            return;
        }

        ApplySubmittedCardsInOrder(context);
        GameLog.Info("[호롱불] 첫 제출의 패 효과가 한 번 더 발동했습니다.");
    }

    private void ApplyNormalCardScoreStep(ScoreCalculationContext context, CardController cardController, CardSO card)
    {
        int triggerCount = card.currentDan == DanEnhancement.HongDan ? 2 : 1;
        for (int trigger = 0; trigger < triggerCount; trigger++)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            int effectiveBaseScore = ResolveNormalCardBaseScore(card);
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, effectiveBaseScore);
            context.BaseScore = ScoreSafeMath.AddScore(context.BaseScore, effectiveBaseScore);
            RecordScoreStep(
                context,
                ScoreTimelineStepKind.CardBase,
                ResolveCardDisplayName(card),
                cardController,
                beforeChips,
                beforeMultiplier,
                ScoreMultiplierChangeType.None);

            ResolveReactiveArtifactScoreEffects(
                context,
                cardController,
                card,
                ScoreTriggerStage.CardBase,
                DanEnhancement.None,
                YulGgeutEnhancement.None,
                GwangEnhancement.None,
                EditionType.None);

            ApplyCardEnhancements(context, cardController, card);
            ApplyCardEdition(context, cardController, card);
            // Dan is the last effect. HongDan schedules one complete replay without replaying itself.
            if (trigger == 0)
                ApplyDanSideEffect(context, cardController, card);
        }
    }

    private void ApplyCardEnhancements(ScoreCalculationContext context, CardController cardController, CardSO card)
    {
        if (card == null)
            return;

        if (card.currentGwang == GwangEnhancement.Gwang)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, GwangEnhancementMultiplier);
            RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyCardEnhancements.3d157ee610", "광"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Multiplicative, appliedMultiplierFactor: GwangEnhancementMultiplier);
            ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.None, YulGgeutEnhancement.None, GwangEnhancement.Gwang, EditionType.None);
        }

        ApplyYulGgeutEnhancement(context, cardController, card);
    }

    private void ApplyYulGgeutEnhancement(ScoreCalculationContext context, CardController cardController, CardSO card)
    {
        switch (card.currentYulGgeut)
        {
            case YulGgeutEnhancement.Warbler:
            {
                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 40);
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyYulGgeutEnhancement.645f9b83f8", "휘파람새"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.None, YulGgeutEnhancement.Warbler, GwangEnhancement.None, EditionType.None);
                break;
            }
            case YulGgeutEnhancement.Cuckoo:
            {
                if (!ProbabilityResolver.RollChance(0.25f))
                {
                    GameLog.Info($"[강화 실패] 두견새 {ProbabilityResolver.FormatChance(0.25f)} 효과가 빗나갔습니다.");
                    break;
                }

                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 2d);
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyYulGgeutEnhancement.8ce11d72dd", "두견새"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Multiplicative, appliedMultiplierFactor: 2d);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.None, YulGgeutEnhancement.Cuckoo, GwangEnhancement.None, EditionType.None);
                break;
            }
            case YulGgeutEnhancement.Butterfly:
            {
                if (!ProbabilityResolver.RollChance(0.5f))
                {
                    GameLog.Info($"[강화 실패] 나비 {ProbabilityResolver.FormatChance(0.5f)} 효과가 빗나갔습니다.");
                    break;
                }

                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 60);
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyYulGgeutEnhancement.2cb37f5048", "나비"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.None, YulGgeutEnhancement.Butterfly, GwangEnhancement.None, EditionType.None);
                break;
            }
            case YulGgeutEnhancement.Boar:
            {
                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, 5d);
                DiscardRandomDeckCardsForBoar(context);
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyYulGgeutEnhancement.84031a103e", "멧돼지"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.None, YulGgeutEnhancement.Boar, GwangEnhancement.None, EditionType.None);
                break;
            }
            case YulGgeutEnhancement.Deer:
                GameLog.Info("[효과 확인] 제출된 사슴은 점수 계산 효과가 없습니다. 손패에 남아 있을 때 재화를 줍니다.");
                break;
        }
    }

    private void ApplyDanSideEffect(ScoreCalculationContext context, CardController cardController, CardSO card)
    {
        switch (card.currentDan)
        {
            case DanEnhancement.HongDan:
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyDanSideEffect.dcceb807d2", "홍단 재발동"), cardController,
                    context.ChipValue, context.Multiplier, ScoreMultiplierChangeType.None, forceRecord: true);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement,
                    DanEnhancement.HongDan, YulGgeutEnhancement.None, GwangEnhancement.None, EditionType.None);
                break;
            case DanEnhancement.ChoDan:
            {
                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                if (ConsumableManager.Instance != null)
                    ConsumableManager.Instance.AddRandomConsumable(1, GameText.Get("game.ScoreManager.ApplyDanSideEffect.8cc9a5b6ba", "초단"));

                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyDanSideEffect.8cc9a5b6ba", "초단"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.None, null, JokBoType.None, true);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.ChoDan, YulGgeutEnhancement.None, GwangEnhancement.None, EditionType.None);
                break;
            }
            case DanEnhancement.CheongDan:
            {
                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                EconomyManager.instance?.AddMoney(3);
                RecordScoreStep(context, ScoreTimelineStepKind.CardEnhancement, GameText.Get("game.ScoreManager.ApplyDanSideEffect.133a335219", "청단"), cardController, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.None, null, JokBoType.None, true);
                ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEnhancement, DanEnhancement.CheongDan, YulGgeutEnhancement.None, GwangEnhancement.None, EditionType.None);
                break;
            }
        }
    }

    private void ApplyCardEdition(ScoreCalculationContext context, CardController cardController, CardSO card)
    {
        if (card == null || card.currentEdition == EditionType.None)
            return;

        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        ScoreMultiplierChangeType changeType = ScoreMultiplierChangeType.None;
        string displayName = card.currentEdition.ToString();
        double appliedMultiplierFactor = 0d;

        switch (card.currentEdition)
        {
            case EditionType.Silver:
                context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 50);
                displayName = GameText.Get("game.ScoreManager.ApplyCardEdition.d07420ba7d", "은박");
                changeType = ScoreMultiplierChangeType.Additive;
                break;
            case EditionType.Gold:
                context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, 10d);
                displayName = GameText.Get("game.ScoreManager.ApplyCardEdition.86468b7d8c", "금박");
                changeType = ScoreMultiplierChangeType.Additive;
                break;
            case EditionType.Radiant:
                appliedMultiplierFactor = 1.5d;
                context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, appliedMultiplierFactor);
                displayName = GameText.Get("game.ScoreManager.ApplyCardEdition.13cf50b48f", "찬란");
                changeType = ScoreMultiplierChangeType.Multiplicative;
                break;
        }

        RecordScoreStep(context, ScoreTimelineStepKind.CardEdition, displayName, cardController, beforeChips, beforeMultiplier, changeType, appliedMultiplierFactor: appliedMultiplierFactor);
        ResolveReactiveArtifactScoreEffects(context, cardController, card, ScoreTriggerStage.CardEdition, DanEnhancement.None, YulGgeutEnhancement.None, GwangEnhancement.None, card.currentEdition);
    }

    private void DiscardRandomDeckCardsForBoar(ScoreCalculationContext context)
    {
        if (CardManager_2.Instance == null || CardManager_2.Instance.cardBuffer == null)
            return;

        int cardsToDiscard = Mathf.Min(3, CardManager_2.Instance.cardBuffer.Count);
        for (int i = 0; i < cardsToDiscard; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, CardManager_2.Instance.cardBuffer.Count);
            BaseCardSO cardToDrop = CardManager_2.Instance.cardBuffer[randomIndex];
            CardManager_2.Instance.cardBuffer.RemoveAt(randomIndex);
            CardManager_2.Instance.dropCardBuffer.Add(cardToDrop);
        }

        context.DeckCount = CardManager_2.Instance.cardBuffer.Count;
        CardManager_2.Instance.RefreshRemainingDeckCountUI();
    }

    private void ResolveReactiveArtifactScoreEffects(
        ScoreCalculationContext context,
        CardController sourceCard,
        CardSO triggeredCard,
        ScoreTriggerStage triggerStage,
        DanEnhancement triggeredDan,
        YulGgeutEnhancement triggeredYulGgeut,
        GwangEnhancement triggeredGwang,
        EditionType triggeredEdition)
    {
        if (ArtifactManager.Instance == null || ArtifactManager.Instance.inventory == null || triggeredCard == null)
            return;

        List<ArtifactData> artifacts = ArtifactManager.Instance.inventory.items;
        for (int i = 0; i < artifacts.Count; i++)
        {
            if (ArtifactManager.Instance.IsArtifactTemporarilySuppressed(i))
                continue;

            ResolveArtifactScoreEntries(
                context,
                artifacts[i],
                i,
                true,
                sourceCard,
                triggeredCard,
                triggerStage,
                triggeredDan,
                triggeredYulGgeut,
                triggeredGwang,
                triggeredEdition);
        }
    }

    private void ApplyGeneralArtifactScoreEffects(ScoreCalculationContext context)
    {
        if (ArtifactManager.Instance == null || ArtifactManager.Instance.inventory == null)
            return;

        List<ArtifactData> artifacts = ArtifactManager.Instance.inventory.items;
        for (int i = 0; i < artifacts.Count; i++)
        {
            if (ArtifactManager.Instance.IsArtifactTemporarilySuppressed(i))
                continue;

            ArtifactData artifact = artifacts[i];
            ResolveArtifactScoreEntries(
                context,
                artifact,
                i,
                false,
                null,
                null,
                ScoreTriggerStage.GeneralArtifact,
                DanEnhancement.None,
                YulGgeutEnhancement.None,
                GwangEnhancement.None,
                EditionType.None);

            ApplyArtifactEdition(context, artifact, i);
        }

        ApplyArtifactSynergyScoreEffects(context);
    }

    private void ApplyArtifactEdition(ScoreCalculationContext context, ArtifactData artifact, int artifactIndex)
    {
        EditionType edition = ArtifactManager.Instance != null
            ? ArtifactManager.Instance.GetArtifactEdition(artifactIndex)
            : (artifact != null ? artifact.currentEdition : EditionType.None);

        if (context == null || artifact == null || edition == EditionType.None)
            return;

        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        ScoreMultiplierChangeType changeType = ScoreMultiplierChangeType.None;

        switch (edition)
        {
            case EditionType.Silver:
                context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 50);
                changeType = ScoreMultiplierChangeType.Additive;
                break;

            case EditionType.Gold:
                context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, 10d);
                changeType = ScoreMultiplierChangeType.Additive;
                break;

            case EditionType.Radiant:
                context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
                changeType = ScoreMultiplierChangeType.Multiplicative;
                break;
        }

        ScoreSafeMath.SanitizeScoreState(ref context.ChipValue, ref context.Multiplier);
        RecordArtifactScoreStep(
            context,
            artifact,
            null,
            beforeChips,
            beforeMultiplier,
            changeType,
            artifactInventoryIndex: artifactIndex,
            appliedMultiplierFactor: edition == EditionType.Radiant ? 1.5d : 0d);
    }

    private void ResolveArtifactScoreEntries(
        ScoreCalculationContext context,
        ArtifactData artifact,
        int artifactIndex,
        bool cardReactiveOnly,
        CardController sourceCard,
        CardSO triggeredCard,
        ScoreTriggerStage triggerStage,
        DanEnhancement triggeredDan,
        YulGgeutEnhancement triggeredYulGgeut,
        GwangEnhancement triggeredGwang,
        EditionType triggeredEdition)
    {
        if (artifact == null)
            return;

        if (artifact.artifactID == EffectResolver.ClownHatArtifactID)
        {
            if (!cardReactiveOnly)
                ApplyClownHatScoreSwap(context, artifact, sourceCard, artifactIndex);
            return;
        }

        if (artifact.abilities == null)
            return;

        ResolveCopyLeftArtifactScoreEntries(context, artifact, artifactIndex, cardReactiveOnly, sourceCard, triggeredCard, triggerStage, triggeredDan, triggeredYulGgeut, triggeredGwang, triggeredEdition);

        for (int i = 0; i < artifact.abilities.Count; i++)
        {
            AbilityEntry entry = artifact.abilities[i];
            if (entry.ability == null || entry.trigger != ArtifactTriggerType.OnFinishScore)
                continue;

            bool isReactiveCondition = ArtifactConditionEvaluator.IsCardReactiveCondition(entry.condition);
            if (cardReactiveOnly != isReactiveCondition)
                continue;

            List<BaseCardSO> activeCards = triggeredCard != null
                ? new List<BaseCardSO> { triggeredCard }
                : context.PlayedCardDatas;
            ArtifactEffectContext artifactContext = context.ArtifactContext;
            artifactContext.Setup(
                ArtifactTriggerType.OnFinishScore,
                context.ChipValue,
                context.Multiplier,
                context.Economy,
                activeCards,
                context.CurrentHands,
                context.CurrentDiscards,
                context.DeckCount,
                context.Jokbo,
                null,
                triggerStage,
                triggeredCard,
                triggeredDan,
                triggeredYulGgeut,
                triggeredGwang,
                triggeredEdition);

            if (!ArtifactConditionEvaluator.Passes(artifact, entry, artifactContext))
                continue;

            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;

            string previousSpawnSourceName = null;
            ArtifactData previousSpawnSourceArtifact = null;
            AbilitySpawnConsumable spawnConsumable = entry.ability as AbilitySpawnConsumable;
            if (spawnConsumable != null)
            {
                previousSpawnSourceName = spawnConsumable.RuntimeSourceName;
                previousSpawnSourceArtifact = spawnConsumable.RuntimeSourceArtifact;
                spawnConsumable.RuntimeSourceName = ResolveArtifactDisplayName(artifact);
                spawnConsumable.RuntimeSourceArtifact = artifact;
            }

            ArtifactManager artifactManager = ArtifactManager.Instance;
            int previousArtifactIndex = -1;
            string previousInstanceID = null;
            if (artifactManager != null)
                previousInstanceID = artifactManager.BeginArtifactEffectScope(artifactIndex, out previousArtifactIndex);

            try
            {
                AbilityGainGrowthStack growth = entry.ability as AbilityGainGrowthStack;
                float beforeGrowth = growth != null && artifactManager != null
                    ? artifactManager.GetGrowthStack(growth.targetGrowthID) : 0f;
                entry.ability.OnActivate(
                    ref context.ChipValue,
                    ref context.Multiplier,
                    context.Economy,
                    activeCards,
                    context.CurrentHands,
                    context.CurrentDiscards,
                    context.DeckCount,
                    context.Jokbo);

                EffectResolver.RecordGrowthMilestones(artifact, artifactIndex, growth, beforeGrowth, artifactContext.MilestoneSteps);
                foreach (ScoreTimelineStep notice in artifactContext.MilestoneSteps)
                {
                    notice.beforeChips = notice.afterChips = context.ChipValue;
                    notice.beforeMultiplier = notice.afterMultiplier = context.Multiplier;
                    context.ScoreTimelineSteps.Add(notice);
                }
                if (artifactContext.MilestoneSteps.Count > 0 && CombatManager.instance != null)
                {
                    context.CurrentHands = CombatManager.instance.remaningHands;
                    context.CurrentDiscards = CombatManager.instance.remaningDrops;
                }
            }
            finally
            {
                if (artifactManager != null)
                    artifactManager.EndArtifactEffectScope(previousInstanceID, previousArtifactIndex);
            }
            ScoreSafeMath.SanitizeScoreState(ref context.ChipValue, ref context.Multiplier);

            if (spawnConsumable != null)
            {
                spawnConsumable.RuntimeSourceName = previousSpawnSourceName;
                spawnConsumable.RuntimeSourceArtifact = previousSpawnSourceArtifact;
            }

            RecordArtifactScoreStep(
                context,
                artifact,
                sourceCard,
                beforeChips,
                beforeMultiplier,
                EffectResolver.GetMultiplierChangeType(entry.ability),
                entry.ability is AbilitySpawnConsumable,
                artifactIndex);
        }
    }

    private void ApplyClownHatScoreSwap(
        ScoreCalculationContext context,
        ArtifactData clownHat,
        CardController sourceCard,
        int artifactIndex)
    {
        if (context == null || clownHat == null)
            return;

        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        EffectResolver.SwapScoreTotals(ref context.ChipValue, ref context.Multiplier);
        ScoreSafeMath.SanitizeScoreState(ref context.ChipValue, ref context.Multiplier);
        RecordArtifactScoreStep(
            context,
            clownHat,
            sourceCard,
            beforeChips,
            beforeMultiplier,
            ScoreMultiplierChangeType.Mixed,
            true,
            artifactIndex);
    }

    private void ResolveCopyLeftArtifactScoreEntries(
        ScoreCalculationContext context,
        ArtifactData artifact,
        int artifactIndex,
        bool cardReactiveOnly,
        CardController sourceCard,
        CardSO triggeredCard,
        ScoreTriggerStage triggerStage,
        DanEnhancement triggeredDan,
        YulGgeutEnhancement triggeredYulGgeut,
        GwangEnhancement triggeredGwang,
        EditionType triggeredEdition)
    {
        if (!HasAbility<AbilityCopyLeft>(artifact) || ArtifactManager.Instance == null || ArtifactManager.Instance.inventory == null)
            return;

        List<ArtifactData> artifacts = ArtifactManager.Instance.inventory.items;
        if (artifactIndex <= 0)
            return;

        int leftArtifactIndex = artifactIndex - 1;
        if (ArtifactManager.Instance.IsArtifactTemporarilySuppressed(leftArtifactIndex))
            return;

        ArtifactData leftArtifact = artifacts[leftArtifactIndex];
        if (HasAbility<AbilityCopyLeft>(leftArtifact))
            return;

        ResolveArtifactScoreEntries(context, leftArtifact, leftArtifactIndex, cardReactiveOnly, sourceCard, triggeredCard, triggerStage, triggeredDan, triggeredYulGgeut, triggeredGwang, triggeredEdition);
    }

    private void ApplyArtifactSynergyScoreEffects(ScoreCalculationContext context)
    {
        ArtifactManager artifactManager = ArtifactManager.Instance;
        if (context == null || artifactManager == null)
            return;

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.FourGentlemen, 2))
            ApplySynergyMultiplier(context, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.a17018687d", "시너지: 사군자 2"), artifactManager.ScaleSynergyEffect(2d));

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.FourGentlemen, 4))
            ApplySynergyMultiplier(context, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.fb5f18cb67", "시너지: 사군자 4"), artifactManager.ScaleSynergyEffect(3d));

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.Peace, 2))
            ApplySynergyMultiplier(context, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.62f52b4d42", "시너지: 태평성대 2"), artifactManager.ScaleSynergyEffect(3d));

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.Peace, 3))
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, artifactManager.ScaleSynergyEffect(250));
            RecordScoreStep(context, ScoreTimelineStepKind.Synergy, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.961875cb58", "시너지: 태평성대 3"), null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
        }

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.Hyoshi, 2)
            && CombatManager.instance != null
            && CombatManager.instance.SubmittedHandsThisRound == 0)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, artifactManager.ScaleSynergyEffect(50));
            context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, artifactManager.ScaleSynergyEffect(5d));
            RecordScoreStep(context, ScoreTimelineStepKind.Synergy, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.8b5b16c0cb", "시너지: 효시 2"), null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Mixed);
        }

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.ImmortalPlay, 2)
            && GameManager.instance != null)
        {
            int discardBonus = artifactManager.ScaleSynergyEffect(
                Mathf.Max(0, GameManager.instance.TotalDiscardActionsThisRun * 2));
            if (discardBonus > 0)
            {
                long beforeChips = context.ChipValue;
                double beforeMultiplier = context.Multiplier;
                context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, discardBonus);
                RecordScoreStep(context, ScoreTimelineStepKind.Synergy, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.ee24e8a9aa", "시너지: 신선놀음 2"), null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Additive);
            }
        }

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.Wealth, 1)
            && context.Economy != null
            && context.Economy.ecoData != null)
        {
            int moneySteps = Mathf.Max(0, context.Economy.ecoData.currentMoney / 10);
            double wealthMultiplier = 1d + (moneySteps * 0.2d * artifactManager.GetSynergyEffectMultiplier());
            ApplySynergyMultiplier(context, GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.ffeae6b45d", "시너지: 부귀영화 1"), wealthMultiplier);
        }

        if (artifactManager.HasSynergyTier(ArtifactSynergyType.GuardianSpirit, 2))
        {
            float rounds = artifactManager.GetGrowthStack("Synergy_GuardianSpirit_Rounds");
            ApplySynergyMultiplier(
                context,
                GameText.Get("game.ScoreManager.ApplyArtifactSynergyScoreEffects.a33828c0e6", "시너지: 수호신 2"),
                1d + (rounds * 0.3d * artifactManager.GetSynergyEffectMultiplier()));
        }
    }

    private void ApplySynergyMultiplier(ScoreCalculationContext context, string displayName, double multiplier)
    {
        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, multiplier);
        RecordScoreStep(context, ScoreTimelineStepKind.Synergy, displayName, null, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Multiplicative, appliedMultiplierFactor: multiplier);
    }

    private void ApplyHandAndDiscardPileEffects(ScoreCalculationContext context)
    {
        if (CardManager_2.Instance == null)
            return;

        if (CardManager_2.Instance.myHandCards != null)
        {
            foreach (CardController handCard in CardManager_2.Instance.myHandCards)
            {
                if (handCard == null || context.SelectedCards.Contains(handCard))
                    continue;

                if (handCard.cardData is CardSO card)
                    ApplyHandCardPassive(context, handCard, card);
                else if (handCard.cardData is JokerSO joker)
                    ActivateJokerScoreAbilities(context, joker, handCard, ScoreTimelineStepKind.HandPassive, GameText.Get("game.ScoreManager.ApplyHandAndDiscardPileEffects.751f1cfa45", "손패 {value0}", new { value0 = ResolveJokerDisplayName(joker) }));
            }
        }

        if (CardManager_2.Instance.dropCardBuffer == null)
            return;

        for (int i = 0; i < CardManager_2.Instance.dropCardBuffer.Count; i++)
        {
            if (CardManager_2.Instance.dropCardBuffer[i] is JokerSO joker)
                ActivateJokerScoreAbilities(context, joker, null, ScoreTimelineStepKind.HandPassive, GameText.Get("game.ScoreManager.ApplyHandAndDiscardPileEffects.7df7a8bb15", "저승 {value0}", new { value0 = ResolveJokerDisplayName(joker) }));
        }
    }

    private void ApplyHandCardPassive(ScoreCalculationContext context, CardController handCard, CardSO card)
    {
        if (card.currentYulGgeut == YulGgeutEnhancement.Goose)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
            RecordScoreStep(context, ScoreTimelineStepKind.HandPassive, GameText.Get("game.ScoreManager.ApplyHandCardPassive.41ff6e70db", "손패 기러기"), handCard, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.Multiplicative, appliedMultiplierFactor: 1.5d);
        }

        if (card.currentYulGgeut == YulGgeutEnhancement.Deer && EconomyManager.instance != null)
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            EconomyManager.instance.AddMoney(3);
            RecordScoreStep(context, ScoreTimelineStepKind.HandPassive, GameText.Get("game.ScoreManager.ApplyHandCardPassive.c387cb1b0d", "손패 사슴"), handCard, beforeChips, beforeMultiplier, ScoreMultiplierChangeType.None, null, JokBoType.None, true);
        }
    }

    private void ActivateJokerScoreAbilities(
        ScoreCalculationContext context,
        JokerSO joker,
        CardController sourceCard,
        ScoreTimelineStepKind stepKind,
        string displayName)
    {
        if (context == null || joker == null)
            return;

        // 기본 칩은 제출한 탈에만 붙습니다. 손패/저승 발동 시에는 중복 가산하지 않습니다.
        if (stepKind == ScoreTimelineStepKind.Joker && joker.bonusChip > 0)
        {
            long beforeChips = context.ChipValue;
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, joker.bonusChip);
            RecordScoreStep(context, stepKind, GameText.Get("game.ScoreManager.ActivateJokerScoreAbilities.c5e2c08483", "{value0} / 기본 점수", new { value0 = displayName }), sourceCard,
                beforeChips, context.Multiplier, ScoreMultiplierChangeType.None);
        }
        long chips = context.ChipValue;
        double multiplier = context.Multiplier;
        joker.ActivateAbilities(
            JokerTriggerType.OnScoreCalculation,
            ref chips,
            ref multiplier,
            context.Economy,
            context.PlayedCardDatas,
            context.CurrentHands,
            context.CurrentDiscards,
            context.DeckCount,
            context.Jokbo,
            (entry, beforeChips, beforeMultiplier, afterChips, afterMultiplier) =>
            {
                context.ChipValue = afterChips;
                context.Multiplier = afterMultiplier;
                // 초랭이탈은 내부에서 패별 타임라인을 이미 기록하므로 합산 요약을 다시 연출하지 않습니다.
                if (entry.ability is AbilityChoraengiMask)
                    return;
                string abilityName = entry.ability != null ? entry.ability.GetType().Name : GameText.Get("game.ScoreManager.ActivateJokerScoreAbilities.95c8cb5085", "탈 효과");
                RecordScoreStep(
                    context,
                    stepKind,
                    $"{displayName} / {abilityName}",
                    sourceCard,
                    beforeChips,
                    beforeMultiplier,
                    EffectResolver.GetMultiplierChangeType(entry.ability));
            });

        context.ChipValue = chips;
        context.Multiplier = multiplier;
    }

    private void RecordArtifactScoreStep(
        ScoreCalculationContext context,
        ArtifactData artifact,
        CardController sourceCard,
        long beforeChips,
        double beforeMultiplier,
        ScoreMultiplierChangeType changeType,
        bool forceRecord = false,
        int artifactInventoryIndex = -1,
        double appliedMultiplierFactor = 0d)
    {
        if (context == null || artifact == null)
            return;

        string displayName = string.IsNullOrWhiteSpace(artifact.artifactName) ? artifact.name : GameContent.Get(artifact, "artifactName", artifact.artifactName);
        ArtifactScoreStep artifactStep = new ArtifactScoreStep
        {
            artifact = artifact,
            displayName = displayName,
            beforeChips = beforeChips,
            afterChips = context.ChipValue,
            beforeMultiplier = beforeMultiplier,
            afterMultiplier = context.Multiplier,
            multiplierChangeType = changeType
        };

        if (forceRecord || artifactStep.HasScoreChange())
            context.ArtifactScoreSteps.Add(artifactStep);

        RecordScoreStep(context, ScoreTimelineStepKind.Artifact, displayName, sourceCard, beforeChips, beforeMultiplier, changeType, artifact, JokBoType.None, forceRecord, artifactInventoryIndex, appliedMultiplierFactor);
    }

    private static string ResolveArtifactDisplayName(ArtifactData artifact)
    {
        if (artifact == null)
            return GameText.Get("game.ScoreManager.ResolveArtifactDisplayName.7bada8867d", "기물");

        if (!string.IsNullOrWhiteSpace(artifact.artifactName))
            return GameContent.Get(artifact, "artifactName", artifact.artifactName);

        return string.IsNullOrWhiteSpace(artifact.name) ? GameText.Get("game.ScoreManager.ResolveArtifactDisplayName.7bada8867d", "기물") : artifact.name;
    }

    private void RecordScoreStep(
        ScoreCalculationContext context,
        ScoreTimelineStepKind kind,
        string displayName,
        CardController sourceCard,
        long beforeChips,
        double beforeMultiplier,
        ScoreMultiplierChangeType changeType,
        ArtifactData artifact = null,
        JokBoType jokbo = JokBoType.None,
        bool forceRecord = false,
        int artifactInventoryIndex = -1,
        double appliedMultiplierFactor = 0d)
    {
        if (context == null)
            return;

        ScoreTimelineStep step = new ScoreTimelineStep
        {
            kind = kind,
            artifactInventoryIndex = artifactInventoryIndex,
            displayName = string.IsNullOrWhiteSpace(displayName) ? GameText.Get("game.ScoreManager.RecordScoreStep.377d1a0f2e", "효과") : displayName,
            sourceCard = sourceCard,
            artifact = artifact,
            jokbo = jokbo,
            beforeChips = beforeChips,
            afterChips = context.ChipValue,
            beforeMultiplier = beforeMultiplier,
            afterMultiplier = context.Multiplier,
            multiplierChangeType = changeType,
            appliedMultiplierFactor = appliedMultiplierFactor
        };

        if (forceRecord || step.HasScoreChange())
            context.ScoreTimelineSteps.Add(step);
    }

    private static bool HasAbility<TAbility>(ArtifactData artifact) where TAbility : GameAbility
    {
        if (artifact == null || artifact.abilities == null)
            return false;

        for (int i = 0; i < artifact.abilities.Count; i++)
        {
            if (artifact.abilities[i].ability is TAbility)
                return true;
        }

        return false;
    }

    private static string ResolveCardDisplayName(CardSO card)
    {
        if (card == null)
            return GameText.Get("game.ScoreManager.ResolveCardDisplayName.67f8bde348", "패");

        return GameText.Get("game.ScoreManager.ResolveCardDisplayName.7b61159be9", "{value0}월 패", new { value0 = (int)card.cardMonth + 1 });
    }

    private static string ResolveJokerDisplayName(JokerSO joker)
    {
        if (joker == null)
            return GameText.Get("game.ScoreManager.ResolveJokerDisplayName.2d4a8a8b28", "탈");

        return string.IsNullOrWhiteSpace(joker.jokerName) ? joker.name : GameContent.Get(joker, "jokerName", joker.jokerName);
    }

    private void AppendArtifactAuditSteps(ScoreAuditSnapshot scoreAudit, int startIndex)
    {
        if (scoreAudit == null || scoreContext.ArtifactScoreSteps == null)
            return;

        int safeStartIndex = Mathf.Clamp(startIndex, 0, scoreContext.ArtifactScoreSteps.Count);
        for (int i = safeStartIndex; i < scoreContext.ArtifactScoreSteps.Count; i++)
        {
            scoreAudit.AddArtifactStep(scoreContext.ArtifactScoreSteps[i]);
        }
    }

    private string GetCurrentDebuffAuditLabel()
    {
        BossDebuffSO debuff = CombatManager.instance != null ? CombatManager.instance.currentDebuff : null;
        if (debuff == null)
            return GameText.Get("game.ScoreManager.GetCurrentDebuffAuditLabel.636cdef390", "현재 보스 저주 없음");

        string debuffName = string.IsNullOrWhiteSpace(debuff.name) ? debuff.GetType().Name : debuff.name;
        return GameText.Get("game.ScoreManager.GetCurrentDebuffAuditLabel.48a50e8b17", "현재 보스 저주: {value0}", new { value0 = debuffName });
    }

    private void PrepareTemporaryCardDataForJokbo(ScoreCalculationContext context)
    {
        RestoreTemporaryCardData();

        List<CardController> selectedCards = context.SelectedCards;
        if (selectedCards == null)
            return;

        CardSO highestMonthCard = FindHighestMonthCard(selectedCards);
        if (highestMonthCard == null)
            return;

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardController card = selectedCards[i];
            if (card == null || !(card.cardData is JokerSO joker))
                continue;

            if (!HasAbility<AbilityCopyHighMonth>(joker))
                continue;

            copiedOriginalCardDatas[card] = card.cardData;

            // 월뿐 아니라 강화·에디션·파괴·기본 점수를 복사하며 원본 패는 변경하지 않습니다.
            CardSO copy = Instantiate(highestMonthCard);
            copiedScoreCardDatas[card] = copy;
            card.cardData = copy;
        }
    }

    private static bool HasAbility<T>(JokerSO joker) where T : GameAbility
    {
        if (joker == null || joker.abilities == null)
            return false;

        for (int i = 0; i < joker.abilities.Count; i++)
        {
            if (joker.abilities[i].ability is T)
                return true;
        }

        return false;
    }

    private CardSO FindHighestMonthCard(List<CardController> selectedCards)
    {
        CardSO highestMonthCard = null;

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardController card = selectedCards[i];
            if (card == null || !(card.cardData is CardSO normalCard))
                continue;

            if (highestMonthCard == null || (int)normalCard.cardMonth > (int)highestMonthCard.cardMonth)
                highestMonthCard = normalCard;
        }

        return highestMonthCard;
    }

    private void RestoreTemporaryCardData(bool keepScoreCopies = false)
    {
        foreach (var kvp in copiedOriginalCardDatas)
        {
            if (kvp.Key != null)
                kvp.Key.cardData = kvp.Value;
        }

        copiedOriginalCardDatas.Clear();
        if (keepScoreCopies)
            return;
        foreach (CardSO copy in copiedScoreCardDatas.Values)
        {
            if (copy == null) continue;
            if (Application.isPlaying) Destroy(copy);
            else DestroyImmediate(copy);
        }
        copiedScoreCardDatas.Clear();
    }

    private void CalculateBaseScore(ScoreCalculationContext context)
    {
        context.BaseScore = 0;

        List<CardController> scoreCards = context.JokboMatchedCards;
        if (scoreCards == null || scoreCards.Count == 0)
            scoreCards = context.SelectedCards;
        if (scoreCards == null)
            return;

        for (int i = 0; i < scoreCards.Count; i++)
        {
            CardController card = scoreCards[i];
            if (card != null && card.cardData is CardSO normalCard)
                context.BaseScore = ScoreSafeMath.AddScore(context.BaseScore, ResolveNormalCardBaseScore(normalCard));
        }
    }

    private void ApplyPreJokboSelectionDebuff(ScoreCalculationContext context)
    {
        if (CombatManager.instance == null || CombatManager.instance.currentDebuff == null)
            return;

        // 현재 보스 저주 중 실제로 족보 판정 전에 카드 목록을 바꿔야 하는 것은
        // 월 무효화뿐입니다. 나머지 저주는 상태 변화나 배수 변화가 있어 한 번만 실행합니다.
        if (!(CombatManager.instance.currentDebuff is UnusableRandomMonth))
            return;

        long baseScore = context.BaseScore;
        double multiplier = context.Multiplier;
        CombatManager.instance.currentDebuff.OnDebuff(context.SelectedCards, ref baseScore, ref multiplier);
        context.BaseScore = baseScore;
    }

    private void EvaluateJokbo(ScoreCalculationContext context)
    {
        var result = EvaluateJokbo(context.SelectedCards, jokboMatchedControllerScratch, null);
        context.SetJokboMatchedCards(jokboMatchedControllerScratch);
        context.Jokbo = result.jokbo;
        context.JokboBonusChip = result.bonus;
        context.JokboMultiplier = result.multi;
    }

    private void ApplyPlanetBonus(ScoreCalculationContext context)
    {
        context.PlanetBonusChip = 0;
        context.PlanetBonusMultiplier = 0;

        if (TryGetPlanetStats(context.Jokbo, out PlanetStats stats))
        {
            context.PlanetBonusChip = stats.plusChip;
            context.PlanetBonusMultiplier = stats.plusMulti;
        }

        if (ArtifactManager.Instance != null
            && ArtifactManager.Instance.HasArtifact("L_Artifact_20")
            && ProbabilityResolver.RollChance(1f / 3f)
            && PlanetCardLevelManager.Instance != null
            && PlanetCardLevelManager.Instance.TryGetNextLevelBonus(context.Jokbo, out int nextChip, out int nextMult))
        {
            context.PlanetBonusChip = (int)ScoreSafeMath.AddScore(context.PlanetBonusChip, nextChip);
            context.PlanetBonusMultiplier = ScoreSafeMath.AddMultiplier(context.PlanetBonusMultiplier, nextMult);
            GameLog.Info($"[고장난 슬롯머신] {context.Jokbo} 족보 레벨 상승 효과 적용");
        }
    }

    private void ApplyPlanetBonusToPreview(ScorePreviewData previewData)
    {
        if (previewData == null || previewData.jokbo == JokBoType.None)
            return;

        if (TryGetPlanetStats(previewData.jokbo, out PlanetStats stats))
        {
            previewData.jokboBonusChip = ScoreSafeMath.AddScore(previewData.jokboBonusChip, stats.plusChip);
            previewData.multiplier = ScoreSafeMath.AddScore(previewData.multiplier, stats.plusMulti);
        }
    }

    private static bool TryGetPlanetStats(JokBoType jokboType, out PlanetStats stats)
    {
        stats = null;
        if (jokboType == JokBoType.None
            || PlanetCardLevelManager.Instance == null
            || PlanetCardLevelManager.Instance.jokboLevel == null)
        {
            return false;
        }

        return PlanetCardLevelManager.Instance.jokboLevel.TryGetValue(jokboType, out stats) && stats != null;
    }

    private void BuildInitialScoreValue(ScoreCalculationContext context)
    {
        context.ChipValue = context.JokboBonusChip + context.PlanetBonusChip + context.BaseScore;

        double roundDiscardBonus = CombatManager.instance != null
            ? CombatManager.instance.roundDiscardBonusMultiplier
            : 0;

        context.Multiplier = context.JokboMultiplier + context.PlanetBonusMultiplier + roundDiscardBonus;
    }

    private void ApplyBossDebuffOnce(ScoreCalculationContext context)
    {
        if (context == null || CombatManager.instance == null || CombatManager.instance.currentDebuff == null)
            return;

        // UnusableRandomMonth는 위에서 카드 필터링만 선처리합니다.
        // 여기서 다시 호출하면 같은 제출에서 저주가 중복 실행될 수 있습니다.
        if (CombatManager.instance.currentDebuff is UnusableRandomMonth)
            return;

        // 첫 족보 고정은 다른 효과를 모두 보여준 뒤 마지막 잠금 단계에서 0점 처리합니다.
        if (CombatManager.instance.currentDebuff is JustPlayFirstJokbo)
            return;

        JokboPlayTracker tracker = JokboPlayTracker.Instance;
        JokBoType previousEvaluatingJokbo = tracker != null ? tracker.CurrentEvaluatingJokbo : JokBoType.None;

        if (tracker != null)
            tracker.CurrentEvaluatingJokbo = context.Jokbo;

        try
        {
            long beforeChips = context.ChipValue;
            double beforeMultiplier = context.Multiplier;
            long chipValue = beforeChips;
            double multiplier = beforeMultiplier;
            CombatManager.instance.currentDebuff.OnDebuff(context.SelectedCards, ref chipValue, ref multiplier);

            context.ChipValue = System.Math.Max(0L, chipValue);
            context.Multiplier = System.Math.Max(0d, multiplier);
            RecordScoreStep(
                context,
                ScoreTimelineStepKind.BossDebuff,
                GetCurrentDebuffAuditLabel(),
                null,
                beforeChips,
                beforeMultiplier,
                ScoreMultiplierChangeType.Mixed);
        }
        finally
        {
            if (tracker != null)
                tracker.CurrentEvaluatingJokbo = previousEvaluatingJokbo;
        }
    }

    private bool ApplyUnavailableJokboFinalLock(ScoreCalculationContext context)
    {
        if (context == null || CombatManager.instance == null)
            return false;

        JustPlayFirstJokbo firstJokboDebuff = CombatManager.instance.currentDebuff as JustPlayFirstJokbo;
        if (firstJokboDebuff == null || !firstJokboDebuff.IsJokboBlocked(context.Jokbo))
            return false;

        long beforeChips = context.ChipValue;
        double beforeMultiplier = context.Multiplier;
        context.ChipValue = 0;
        context.Multiplier = 0;
        RecordScoreStep(
            context,
            ScoreTimelineStepKind.FinalLock,
            GameText.Get("game.ScoreManager.ApplyUnavailableJokboFinalLock.94238f856f", "사용 불가"),
            null,
            beforeChips,
            beforeMultiplier,
            ScoreMultiplierChangeType.Mixed,
            null,
            context.Jokbo,
            true);
        return true;
    }

    // 족보 판정 함수
    private (JokBoType jokbo, int bonus, int multi) EvaluateJokbo(List<CardController> selectedCards)
    {
        return EvaluateJokbo(selectedCards, null, null);
    }

    private (JokBoType jokbo, int bonus, int multi) EvaluateJokbo(List<CardController> selectedCards, List<CardController> matchedCards)
    {
        return EvaluateJokbo(selectedCards, null, matchedCards);
    }

    private (JokBoType jokbo, int bonus, int multi) EvaluateJokbo(
        List<CardController> selectedCards,
        List<CardController> scoreMatchedCards,
        List<CardController> presentationMatchedCards)
    {
        CollectNormalCards(selectedCards, jokboCardScratch, jokboControllerScratch);
        scoreMatchedCards?.Clear();
        presentationMatchedCards?.Clear();

        JokboRuleProfile profile = ResolveJokboRuleProfile();
        if (profile != null
            && profile.TryEvaluate(
                jokboCardScratch,
                jokboControllerScratch,
                scoreMatchedCards,
                presentationMatchedCards,
                out JokboRuleEvaluationResult result))
            return (result.JokboType, result.BonusChip, result.Multiplier);

        return (JokBoType.None, 0, 1);
    }

    private void BuildPreviewCardSteps(
        ScorePreviewData previewData,
        List<CardController> scoreCards,
        List<CardController> submittedCards)
    {
        if (previewData == null)
            return;

        previewData.cardSteps.Clear();
        if (scoreCards == null)
            return;

        for (int i = 0; i < scoreCards.Count; i++)
        {
            CardController card = scoreCards[i];
            if (card == null || !(card.cardData is CardSO normalCard))
                continue;

            if (ShouldCurrentBossIgnoreSubmittedCard(card, submittedCards))
                continue;

            previewData.cardSteps.Add(new ScorePreviewCardStep
            {
                sourceCard = card,
                chipValue = ResolveNormalCardBaseScore(normalCard)
            });
        }
    }

    /// <summary>잡아채는 손길이 활성화된 동안 일반 패의 기본점수를 모든 계산 경로에서 0으로 통일합니다.</summary>
    private static int ResolveNormalCardBaseScore(CardSO card)
    {
        if (card == null)
            return 0;

        return CombatManager.instance != null
            && CombatManager.instance.currentDebuff is DebuffMangtaeZeroBaseScore
                ? 0
                : CardUpgradeService.GetExpectedScore(card);
    }

    /// <summary>
    /// 족보 판정이 끝난 뒤 현재 보스가 개별 점수에서 제외할 제출 패를 기록합니다.
    /// 족보 목록은 건드리지 않으므로 해당 패는 족보 구성에는 그대로 포함됩니다.
    /// </summary>
    private static void MarkBossIgnoredSubmittedCards(ScoreCalculationContext context)
    {
        if (context == null || context.SelectedCards == null || CombatManager.instance == null)
            return;

        BossDebuffSO debuff = CombatManager.instance.currentDebuff;
        if (debuff == null)
            return;

        int submittedCount = context.SelectedCards.Count;
        for (int i = 0; i < submittedCount; i++)
        {
            CardController card = context.SelectedCards[i];
            if (card != null && debuff.ShouldIgnoreSubmittedCardScore(card, i, submittedCount))
                context.IgnoredScoreCards.Add(card);
        }
    }

    /// <summary>현재 보스 디버프가 미리보기에서 해당 제출 패 점수를 제외하는지 확인합니다.</summary>
    private static bool ShouldCurrentBossIgnoreSubmittedCard(
        CardController card,
        List<CardController> submittedCards)
    {
        if (card == null || submittedCards == null || CombatManager.instance == null)
            return false;

        BossDebuffSO debuff = CombatManager.instance.currentDebuff;
        if (debuff == null)
            return false;

        int submittedIndex = submittedCards.IndexOf(card);
        return submittedIndex >= 0
            && debuff.ShouldIgnoreSubmittedCardScore(card, submittedIndex, submittedCards.Count);
    }

    private static bool ShouldScoreAllSubmittedCards()
    {
        return ArtifactManager.Instance != null
            && ArtifactManager.Instance.HasArtifact("O_Artifact_27");
    }

    private void CollectNormalCards(List<CardController> selectedCards, List<CardSO> results, List<CardController> controllers = null)
    {
        results.Clear();
        controllers?.Clear();

        if (selectedCards == null)
            return;

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardController card = selectedCards[i];
            if (card != null && card.cardData is CardSO normalCard)
            {
                results.Add(normalCard);
                controllers?.Add(card);
            }
        }
    }

    public JokBoType PreviewJokbo(List<CardController> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count == 0) return JokBoType.None;

        scoreContext.Reset(selectedCards);
        PrepareTemporaryCardDataForJokbo(scoreContext);

        // 트리거 없이 순수하게 족보만 판정
        var (jokbo, _, _) = EvaluateJokbo(selectedCards);

        // 변경했던 조커 데이터 원상복구
        RestoreTemporaryCardData();

        return jokbo;
    }

    public void ApplyPlayedCardEnhancements(ScoreCalculationContext context)
    {
        if (context.SelectedCards == null) return;

        int hongDan_DiscardCount = 0;
        int chodan_DiscardCount = 0;
        int cheongDan_DiscardCount = 0;

        if (CardManager_2.Instance != null && CardManager_2.Instance.dropCardBuffer != null)
        {
            // 저승 카드 버퍼를 뒤져봐서
            foreach (var dropped in CardManager_2.Instance.dropCardBuffer)
            {
                // 해당 카드에 홍단, 초단, 청단이 존재할 때 각각 카운트를 1씩 올림
                if (dropped is CardSO droppedCardSO)
                {
                    if (droppedCardSO.currentDan == DanEnhancement.HongDan) hongDan_DiscardCount++;
                    if (droppedCardSO.currentDan == DanEnhancement.ChoDan) chodan_DiscardCount++;
                    if (droppedCardSO.currentDan == DanEnhancement.CheongDan) cheongDan_DiscardCount++;
                }
            }
        }

        if (chodan_DiscardCount >= 3) 
        {
            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 50);
            Debug.Log($"<color=yellow>[강화 발동]</color> 초단 세트 효과 (무덤 3장)! 칩 +50 -> 현재 칩: {context.ChipValue}");
        }
        if (hongDan_DiscardCount >= 3) 
        {
            context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
            Debug.Log($"<color=yellow>[강화 발동]</color> 홍단 세트 효과 (무덤 3장)! 배수 *1.5 -> 현재 배수: {context.Multiplier}");
        }
        if (cheongDan_DiscardCount >= 3)
        {
            // 청단 세트 효과 기획이 있다면 이곳에 추가하세요!
            // 예시: context.ChipValue += 100;
        }

        // 3. 제출한 카드들의 개별 강화 및 에디션 효과 적용
        foreach (var cardCtrl in context.SelectedCards)
        {
            if (cardCtrl != null && cardCtrl.cardData is CardSO cardSO)
            {
                Debug.Log($"--- 카드 [{cardSO.name}] 검사 중 ---");

                int triggerCount = 1;
                // 홍단은 효과를 2번 발동시킴!
                if (cardSO.currentDan == DanEnhancement.HongDan)
                {
                    triggerCount = 2; 
                }
                
                for (int i = 0; i < triggerCount; i++)
                {
                    if (i > 0) 
                    {
                        Debug.Log($"<color=orange>[재발동]</color> 홍단 효과로 인한 [{cardSO.name}] {i + 1}번째 발동");
                        int effectiveBaseScore = ResolveNormalCardBaseScore(cardSO);
                        context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, effectiveBaseScore);
                        Debug.Log($"<color=yellow>[기본 점수 재발동]</color> 칩 +{effectiveBaseScore} -> 현재 칩: {context.ChipValue}");
                    }

                    // 에디션 효과
                    if (cardSO.currentEdition == EditionType.Silver) 
                    {
                        context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 50);
                        Debug.Log($"<color=yellow>[카드 에디션 발동]</color> 은박: 칩 +50 -> 현재 칩: {context.ChipValue}");
                    }
                    if (cardSO.currentEdition == EditionType.Gold) 
                    {
                        context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, 10d);
                        Debug.Log($"<color=yellow>[카드 에디션 발동]</color> 금박: 배수 +10 -> 현재 배수: {context.Multiplier}");
                    }
                    if (cardSO.currentEdition == EditionType.Radiant) 
                    {
                        context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
                        Debug.Log($"<color=yellow>[카드 에디션 발동]</color> 찬란(나전): 배수 *1.5 -> 현재 배수: {context.Multiplier}");
                    }

                    // 광 강화 효과
                    if (cardSO.currentGwang == GwangEnhancement.Gwang) 
                    {
                        context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, GwangEnhancementMultiplier);
                        Debug.Log($"<color=yellow>[강화 발동]</color> 광(光): 배수 *{GwangEnhancementMultiplier:0.0} -> 현재 배수: {context.Multiplier}");
                    }

                    // 열끗 강화 효과
                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Warbler) 
                    {
                        context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 40);
                        Debug.Log($"<color=yellow>[강화 발동]</color> 휘파람새: 칩 +40 -> 현재 칩: {context.ChipValue}");
                    }
                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Cuckoo) 
                    {
                        if (ProbabilityResolver.RollChance(0.25f))
                        {
                            context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 2d);
                            Debug.Log($"<color=yellow>[강화 발동]</color> 두견새 ({ProbabilityResolver.FormatChance(0.25f)} 성공!): 배수 *2 -> 현재 배수: {context.Multiplier}");
                        }
                        else Debug.Log($"<color=gray>[강화 실패]</color> 두견새 ({ProbabilityResolver.FormatChance(0.25f)} 빗나감)");
                    }
                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Butterfly) 
                    {
                        if (ProbabilityResolver.RollChance(0.5f))
                        {
                            context.ChipValue = ScoreSafeMath.AddScore(context.ChipValue, 60);
                            Debug.Log($"<color=yellow>[강화 발동]</color> 나비 ({ProbabilityResolver.FormatChance(0.5f)} 성공!): 칩 +60 -> 현재 칩: {context.ChipValue}");
                        }
                        else Debug.Log($"<color=gray>[강화 실패]</color> 나비 ({ProbabilityResolver.FormatChance(0.5f)} 빗나감)");
                    }
                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Boar) 
                    {
                        context.Multiplier = ScoreSafeMath.AddMultiplier(context.Multiplier, 5d);
                        // 덱에 있는 카드 3장 버림 코드
                        if (CardManager_2.Instance != null && CardManager_2.Instance.cardBuffer != null)
                        {
                            int cardsToDiscard = Mathf.Min(3, CardManager_2.Instance.cardBuffer.Count);

                            for (int j = 0; j < cardsToDiscard; j++)
                            {
                                int randomIndex = UnityEngine.Random.Range(0, CardManager_2.Instance.cardBuffer.Count);
                                BaseCardSO cardToDrop = CardManager_2.Instance.cardBuffer[randomIndex];
                                
                                CardManager_2.Instance.cardBuffer.RemoveAt(randomIndex);
                                CardManager_2.Instance.dropCardBuffer.Add(cardToDrop);
                            }
                            CardManager_2.Instance.RefreshRemainingDeckCountUI();
                            Debug.Log($"<color=yellow>[강화 발동]</color> 멧돼지: 배수 +5 및 덱에서 {cardsToDiscard}장 버림 -> 현재 배수: {context.Multiplier}");
                        }
                    }

                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Deer) 
                    {
                        Debug.Log($"<color=gray>[효과 확인]</color> 사슴 카드가 제출되었습니다. (재화 획득은 손패에 있을 때만 발동합니다)");
                    }

                    // 단 강화 개별 효과
                    if (cardSO.currentDan == DanEnhancement.ChoDan) 
                    {
                        // TODO: 아이템 생성 로직 (ConsumableManager 연동)
                        if (ConsumableManager.Instance != null) 
                        {
                            ConsumableManager.Instance.AddRandomConsumable(1, GameText.Get("game.ScoreManager.ApplyPlayedCardEnhancements.8cc9a5b6ba", "초단"));
                            Debug.Log($"<color=yellow>[강화 발동]</color> 초단 개별: 무작위 소모품 1개 생성!");
                        }
                    }

                    if (cardSO.currentDan == DanEnhancement.CheongDan) 
                    {
                        if (EconomyManager.instance != null)
                        {
                            EconomyManager.instance.AddMoney(3);
                            Debug.Log($"<color=yellow>[강화 발동]</color> 청단 개별: 3전 획득!");
                        }
                    }
                }
            }
        }

        // 4. 손패에 남아있는 카드들의 패시브 효과 (기러기, 사슴)
        if (CardManager_2.Instance != null && CardManager_2.Instance.myHandCards != null)
        {
            foreach (var handCard in CardManager_2.Instance.myHandCards)
            {
                if (context.SelectedCards.Contains(handCard)) continue; 
                
                if (handCard != null && handCard.cardData is CardSO cardSO)
                {
                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Goose)
                    {
                        context.Multiplier = ScoreSafeMath.MultiplyMultiplier(context.Multiplier, 1.5d);
                        Debug.Log($"<color=yellow>[패시브 발동]</color> 손패의 기러기: 배수 *1.5 -> 현재 배수: {context.Multiplier}");
                    }

                    if (cardSO.currentYulGgeut == YulGgeutEnhancement.Deer)
                    {
                        if (EconomyManager.instance != null)
                        {
                            EconomyManager.instance.AddMoney(3);
                            Debug.Log($"<color=yellow>[패시브 발동]</color> 손패의 사슴: +3 전 -> 현재 전: {EconomyManager.instance.ecoData.currentMoney}");
                        }
                    }
                }
            }
        }
        
    }

    private JokboRuleProfile ResolveJokboRuleProfile()
    {
        if (jokboRuleProfile != null)
            return jokboRuleProfile;

        JokboRuleProfile resourcesProfile = Resources.Load<JokboRuleProfile>("DefaultJokboRuleProfile");
        if (resourcesProfile != null)
        {
            jokboRuleProfile = resourcesProfile;
            return jokboRuleProfile;
        }

        return JokboRuleProfile.RuntimeDefault;
    }
}
