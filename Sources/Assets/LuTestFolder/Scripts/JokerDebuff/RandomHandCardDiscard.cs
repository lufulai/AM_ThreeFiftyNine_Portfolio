using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Random HandCard Discard", menuName = "Stage/Random HandCard Discard")]
public class RandomHandCardDiscard : BossDebuffSO
{
    private const int DiscardCount = 1;

    public override void ResetDebuff()
    {
        
    }

    public override void InitDebuff()
    {
        GameLog.Info("[Boss] RandomHandCardDiscard 발동: 핸드 플레이 후 사용하지 않은 손패 1장을 무작위로 버립니다.");
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        
    }

    public bool TryPickUnusedHandCard(
        List<CardController> handCards,
        List<CardController> playedCards,
        out CardController cardToDiscard)
    {
        cardToDiscard = null;

        if (handCards == null || handCards.Count == 0)
            return false;

        List<CardController> candidates = new List<CardController>(handCards.Count);
        for (int i = 0; i < handCards.Count; i++)
        {
            CardController candidate = handCards[i];
            if (candidate == null)
                continue;

            if (playedCards != null && playedCards.Contains(candidate))
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count < DiscardCount)
            return false;

        cardToDiscard = candidates[Random.Range(0, candidates.Count)];
        return cardToDiscard != null;
    }
}
