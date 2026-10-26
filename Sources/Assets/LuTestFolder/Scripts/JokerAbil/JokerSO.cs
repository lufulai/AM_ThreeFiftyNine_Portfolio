using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu (fileName = "JokerSO", menuName = "Card/JokerSO")]
public class JokerSO : BaseCardSO
{
    public override CardType cardType => CardType.Joker;

    [Header("기본 정보")]
    [Tooltip("저장 데이터에서 사용하는 고정 ID입니다. 비어 있으면 카드 ID 또는 에셋 이름을 사용합니다.")]
    public string jokerID;
    public string jokerName; // 조커 이름 (UI 표시 및 ID 역할)
    [TextArea(3, 5)]
    public string description; // 조커 효과 설명
    public Sprite jokerSprite; // 조커 스프라이트
    public Sprite unselectedJokerSprite; // 조커 선택 풀에서 아직 고르지 않았을 때 보여줄 회색 스프라이트
    public int bonusChip = 0; // 조커의 기본 칩 [ 칩을 주는 조커가 존재 ]
    // 런타임 복제본만 성장합니다. 새 여정에서는 0, 이어하기에서는 체크포인트 값으로 복원합니다.
    [System.NonSerialized] public int accumulatedChips;

    [Header("강화 및 에디션 상태")]
    public GwangEnhancement currentGwang = GwangEnhancement.None;
    public EditionType currentEdition = EditionType.None;

    [Header("해금 조건")]
    // UnlockManager에서 사용하는 것과 동일한 enum을 사용해야 합니다.
    // 만약 다른 파일에 있다면, 해당 파일을 참조하도록 해주세요.
    public UnlockConditionType unlockType = UnlockConditionType.None; 
    public int unlockValue;
    public string unlockDescription; // 해금 조건 설명 텍스트 (예: "플레이 10회 달성")
    [Tooltip("지정한 탈을 덱에 넣고 승리하는 조건에 사용하는 탈의 고정 ID입니다.")]
    public string unlockRequiredJokerID;

    [Header("조커 효과")]
    public List<JokerAbilityEntry> abilities; // = new List<JokerAbilityEntry>();

    /// <summary>표시 이름과 분리된 저장용 ID를 반환합니다.</summary>
    public string GetPersistentID()
    {
        if (!string.IsNullOrWhiteSpace(jokerID))
            return jokerID;
        if (!string.IsNullOrWhiteSpace(cardID))
            return cardID;
        if (!string.IsNullOrWhiteSpace(name))
            return NormalizeRuntimeName(name);
        return jokerName;
    }

