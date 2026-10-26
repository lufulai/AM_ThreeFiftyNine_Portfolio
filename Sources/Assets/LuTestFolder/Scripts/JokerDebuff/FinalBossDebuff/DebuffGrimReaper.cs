using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Debuff_GrimReaper", menuName = "Boss Debuff/저승사자 (강화 초기화)")]

public class DebuffGrimReaper : BossDebuffSO
{
    public override void InitDebuff()
    {
        // 전투에 진입한 이번 라운드의 카드덱 버퍼만 초기화합니다.
        if (CardManager_2.Instance != null && CardManager_2.Instance.cardBuffer != null)
        {
            foreach (var card in CardManager_2.Instance.cardBuffer)
            {
                if (card is CardSO c)
                {
                    c.currentDan = DanEnhancement.None;
                    c.isGwang = false;
                    c.currentGwang = GwangEnhancement.None;
                    c.ggeutValue = 0;
                    c.currentEdition = EditionType.None;
                }
            }
            GameLog.Info("[저승사자] 모든 카드의 기억(강화)이 지워졌습니다.");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        
    }
}
