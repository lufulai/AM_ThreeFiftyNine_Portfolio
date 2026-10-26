using System.Collections.Generic;
using UnityEngine;

// 손패 줄이기
[CreateAssetMenu(fileName = "ReduceHandCard", menuName = "Stage/ReduceHandCard")]
public class ReduceHandCard : BossDebuffSO
{
    private bool isSet = false;
    private int originalMaxHandCard;
    private bool hasOriginalMaxHandCard;

    public override void ResetDebuff()
    {
        if (CombatManager.instance != null && hasOriginalMaxHandCard)
            CombatManager.instance.maxHandCard = originalMaxHandCard;

        originalMaxHandCard = 0;
        hasOriginalMaxHandCard = false;
        isSet = false;
    }

    public override void InitDebuff()
    {
        if (!isSet && CombatManager.instance != null)
        {
            isSet = true;
            originalMaxHandCard = CombatManager.instance.maxHandCard;
            hasOriginalMaxHandCard = true;

            CombatManager.instance.maxHandCard = Mathf.Max(0, originalMaxHandCard - 1);
            GameLog.Info($"손패 줄이기 저주 적용, 손패 크기 {originalMaxHandCard} -> {CombatManager.instance.maxHandCard}");

            if (CombatManager.instance.maxHandCard <= 0)
            {
                GameLog.Info("손패 크기가 0이 되어 패배 처리합니다.");
                CombatManager.instance.GameOver();
            }
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
    }
}