    /// <summary>런타임 복제본 이름의 Clone 접미사를 제거해 저장 ID가 실행마다 달라지지 않게 합니다.</summary>
    private static string NormalizeRuntimeName(string value)
    {
        const string cloneSuffix = "(Clone)";
        string normalized = value.Trim();
        while (normalized.EndsWith(cloneSuffix, StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).TrimEnd();

        return normalized;
    }
    

    /// <summary>
    /// 특정 트리거에 맞는 이 조커의 모든 능력을 발동시킵니다.
    /// GameManager, ScoreManager 등 다양한 곳에서 각기 다른 트리거로 호출될 수 있습니다.
    /// </summary>
    public void ActivateAbilities(JokerTriggerType currentTrigger, ref long chips, ref double mult, EconomyManager economy, List<BaseCardSO> activeCards, int currentHands, int currentDiscards, int deckCount, JokBoType jokbo)
    {
        ActivateAbilities(currentTrigger, ref chips, ref mult, economy, activeCards, currentHands, currentDiscards, deckCount, jokbo, null);
    }

    public void ActivateAbilities(
        JokerTriggerType currentTrigger,
        ref long chips,
        ref double mult,
        EconomyManager economy,
        List<BaseCardSO> activeCards,
        int currentHands,
        int currentDiscards,
        int deckCount,
        JokBoType jokbo,
        Action<JokerAbilityEntry, long, double, long, double> onAbilityActivated)
    {
        if (abilities == null)
            return;

        foreach (var entry in abilities)
        {
            if (entry.ability != null)
                entry.ability.ParentJoker = this;

            // 1. 트리거 타입이 맞지 않으면 실행하지 않음
            if (entry.trigger != currentTrigger) continue;

            // 2. 발동 조건을 만족하는지 확인
            if (!CheckCondition(entry, economy, activeCards, currentHands, currentDiscards, deckCount, jokbo)) continue;

            // 새 런 준비 단계에서 이미 지급한 시작 재화는 첫 전투에서 다시 발동시키지 않습니다.
            if (entry.ability is AbilityStartMoney startMoney && startMoney.HasGrantedMoney) continue;
            
            // 3. 능력 발동
            if (entry.ability != null)
            {
                long beforeChips = chips;
                double beforeMult = mult;
                GameLog.Info($"[조커 효과 발동] {jokerName} - {entry.ability.GetType().Name}");
                entry.ability.OnActivate(ref chips, ref mult, economy, activeCards, currentHands, currentDiscards, deckCount, jokbo);
                ScoreSafeMath.SanitizeScoreState(ref chips, ref mult);
                onAbilityActivated?.Invoke(entry, beforeChips, beforeMult, chips, mult);
            }
        }
    }

    // 조건을 검사하는 헬퍼 함수
    private bool CheckCondition(JokerAbilityEntry entry, EconomyManager economy, List<BaseCardSO> activeCards, int currentHands, int currentDiscards, int deckCount, JokBoType currentJokbo)
    {
        // ArtifactData.cs의 CheckCondition 메서드를 참고하여 상세 로직을 구현할 수 있습니다.
        switch (entry.condition)
        {
            case JokerConditionType.None:
                return true;
            
            case JokerConditionType.IsInJokbo:
                // 1. 이 카드가 제출된 카드(activeCards)에 포함되어 있는지 먼저 확인
                if (!IsThisJokerInCards(activeCards)) return false;
                // conditionValue에 입력된 숫자(예: 9, 10, 11)를 JokBoType으로 캐스팅하여 현재 제출된 족보와 같은지 확인
                return currentJokbo == (JokBoType)entry.conditionValue;

            case JokerConditionType.IsInDiscardPile:
                return IsThisJokerInDiscardPile();

            case JokerConditionType.IsPlayed:
                if (activeCards == null) return false;
                // 같은 종류를 두 장 선택할 수 있으므로 실제 제출한 인스턴스인지 확인합니다.
                return IsThisJokerInCards(activeCards);

            case JokerConditionType.IsInHand:
                return !IsThisJokerInCards(activeCards) && IsThisJokerInHand();

            case JokerConditionType.PlayedHandSizeIs:
                if (activeCards == null) return false;
                // 해당 카드가 핸드 플레이에 포함되었을 때만 장수를 검사하려면 아래 줄 추가
                if (!IsThisJokerInCards(activeCards)) return false;
                return activeCards.Count == entry.conditionValue;

            case JokerConditionType.DiscardPileCountMoreThan:
                return IsThisJokerInCards(activeCards) && GetDiscardPileCount() >= entry.conditionValue;
            
            case JokerConditionType.ThisCardIsDiscarded:
                if (activeCards == null) return false;
                // 버려진 카드 목록(activeCards)에 현재 조커와 같은 카드가 포함되어 있는지 확인
                return IsThisJokerInCards(activeCards);
           
            // ... 기타 조건들 구현
            default:
                return true;
        }
    }

    private bool IsThisJokerInCards(List<BaseCardSO> cards)
    {
        if (cards == null)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (IsSameJoker(cards[i]))
                return true;
        }

        return false;
    }

    private bool IsThisJokerInHand()
    {
        if (CardManager_2.Instance == null || CardManager_2.Instance.myHandCards == null)
            return true;

        for (int i = 0; i < CardManager_2.Instance.myHandCards.Count; i++)
        {
            CardController handCard = CardManager_2.Instance.myHandCards[i];
            if (handCard != null && IsSameJoker(handCard.cardData))
                return true;
        }

        return false;
    }

    private bool IsThisJokerInDiscardPile()
    {
        if (CardManager_2.Instance == null || CardManager_2.Instance.dropCardBuffer == null)
            return false;

        for (int i = 0; i < CardManager_2.Instance.dropCardBuffer.Count; i++)
        {
            if (IsSameJoker(CardManager_2.Instance.dropCardBuffer[i]))
                return true;
        }

        return false;
    }

    private static int GetDiscardPileCount()
    {
        return CardManager_2.Instance != null && CardManager_2.Instance.dropCardBuffer != null
            ? CardManager_2.Instance.dropCardBuffer.Count
            : 0;
    }

    private bool IsSameJoker(BaseCardSO card)
    {
        if (card == null)
            return false;

        // 컨트롤러/손패/저승 모두 masterDeck의 런타임 복제본을 공유합니다.
        // 이름으로 비교하면 동일한 탈 두 장의 제출/손패/저승 상태가 섞입니다.
        return ReferenceEquals(card, this);
    }
}
