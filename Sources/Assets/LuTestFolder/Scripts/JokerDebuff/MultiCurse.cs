using System.Collections.Generic;
using UnityEngine;

// 점수 저주 [기본 점수와 배수 반토막]
[CreateAssetMenu(fileName = "MultiCurse", menuName = "Stage/MultiCurse")]
public class MultiCurse : BossDebuffSO
{
    private bool isSet = false;
    private const double DebuffRate = 0.5d;

    public override void ResetDebuff()
    {
        isSet = false;
    }

    public override void InitDebuff()
    {
        if(!isSet)
        {
            isSet = true;
            GameLog.Info("배수 저주 발동");
        }
    }
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        chip = ScoreSafeMath.MultiplyScore(chip, DebuffRate);
        multiplier = ScoreSafeMath.MultiplyMultiplier(multiplier, DebuffRate);

        GameLog.Info($"점수/배수 50% 감소 저주 적용, 최종 칩: {chip}, 최종 배수: {multiplier}");
    }
}
