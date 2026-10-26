using System.Collections.Generic;
using UnityEngine;

// 골드 줄이기
[CreateAssetMenu(fileName = "ReduceGold", menuName = "Stage/ReduceGold")]
public class ReduceGold : BossDebuffSO
{
    private bool isSet = false;

    public override void ResetDebuff()
    {
        isSet = false;
    }

    public override void InitDebuff()
    {
        if (!isSet)
        {
            isSet = true;
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        int playedCardCount = selectedCards != null ? selectedCards.Count : 0;
        if (playedCardCount <= 0 || EconomyManager.instance == null || EconomyManager.instance.ecoData == null)
        {
            return;
        }

        EconomyManager.instance.ecoData.currentMoney -= playedCardCount;
        GameLog.Info($"골드 감소 저주 적용, 제출 패 {playedCardCount}장, 현재 골드 {EconomyManager.instance.ecoData.currentMoney}");
    }
}
