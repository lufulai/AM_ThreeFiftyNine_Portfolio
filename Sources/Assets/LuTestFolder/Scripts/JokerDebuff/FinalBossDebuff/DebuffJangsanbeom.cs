using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Debuff_Jangsanbeom", menuName = "Boss Debuff/장산범 (강제 선택)")]

public class DebuffJangsanbeom : BossDebuffSO
{
    public CardController forcedCard;

    public override void InitDebuff() 
    { 
        forcedCard = null; 
    }

    public override void ResetDebuff() 
    { 
        forcedCard = null; 
    }

    public bool IsForcedCard(CardController card)
    {
        return forcedCard != null && forcedCard == card;
    }

    // 카드를 드로우 한 직후 누군가 이 함수를 호출해 주어야 합니다.
    public void ForceSelectRandomCard()
    {
        if (CardManager_2.Instance == null || CardManager_2.Instance.myHandCards.Count == 0) return;

        // 선택되지 않은 카드 중 하나 골라서 강제 선택
        var unselected = CardManager_2.Instance.myHandCards.FindAll(c => !c.IsSelected);
        if (unselected.Count > 0)
        {
            forcedCard = unselected[Random.Range(0, unselected.Count)];
            CardManager_2.Instance.OnCardSelected(forcedCard);
            GameLog.Info("[장산범] 기이한 목소리에 홀려 카드가 강제 선택되었습니다.");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        // 카드를 내면 다음 드로우를 위해 강제 선택을 초기화합니다.
        forcedCard = null;
    }
}