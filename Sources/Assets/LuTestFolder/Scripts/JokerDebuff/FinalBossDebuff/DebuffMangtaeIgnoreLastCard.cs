using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Debuff_Mangtae_IgnoreLastCard",
    menuName = "Boss Debuff/망태할아범 (마지막 제출 패 점수 제외)")]
public sealed class DebuffMangtaeIgnoreLastCard : BossDebuffSO
{
    /// <summary>실제 제출 직전에 제외 대상인 마지막 패를 로그로 알립니다.</summary>
    public override void OnBeforeScoreCalculation(List<CardController> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count == 0)
        {
            GameLog.Info("[망태할아범][망태기 납치] 디버프 발동: 제외할 제출 패가 없습니다.");
            return;
        }

        CardController lastCard = selectedCards[selectedCards.Count - 1];
        string cardName = lastCard != null && lastCard.cardData != null
            ? lastCard.cardData.name
            : GameText.Get("game.DebuffMangtaeIgnoreLastCard.OnBeforeScoreCalculation.54c1cdfcf3", "알 수 없는 패");
        GameLog.Info($"[망태할아범][망태기 납치] 디버프 발동: 마지막 제출 패 '{cardName}'는 족보에만 포함하고 점수에서 제외합니다.");
    }

    /// <summary>제출 순서상 마지막 패만 개별 점수 처리에서 제외하도록 알립니다.</summary>
    public override bool ShouldIgnoreSubmittedCardScore(
        CardController card,
        int submittedIndex,
        int submittedCount)
    {
        return card != null && submittedCount > 0 && submittedIndex == submittedCount - 1;
    }

    /// <summary>실제 제외 처리는 족보 판정 뒤 ScoreManager에서 수행합니다.</summary>
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        // ScoreManager가 족보 판정 후 마지막 제출 패의 개별 점수 처리를 건너뜁니다.
    }
}

