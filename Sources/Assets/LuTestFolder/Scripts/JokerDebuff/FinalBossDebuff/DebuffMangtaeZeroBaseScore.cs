using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Debuff_Mangtae_ZeroBaseScore",
    menuName = "Boss Debuff/망태할아범 (모든 패 기본점수 0)")]
public sealed class DebuffMangtaeZeroBaseScore : BossDebuffSO
{
    private readonly Dictionary<CardSO, int> originalScores = new Dictionary<CardSO, int>();

    /// <summary>라운드에 존재하는 일반 패의 원래 기본점수를 저장하고 모두 0으로 만듭니다.</summary>
    public override void InitDebuff()
    {
        RestoreOriginalScores();
        EnsureBaseScoresAreZero();

        GameLog.Info($"[망태할아범][잡아채는 손길] 디버프 활성화: 이번 라운드의 모든 패 기본점수를 0으로 만듭니다. ({originalScores.Count}장)");
    }

    /// <summary>수동 디버프 지정 경로에서도 실제 제출 점수가 반드시 0이 되도록 다시 보정합니다.</summary>
    public override void OnBeforeScoreCalculation(List<CardController> selectedCards)
    {
        EnsureBaseScoresAreZero();
        GameLog.Info($"[망태할아범][잡아채는 손길] 디버프 발동: 제출 패 {CountNormalCards(selectedCards)}장의 기본점수 0 적용");
    }

    /// <summary>라운드 종료 또는 디버프 교체 시 저장해 둔 기본점수를 복원합니다.</summary>
    public override void ResetDebuff()
    {
        RestoreOriginalScores();
    }

    /// <summary>점수 변경은 라운드 시작에 적용했으므로 제출 시에는 별도 계산을 하지 않습니다.</summary>
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        // 라운드 시작 시 CardSO의 기본점수를 이미 0으로 만들었으므로 제출 시 추가 처리는 없습니다.
    }

    /// <summary>현재 덱과 전투 영역의 일반 패를 기록한 뒤 기본점수를 0으로 유지합니다.</summary>
    private void EnsureBaseScoresAreZero()
    {
        CaptureDeckCards();
        CaptureCombatCards();

        foreach (KeyValuePair<CardSO, int> pair in originalScores)
        {
            if (pair.Key != null)
                pair.Key.cardScore = 0;
        }
    }

    /// <summary>발동 로그에 표시할 제출 일반 패 수를 계산합니다.</summary>
    private static int CountNormalCards(List<CardController> selectedCards)
    {
        if (selectedCards == null)
            return 0;

        int count = 0;
        for (int i = 0; i < selectedCards.Count; i++)
        {
            if (selectedCards[i] != null && selectedCards[i].cardData is CardSO)
                count++;
        }

        return count;
    }

    /// <summary>플레이어의 마스터 덱에 포함된 모든 일반 패를 복원 목록에 기록합니다.</summary>
    private void CaptureDeckCards()
    {
        if (GameManager.instance == null || GameManager.instance.masterDeck == null)
            return;

        for (int i = 0; i < GameManager.instance.masterDeck.Count; i++)
            CaptureCard(GameManager.instance.masterDeck[i]);
    }

    /// <summary>현재 전투의 드로우·버림·손패에 있는 일반 패를 복원 목록에 기록합니다.</summary>
    private void CaptureCombatCards()
    {
        CardManager_2 cardManager = CardManager_2.Instance;
        if (cardManager == null)
            return;

        CaptureCards(cardManager.cardBuffer);
        CaptureCards(cardManager.dropCardBuffer);

        if (cardManager.myHandCards == null)
            return;

        for (int i = 0; i < cardManager.myHandCards.Count; i++)
        {
            CardController controller = cardManager.myHandCards[i];
            if (controller != null)
                CaptureCard(controller.cardData);
        }
    }

    /// <summary>카드 데이터 목록을 순회하며 일반 패만 기록합니다.</summary>
    private void CaptureCards(List<BaseCardSO> cards)
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Count; i++)
            CaptureCard(cards[i]);
    }

    /// <summary>같은 카드가 중복 저장되지 않도록 최초 기본점수만 보관합니다.</summary>
    private void CaptureCard(BaseCardSO card)
    {
        if (card is CardSO normalCard && !originalScores.ContainsKey(normalCard))
            originalScores.Add(normalCard, normalCard.cardScore);
    }

    /// <summary>저장된 기본점수를 전부 되돌리고 라운드 임시 상태를 비웁니다.</summary>
    private void RestoreOriginalScores()
    {
        foreach (KeyValuePair<CardSO, int> pair in originalScores)
        {
            if (pair.Key != null)
                pair.Key.cardScore = pair.Value;
        }

        if (originalScores.Count > 0)
            GameLog.Info($"[망태할아범][잡아채는 손길] 디버프 해제: 패 {originalScores.Count}장의 기본점수를 원래 상태로 복원합니다.");

        originalScores.Clear();
    }
}

